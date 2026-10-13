using System.Numerics;

namespace Ars3D.Items;

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
