using System.Numerics;

namespace Ars3D.Items;

internal static class ItemReachQuery
{
    public static ItemInstance? FindNearest(
        IReadOnlyList<ItemInstance> items,
        IItemInteractionWorld world,
        Vector3 playerFeet,
        Vector3 eyePosition,
        float reach)
    {
        ItemInstance? nearest = null;
        float nearestDistance = reach;
        foreach (ItemInstance item in items)
        {
            if (item.IsHeld)
                continue;
            float distance = Vector3.Distance(eyePosition, item.Bounds.ClosestPoint(eyePosition));
            if (distance > nearestDistance || item.Center.Y < playerFeet.Y - reach ||
                !world.HasLineOfSight(eyePosition, item.Center))
            {
                continue;
            }

            nearest = item;
            nearestDistance = distance;
        }
        return nearest;
    }
}
