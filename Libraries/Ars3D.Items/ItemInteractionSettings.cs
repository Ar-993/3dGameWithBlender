using System.Numerics;

namespace Ars3D.Items;

public sealed class ItemInteractionSettings
{
    public Vector3 HeldOffset { get; }
    public float PickupDistance { get; }
    public float ThrowSpeed { get; }
    public float ThrowLift { get; }
    // Spin components along camera right, camera up and the look direction.
    public Vector3 ThrowSpin { get; }
    public float ReleaseClearance { get; }

    public ItemInteractionSettings(
        Vector3? heldOffset = null,
        float pickupDistance = 2f,
        float throwSpeed = 7.5f,
        float throwLift = 1.7f,
        Vector3? throwSpin = null,
        float releaseClearance = 0.001f)
    {
        Vector3 offset = heldOffset ?? new Vector3(0.38f, -0.55f, -0.9f);
        Vector3 spin = throwSpin ?? new Vector3(7f, 2f, 0.75f);
        if (!Finite(offset))
            throw new ArgumentOutOfRangeException(nameof(heldOffset));
        if (!float.IsFinite(pickupDistance) || pickupDistance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(pickupDistance));
        if (!float.IsFinite(throwSpeed) || throwSpeed < 0f)
            throw new ArgumentOutOfRangeException(nameof(throwSpeed));
        if (!float.IsFinite(throwLift))
            throw new ArgumentOutOfRangeException(nameof(throwLift));
        if (!Finite(spin))
            throw new ArgumentOutOfRangeException(nameof(throwSpin));
        if (!float.IsFinite(releaseClearance) || releaseClearance < 0f)
            throw new ArgumentOutOfRangeException(nameof(releaseClearance));

        HeldOffset = offset;
        PickupDistance = pickupDistance;
        ThrowSpeed = throwSpeed;
        ThrowLift = throwLift;
        ThrowSpin = spin;
        ReleaseClearance = releaseClearance;
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
