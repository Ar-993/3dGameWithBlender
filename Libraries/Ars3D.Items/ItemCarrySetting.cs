using System.Numerics;

namespace Ars3D.Items;

public sealed class ItemCarrySettings
{
    public Vector3 HeldOffset { get; }
    public float ThrowLift { get; }
    public Vector3 ThrowSpin { get; }

    public ItemCarrySettings(
        Vector3? heldOffset = null,
        float throwLift = 1.7f,
        Vector3? throwSpin = null)
    {
        HeldOffset = heldOffset ?? new Vector3(0.38f, -0.55f, -0.9f);
        ThrowLift = throwLift;
        ThrowSpin = throwSpin ?? new Vector3(7f, 2f, 0.75f);
    }
}
