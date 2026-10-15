using System;
using System.Numerics;
using Ars3D.Items.Geometry;

namespace Ars3D.Items.Physics;

// Overlapping spheres approximate one solid rounded box. Sphere offsets are
// relative to the center of mass, never independent moving bodies.
internal sealed class RoundedBoxShape
{
    public Vector3 HalfExtents { get; }
    public Vector3 LocalCenter { get; }
    public float SphereRadius { get; }
    public float BoundingRadius => HalfExtents.Length();
    public Vector3[] SphereOffsets { get; }

    public RoundedBoxShape(float radius, float height, Vector3? halfExtents)
    {
        if (!float.IsFinite(radius) || radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!float.IsFinite(height) || height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(height));
        HalfExtents = halfExtents ?? new Vector3(radius, height * 0.5f, radius);
        if (!RigidBody.Finite(HalfExtents) || HalfExtents.X <= 0f || HalfExtents.Y <= 0f || HalfExtents.Z <= 0f)
            throw new ArgumentOutOfRangeException(nameof(halfExtents));
        LocalCenter = Vector3.UnitY * (height * 0.5f);
        SphereRadius = MathF.Min(HalfExtents.X, MathF.Min(HalfExtents.Y, HalfExtents.Z));
        SphereOffsets = CreateOffsets(HalfExtents, SphereRadius);
    }

    public Aabb GetBounds(Vector3 center, Quaternion orientation)
    {
        Vector3 minimum = new(float.MaxValue);
        Vector3 maximum = new(float.MinValue);
        Vector3 extent = new(SphereRadius);
        foreach (Vector3 offset in SphereOffsets)
        {
            Vector3 point = center + Vector3.Transform(offset, orientation);
            minimum = Vector3.Min(minimum, point - extent);
            maximum = Vector3.Max(maximum, point + extent);
        }
        return new(minimum, maximum);
    }

    private static Vector3[] CreateOffsets(Vector3 halfExtents, float radius)
    {
        Vector3 interior = Vector3.Max(halfExtents - new Vector3(radius), Vector3.Zero);
        int countX = Count(interior.X), countY = Count(interior.Y), countZ = Count(interior.Z);
        var offsets = new Vector3[countX * countY * countZ];
        int index = 0;
        for (int x = 0; x < countX; x++)
        for (int y = 0; y < countY; y++)
        for (int z = 0; z < countZ; z++)
            offsets[index++] = new(Coordinate(x, countX, interior.X),
                Coordinate(y, countY, interior.Y), Coordinate(z, countZ, interior.Z));
        return offsets;

        int Count(float extent) => extent < 0.000001f ? 1 : (int)MathF.Ceiling(2f * extent / (radius * 1.35f)) + 1;
        static float Coordinate(int index, int count, float extent) =>
            count == 1 ? 0f : -extent + 2f * extent * index / (count - 1);
    }
}
