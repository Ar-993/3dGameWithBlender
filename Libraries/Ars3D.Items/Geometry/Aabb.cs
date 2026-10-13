using System.Numerics;

namespace Ars3D.Items.Geometry;

/// <summary>Axis-aligned bounds used by item physics and interaction.</summary>
public readonly record struct Aabb(Vector3 Min, Vector3 Max)
{
    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Size => Max - Min;
    public Vector3 ClosestPoint(Vector3 point) => Vector3.Clamp(point, Min, Max);
    public bool Contains(Vector3 point) =>
        point.X >= Min.X && point.X <= Max.X &&
        point.Y >= Min.Y && point.Y <= Max.Y &&
        point.Z >= Min.Z && point.Z <= Max.Z;
}
