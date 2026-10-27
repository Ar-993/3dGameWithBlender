using Ars3D.Items;
using Ars3D.Items.Physics;
using Microsoft.Xna.Framework;
using NumericsVector = System.Numerics.Vector3;

namespace _3DLight.Items;

internal sealed class ItemCatalogData
{
    public List<ItemDefinitionData> Definitions { get; init; } = [];
    public List<ItemSpawnData> Spawns { get; init; } = [];
}

// Описание типа предмета в items.json. ItemDefinition создаётся после загрузки модели,
// когда известны её размеры и преобразование к заданной высоте.
internal sealed class ItemDefinitionData
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Model { get; init; } = "";
    public string? TextureFolder { get; init; }
    public float Height { get; init; } = 0.5f;
    public float Radius { get; init; } = 0.12f;
    public float[] SpawnSpin { get; init; } = [0f, 0f, 0f];
    public float HeldYawDegrees { get; init; }
    public RigidBodySettings? Physics { get; init; }
    public ItemCarrySettingsData? Interaction { get; init; }
}

internal sealed class ItemSpawnData
{
    public string ItemId { get; init; } = "";
    public string Marker { get; init; } = "";
    public float[]? Fallback { get; init; }
}

internal sealed record ItemSpawnEntry(string ItemId, string Marker, Vector3? Fallback);

internal sealed class ItemCarrySettingsData
{
    public float[]? HeldOffset { get; init; }
    public float? ThrowLift { get; init; }
    public float[]? ThrowSpin { get; init; }

    public ItemCarrySettings ToSettings()
    {
        var defaults = new ItemCarrySettings();
        return new ItemCarrySettings(
            heldOffset: HeldOffset is null
                ? defaults.HeldOffset
                : ItemCatalogVectors.Read(HeldOffset, "heldOffset"),
            throwLift: ThrowLift ?? defaults.ThrowLift,
            throwSpin: ThrowSpin is null
                ? defaults.ThrowSpin
                : ItemCatalogVectors.Read(ThrowSpin, "throwSpin"));
    }
}

internal static class ItemCatalogVectors
{
    public static NumericsVector Read(float[] values, string property)
    {
        if (values.Length != 3 || values.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException($"Item catalog '{property}' must contain three finite numbers.");
        return new(values[0], values[1], values[2]);
    }
}
