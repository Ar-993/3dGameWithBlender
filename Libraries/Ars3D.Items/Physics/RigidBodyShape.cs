using System;
using System.Numerics;
using Ars3D.Items.Geometry;

namespace Ars3D.Items.Physics;

// Все части коллайдера движутся вокруг общего центра массы.
// Для предмета без заданных капсул сохраняется исходная сетка сфер.
internal sealed class RigidBodyShape
{
    public Vector3 HalfExtents { get; }
    public Vector3 LocalCenter { get; }
    public float BoundingRadius { get; }
    public ColliderCapsule[] Parts { get; }

    public RigidBodyShape(float radius, float height, Vector3? halfExtents, ItemCollider? collider)
    {
        if (!float.IsFinite(radius) || radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!float.IsFinite(height) || height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(height));
        HalfExtents = halfExtents ?? new Vector3(radius, height * 0.5f, radius);
        if (!RigidBody.Finite(HalfExtents) || HalfExtents.X <= 0f || HalfExtents.Y <= 0f || HalfExtents.Z <= 0f)
            throw new ArgumentOutOfRangeException(nameof(halfExtents));
        LocalCenter = Vector3.UnitY * (height * 0.5f);
        if (collider is null)
        {
            float sphereRadius = MathF.Min(HalfExtents.X, MathF.Min(HalfExtents.Y, HalfExtents.Z));
            Parts = CreateOffsets(HalfExtents, sphereRadius)
                .Select(offset => new ColliderCapsule(offset, offset, sphereRadius)).ToArray();
            BoundingRadius = HalfExtents.Length();
        }
        else
        {
            Parts = collider.Capsules.Select(part => part with
            {
                Start = part.Start - LocalCenter,
                End = part.End - LocalCenter
            }).ToArray();
            BoundingRadius = Parts.Max(part => MathF.Max(part.Start.Length(), part.End.Length()) + part.Radius);
        }
    }

    public Aabb GetBounds(Vector3 center, Quaternion orientation)
    {
        Vector3 minimum = new(float.MaxValue);
        Vector3 maximum = new(float.MinValue);
        foreach (ColliderCapsule part in Parts)
        {
            Vector3 extent = new(part.Radius);
            Vector3 start = center + Vector3.Transform(part.Start, orientation);
            Vector3 end = center + Vector3.Transform(part.End, orientation);
            minimum = Vector3.Min(minimum, Vector3.Min(start, end) - extent);
            maximum = Vector3.Max(maximum, Vector3.Max(start, end) + extent);
        }
        return new(minimum, maximum);
    }

    private static Vector3[] CreateOffsets(Vector3 halfExtents, float radius)
    {
        Vector3 interior = Vector3.Max(halfExtents - new Vector3(radius), Vector3.Zero);
        int countX = Count(interior.X);
        int countY = Count(interior.Y);
        int countZ = Count(interior.Z);
        var offsets = new Vector3[countX * countY * countZ];
        int index = 0;
        for (int x = 0; x < countX; x++)
        {
            for (int y = 0; y < countY; y++)
            {
                for (int z = 0; z < countZ; z++)
                {
                    offsets[index++] = new Vector3(
                        Coordinate(x, countX, interior.X),
                        Coordinate(y, countY, interior.Y),
                        Coordinate(z, countZ, interior.Z));
                }
            }
        }
        return offsets;

        int Count(float extent) =>
            extent < 0.000001f ? 1 : (int)MathF.Ceiling(2f * extent / (radius * 1.35f)) + 1;
        static float Coordinate(int index, int count, float extent) =>
            count == 1 ? 0f : -extent + 2f * extent * index / (count - 1);
    }
}