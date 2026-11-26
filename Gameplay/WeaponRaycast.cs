using _3DLight.Items;
using Microsoft.Xna.Framework;

namespace _3DLight.Gameplay
{
    internal readonly record struct WeaponHit(Vector3 Position, bool HitTarget);

    internal static class WeaponRaycast
    {
        public static WeaponHit Cast(Level level, Vector3 origin, Vector3 direction,
            float range, BoundingBox? target)
        {
            if (direction.LengthSquared() <= 0.00000001f || range <= 0f)
                return new(origin, false);
            direction.Normalize();
            float distance = range;
            bool targetHit = false;
            if (level.ItemWorld.SweepSphere(ItemMath.ToLibrary(origin),
                    ItemMath.ToLibrary(direction * range), 0.005f, ItemMath.ToLibrary(direction), out var wall))
                distance = range * wall.Fraction;
            if (target is { } bounds && new Ray(origin, direction).Intersects(bounds) is { } targetDistance &&
                targetDistance < distance)
            {
                distance = targetDistance;
                targetHit = true;
            }
            return new(origin + direction * distance, targetHit);
        }
    }
}
