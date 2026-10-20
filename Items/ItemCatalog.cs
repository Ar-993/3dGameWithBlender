using Ars3D.Items;
using _3DLight.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace _3DLight.Items;

// Definitions are data; models are loaded once, only when their first instance is requested.
internal sealed class ItemCatalog : IDisposable
{
    private readonly GraphicsDevice graphics;
    private readonly Texture2D fallback;
    private readonly Effect effect;
    private readonly Dictionary<string, ModelItemEntry> entries;
    private readonly Dictionary<string, LoadedItem> loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Model, string Folder), CompiledModel> models = [];
    private bool disposed;

    public IReadOnlyList<ItemSpawnEntry> Spawns { get; }
    public IReadOnlyCollection<string> ItemIds => entries.Keys;
    internal int LoadedModelCount => models.Count;

    private ItemCatalog(GraphicsDevice graphics, Texture2D fallback, Effect effect, ItemCatalogData data)
    {
        this.graphics = graphics;
        this.fallback = fallback;
        this.effect = effect;
        entries = new(StringComparer.OrdinalIgnoreCase);
        foreach (ModelItemEntry entry in data.Definitions)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.DisplayName) ||
                string.IsNullOrWhiteSpace(entry.Model))
                throw new InvalidDataException("Each item definition needs id, displayName and model.");
            if (!entries.TryAdd(entry.Id, entry))
                throw new InvalidDataException($"Duplicate item definition '{entry.Id}'.");
        }
        var spawns = new List<ItemSpawnEntry>();
        foreach (ItemSpawnData spawn in data.Spawns)
        {
            if (!entries.ContainsKey(spawn.ItemId))
                throw new InvalidDataException($"Spawn references unknown item '{spawn.ItemId}'.");
            Vector3? position = spawn.Fallback is null ? null :
                ItemMath.ToGame(ItemCatalogVectors.Read(spawn.Fallback, "fallback"));
            spawns.Add(new(spawn.ItemId, spawn.Marker, position));
        }
        Spawns = spawns.AsReadOnly();
    }

    public static ItemCatalog Load(GraphicsDevice graphics, Texture2D fallback, Effect effect, string? path = null)
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
        return new(graphics, fallback, effect, data);
    }

    public WorldItem Create(string itemId, Vector3 position)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        LoadedItem entry = GetLoadedItem(itemId);
        var instance = new ItemInstance(entry.Definition, ItemMath.ToLibrary(position));
        var item = new WorldItem(instance);
        item.AttachModel(entry.Model, entry.Normalization);
        return item;
    }

    private LoadedItem GetLoadedItem(string itemId)
    {
        if (loaded.TryGetValue(itemId, out LoadedItem? result))
            return result;
        if (!entries.TryGetValue(itemId, out ModelItemEntry? entry))
            throw new KeyNotFoundException($"Unknown item definition '{itemId}'.");
        string folder = entry.TextureFolder ?? entry.Model;
        var key = (entry.Model.ToLowerInvariant(), folder.ToLowerInvariant());
        if (!models.TryGetValue(key, out CompiledModel? model))
        {
            model = ModelAssetLoader.Load(graphics, entry.Model, fallback, effect, folder);
            models.Add(key, model);
        }
        Matrix normalization = ItemModelTransform.FitToHeight(model.Data, model.Hierarchy, entry.Height);
        BoundingBox bounds = ItemModelTransform.GetBindPoseBounds(model.Data, model.Hierarchy);
        Vector3 halfExtents = (Vector3.Transform(bounds.Max, normalization) -
            Vector3.Transform(bounds.Min, normalization)) * 0.5f;
        var definition = new ItemDefinition(entry.Id, entry.DisplayName, entry.Radius, entry.Height,
            ItemMath.ToLibrary(halfExtents), ItemCatalogVectors.Read(entry.SpawnSpin, "spawnSpin"),
            MathHelper.ToRadians(entry.HeldYawDegrees), entry.Physics, entry.Interaction?.ToSettings());
        result = new(definition, model, normalization);
        loaded.Add(itemId, result);
        return result;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        foreach (CompiledModel model in models.Values)
            model.Dispose();
        models.Clear();
        loaded.Clear();
        disposed = true;
    }

    private sealed record LoadedItem(ItemDefinition Definition, CompiledModel Model, Matrix Normalization);
}
