using System.Numerics;

namespace Ars3D.Items;

internal static class ItemRelease
{
    public static void Apply(
        SingleSlotInventory inventory,
        IItemInteractionWorld world,
        Vector3 eyePosition,
        Vector3 forward,
        ItemInteractionSettings settings,
        bool throwItem)
    {
        ItemInstance? item = inventory.HeldItem;
        if (item is null)
            return;
        HeldItemPlacement.Frame frame = HeldItemPlacement.CreateFrame(forward);
        ItemPose heldPose = HeldItemPlacement.CreatePose(item, eyePosition, frame);
        ItemCarrySettings carry = item.Definition.Interaction;
        Vector3 centerOffset = Vector3.Transform(item.LocalCenter, heldPose.Orientation);
        Vector3 sourceCenter = eyePosition - item.LocalCenter;
        Vector3 desiredCenter = heldPose.Position + centerOffset;
        float radius = item.BoundingRadius + settings.ReleaseClearance;
        Vector3 releaseCenter = world.ResolveSpherePlacement(sourceCenter, desiredCenter, radius);
        var releasePose = heldPose with { Position = releaseCenter - centerOffset };
        Vector3 velocity = throwItem
            ? frame.Forward * settings.ThrowSpeed + Vector3.UnitY * carry.ThrowLift
            : Vector3.Zero;
        Vector3 spin = throwItem
            ? frame.Right * carry.ThrowSpin.X + frame.Up * carry.ThrowSpin.Y + frame.Forward * carry.ThrowSpin.Z
            : Vector3.Zero;
        inventory.Release(releasePose, velocity, spin);
    }
}
