using System.Numerics;

namespace Ars3D.Items;

// One hidden slot. Rendering and UI are responsibilities of the calling game.
public sealed class SingleSlotInventory
{
    public ItemInstance? HeldItem { get; private set; }

    public bool TryPickUp(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (HeldItem is not null || item.IsHeld)
            return false;
        item.PickUp();
        HeldItem = item;
        return true;
    }

    public ItemInstance? Release(ItemPose pose, Vector3 velocity, Vector3 angularVelocity)
    {
        ItemInstance? item = HeldItem;
        if (item is null)
            return null;
        item.Release(pose.Position, velocity, pose.Orientation, angularVelocity);
        HeldItem = null;
        return item;
    }
}
