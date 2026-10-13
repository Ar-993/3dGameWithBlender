using Ars3D.Items;
using Ars3D.Items.Physics;
using Microsoft.Xna.Framework;
using NumericsVector = System.Numerics.Vector3;

namespace _3DLight.Items;

internal sealed class ItemCatalogData
{
    public List<ModelItemEntry> Definitions { get; init; } = [];
    public List<ItemSpawnData> Spawns { get; init; } = [];
    public ItemControlData? Interaction { get; init; }
}

internal sealed class ModelItemEntry
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
}

internal sealed class ItemSpawnData
{
    public string ItemId { get; init; } = "";
    public string Marker { get; init; } = "";
    public float[]? Fallback { get; init; }
}

internal sealed record ItemSpawnEntry(string ItemId, string Marker, Vector3? Fallback);

internal sealed class ItemControlData
{
    public float[]? HeldOffset { get; init; }
    public float? PickupDistance { get; init; }
    public float? ThrowSpeed { get; init; }
    public float? ThrowLift { get; init; }
    public float[]? ThrowSpin { get; init; }
    public float? ReleaseClearance { get; init; }

    public ItemInteractionSettings ToSettings()
    {
        var defaults = new ItemInteractionSettings();
        return new(
            HeldOffset is null ? defaults.HeldOffset : ItemCatalogVectors.Read(HeldOffset, "heldOffset"),
            PickupDistance ?? defaults.PickupDistance,
            ThrowSpeed ?? defaults.ThrowSpeed,
            ThrowLift ?? defaults.ThrowLift,
            ThrowSpin is null ? defaults.ThrowSpin : ItemCatalogVectors.Read(ThrowSpin, "throwSpin"),
            ReleaseClearance ?? defaults.ReleaseClearance);
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
