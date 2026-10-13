namespace Ars3D.Items;

public sealed class ItemInteractionSettings
{
    public float PickupDistance { get; }
    public float ThrowSpeed { get; }
    public float ReleaseClearance { get; }

    public ItemInteractionSettings(
        float pickupDistance = 2f,
        float throwSpeed = 7.5f,
        float releaseClearance = 0.001f)
    {
        if (!float.IsFinite(pickupDistance) || pickupDistance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(pickupDistance));
        if (!float.IsFinite(throwSpeed) || throwSpeed < 0f)
            throw new ArgumentOutOfRangeException(nameof(throwSpeed));
        if (!float.IsFinite(releaseClearance) || releaseClearance < 0f)
            throw new ArgumentOutOfRangeException(nameof(releaseClearance));

        PickupDistance = pickupDistance;
        ThrowSpeed = throwSpeed;
        ReleaseClearance = releaseClearance;
    }

}
