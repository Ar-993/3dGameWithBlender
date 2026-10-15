using System.Numerics;
using Ars3D.Items.Physics;

namespace Ars3D.Items;

// Share one definition between as many physical instances as the game needs.
public sealed class ItemDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public float Radius { get; }
    public float Height { get; }
    public Vector3 HalfExtents { get; }
    public Vector3 SpawnAngularVelocity { get; }
    public float HeldYaw { get; }
    public RigidBodySettings BodySettings { get; }
    public ItemCarrySettings Interaction { get; }

    public ItemDefinition(
        string id,
        string displayName,
        float radius = 0.12f,
        float height = 0.5f,
        Vector3? halfExtents = null,
        Vector3? spawnAngularVelocity = null,
        float heldYaw = 0f,
        RigidBodySettings? bodySettings = null,
        ItemCarrySettings? interaction = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An item definition ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("An item display name is required.", nameof(displayName));
        if (!float.IsFinite(radius) || radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!float.IsFinite(height) || height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(height));
        Vector3 dimensions = halfExtents ?? new Vector3(radius, height * 0.5f, radius);
        if (!Finite(dimensions) || dimensions.X <= 0f || dimensions.Y <= 0f || dimensions.Z <= 0f)
            throw new ArgumentOutOfRangeException(nameof(halfExtents));
        Vector3 initialSpin = spawnAngularVelocity ?? Vector3.Zero;
        if (!Finite(initialSpin))
            throw new ArgumentOutOfRangeException(nameof(spawnAngularVelocity));
        if (!float.IsFinite(heldYaw))
            throw new ArgumentOutOfRangeException(nameof(heldYaw));

        Id = id;
        DisplayName = displayName;
        Radius = radius;
        Height = height;
        HalfExtents = dimensions;
        SpawnAngularVelocity = initialSpin;
        HeldYaw = heldYaw;
        BodySettings = bodySettings ?? new RigidBodySettings();
        BodySettings.Validate();
        Interaction = interaction ?? new ItemCarrySettings();
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

// These values belong to the item type, independently of who carries it.
public sealed class ItemCarrySettings
{
    public Vector3 HeldOffset { get; }
    public float ThrowLift { get; }
    public Vector3 ThrowSpin { get; }

    public ItemCarrySettings(Vector3? heldOffset = null, float throwLift = 1.7f, Vector3? throwSpin = null)
    {
        Vector3 offset = heldOffset ?? new Vector3(0.38f, -0.55f, -0.9f);
        Vector3 spin = throwSpin ?? new Vector3(7f, 2f, 0.75f);
        if (!Finite(offset))
            throw new ArgumentOutOfRangeException(nameof(heldOffset));
        if (!float.IsFinite(throwLift))
            throw new ArgumentOutOfRangeException(nameof(throwLift));
        if (!Finite(spin))
            throw new ArgumentOutOfRangeException(nameof(throwSpin));
        HeldOffset = offset;
        ThrowLift = throwLift;
        ThrowSpin = spin;
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
