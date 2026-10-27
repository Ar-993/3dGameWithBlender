using Ars3D.Items;
using Ars3D.Rendering;
using _3DLight.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Items;

namespace _3DLight.Items;

// Читает описания из JSON и создаёт экземпляры предметов.
// Общие определения и модели загружаются при первом Create и используются повторно.
internal sealed class ItemCatalog : IDisposable
{
    private readonly GraphicsDevice graphicsDevice;
    private readonly Texture2D fallbackTexture;
    private readonly Effect toonEffect;
    private readonly Dictionary<string, ItemDefinitionData> definitionsById;

    // Общие настройки и преобразование модели к заданной высоте хранятся на тип предмета.
    private readonly Dictionary<string, LoadedItemType> loadedItemTypesById =
        new(StringComparer.OrdinalIgnoreCase);

    // Разные типы предметов могут использовать одну модель с одной папкой текстур.
    private readonly Dictionary<(string Model, string TextureFolder), CompiledModel> loadedModels = [];
    private bool disposed;

    public IReadOnlyList<ItemSpawnEntry> Spawns { get; }
    public IReadOnlyCollection<string> ItemIds => definitionsById.Keys;
    internal int LoadedModelCount => loadedModels.Count;

    private ItemCatalog(
        GraphicsDevice graphicsDevice,
        Texture2D fallbackTexture,
        Effect toonEffect,
        ItemCatalogData data)
    {
        this.graphicsDevice = graphicsDevice;
        this.fallbackTexture = fallbackTexture;
        this.toonEffect = toonEffect;
        definitionsById = IndexDefinitions(data.Definitions);
        Spawns = CreateSpawnEntries(data.Spawns);
    }

    public static ItemCatalog Load(
        GraphicsDevice graphicsDevice,
        Texture2D fallbackTexture,
        Effect toonEffect,
        string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "Content", "Items", "items.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        ItemCatalogData data = JsonSerializer.Deserialize<ItemCatalogData>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException($"Empty item catalog: {path}");

        return new ItemCatalog(graphicsDevice, fallbackTexture, toonEffect, data);
    }

    public WorldItem Create(string itemId, Vector3 position)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        LoadedItemType itemType = GetOrLoadItemType(itemId);
        var instance = new ItemInstance(itemType.Definition, ItemMath.ToLibrary(position));
        var item = new WorldItem(instance);
        item.AttachModel(itemType.Model, itemType.Normalization);
        return item;
    }

    private LoadedItemType GetOrLoadItemType(string itemId)
    {
        if (loadedItemTypesById.TryGetValue(itemId, out LoadedItemType? itemType))
            return itemType;

        if (!definitionsById.TryGetValue(itemId, out ItemDefinitionData? definitionData))
            throw new KeyNotFoundException($"Unknown item definition '{itemId}'.");

        CompiledModel model = GetOrLoadModel(definitionData);
        Matrix normalization = ItemModelTransform.FitToHeight(
            model.Data, model.Hierarchy, definitionData.Height);
        ItemDefinition definition = CreateDefinition(definitionData, model, normalization);

        itemType = new LoadedItemType(definition, model, normalization);
        loadedItemTypesById.Add(itemId, itemType);
        return itemType;
    }

    private CompiledModel GetOrLoadModel(ItemDefinitionData definitionData)
    {
        string textureFolder = definitionData.TextureFolder ?? definitionData.Model;
        var modelKey = (definitionData.Model.ToLowerInvariant(), textureFolder.ToLowerInvariant());

        if (loadedModels.TryGetValue(modelKey, out CompiledModel? model))
            return model;

        model = ModelAssetLoader.Load(
            graphicsDevice, ModelPaths.ModelFile(definitionData.Model), fallbackTexture, toonEffect,
            ModelPaths.TextureDirectory(textureFolder));
        loadedModels.Add(modelKey, model);
        return model;
    }

    private static ItemDefinition CreateDefinition(
        ItemDefinitionData data,
        CompiledModel model,
        Matrix normalization)
    {
        BoundingBox bounds = ItemModelTransform.GetBindPoseBounds(model.Data, model.Hierarchy);
        Vector3 normalizedMin = Vector3.Transform(bounds.Min, normalization);
        Vector3 normalizedMax = Vector3.Transform(bounds.Max, normalization);
        Vector3 halfExtents = (normalizedMax - normalizedMin) * 0.5f;

        return new ItemDefinition(
            id: data.Id,
            displayName: data.DisplayName,
            radius: data.Radius,
            height: data.Height,
            halfExtents: ItemMath.ToLibrary(halfExtents),
            spawnAngularVelocity: ItemCatalogVectors.Read(data.SpawnSpin, "spawnSpin"),
            heldYaw: MathHelper.ToRadians(data.HeldYawDegrees),
            bodySettings: data.Physics,
            interaction: data.Interaction?.ToSettings());
    }

    private static Dictionary<string, ItemDefinitionData> IndexDefinitions(
        IEnumerable<ItemDefinitionData> definitions)
    {
        var definitionsById = new Dictionary<string, ItemDefinitionData>(StringComparer.OrdinalIgnoreCase);

        foreach (ItemDefinitionData definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Id) ||
                string.IsNullOrWhiteSpace(definition.DisplayName) ||
                string.IsNullOrWhiteSpace(definition.Model))
            {
                throw new InvalidDataException("Each item definition needs id, displayName and model.");
            }

            if (!definitionsById.TryAdd(definition.Id, definition))
                throw new InvalidDataException($"Duplicate item definition '{definition.Id}'.");
        }

        return definitionsById;
    }

    private IReadOnlyList<ItemSpawnEntry> CreateSpawnEntries(IEnumerable<ItemSpawnData> spawnData)
    {
        var spawns = new List<ItemSpawnEntry>();

        foreach (ItemSpawnData spawn in spawnData)
        {
            if (!definitionsById.ContainsKey(spawn.ItemId))
                throw new InvalidDataException($"Spawn references unknown item '{spawn.ItemId}'.");

            Vector3? fallbackPosition = spawn.Fallback is null
                ? null
                : ItemMath.ToGame(ItemCatalogVectors.Read(spawn.Fallback, "fallback"));

            spawns.Add(new ItemSpawnEntry(spawn.ItemId, spawn.Marker, fallbackPosition));
        }

        return spawns.AsReadOnly();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        foreach (CompiledModel model in loadedModels.Values)
            model.Dispose();

        loadedModels.Clear();
        loadedItemTypesById.Clear();
        disposed = true;
    }

    private sealed record LoadedItemType(
        ItemDefinition Definition,
        CompiledModel Model,
        Matrix Normalization);
}
