using Microsoft.Xna.Framework;

namespace _3DLight;

// Feet is the lowest point of the shape, Height includes both hemispheres.
internal readonly record struct CapsuleShape(Vector3 Feet, float Radius, float Height)
{
    public Vector3 AxisStart => Feet + Vector3.Up * Radius;
    public Vector3 AxisEnd => Feet + Vector3.Up * (Height - Radius);
    public BoundingBox Bounds => new(
        Feet - new Vector3(Radius, 0f, Radius),
        Feet + new Vector3(Radius, Height, Radius));
}

internal static class CapsuleCollision
{
    public const float SkinWidth = 0.001f;
    private const float DistanceTolerance = 0.00001f;
    private const float Tiny = 0.000000000001f;

    internal readonly record struct Contact(
        Vector3 AxisPoint, Vector3 SurfacePoint, Vector3 Normal, float Distance, float Penetration);

    internal readonly record struct Hit(float Fraction, Vector3 Normal, Level.TriangleCollider Triangle);

    public static Contact GetContact(CapsuleShape capsule, Level.TriangleCollider triangle)
    {
        ClosestSegmentTriangle(capsule.AxisStart, capsule.AxisEnd, triangle,
            out Vector3 axisPoint, out Vector3 surfacePoint);
        Vector3 difference = axisPoint - surfacePoint;
        float distance = difference.Length();
        if (distance > DistanceTolerance)
            return new(axisPoint, surfacePoint, difference / distance, distance,
                capsule.Radius + SkinWidth - distance);

        // Recovery from an intersecting axis: select the side containing its
        // midpoint, independent of winding. Move the entire axis out of the plane.
        Vector3 normal = triangle.Normal;
        Vector3 midpoint = (capsule.AxisStart + capsule.AxisEnd) * 0.5f;
        if (Vector3.Dot(midpoint - triangle.A, normal) < 0f)
            normal = -normal;
        float minimumSignedDistance = MathF.Min(
            Vector3.Dot(capsule.AxisStart - triangle.A, normal),
            Vector3.Dot(capsule.AxisEnd - triangle.A, normal));
        return new(axisPoint, surfacePoint, normal, distance,
            capsule.Radius + SkinWidth - minimumSignedDistance);
    }

    public static bool Cast(CapsuleShape capsule, Vector3 movement,
        IReadOnlyList<Level.TriangleCollider> triangles, out Hit hit)
    {
        hit = default;
        if (movement.LengthSquared() <= Tiny)
            return false;

        BoundingBox startBounds = capsule.Bounds;
        BoundingBox sweptBounds = new(
            Vector3.Min(startBounds.Min, startBounds.Min + movement) - new Vector3(SkinWidth),
            Vector3.Max(startBounds.Max, startBounds.Max + movement) + new Vector3(SkinWidth));
        float firstTime = 1f;
        float timeTolerance = DistanceTolerance / movement.Length();
        bool found = false;
        foreach (var triangle in triangles)
        {
            if (!Overlaps(sweptBounds, triangle.Bounds) ||
                !CastTriangle(capsule, movement, triangle, MathF.Min(1f, firstTime + timeTolerance), out Hit candidate))
                continue;

            // At a tread/riser seam both triangles can have the same contact.
            // Prefer the tread as support, without changing the earliest time.
            if (!found || candidate.Fraction < firstTime - timeTolerance ||
                (MathF.Abs(candidate.Fraction - firstTime) <= timeTolerance && candidate.Normal.Y > 0f &&
                 MathF.Abs(candidate.Triangle.Normal.Y) > MathF.Abs(hit.Triangle.Normal.Y)))
            {
                firstTime = found ? MathF.Min(firstTime, candidate.Fraction) : candidate.Fraction;
                hit = candidate with { Fraction = firstTime };
                found = true;
            }
        }
        return found;
    }

    private static bool CastTriangle(CapsuleShape capsule, Vector3 movement,
        Level.TriangleCollider triangle, float maximumTime, out Hit hit)
    {
        float time = 0f;
        Contact contact = default;
        for (int iteration = 0; iteration < 48; iteration++)
        {
            contact = GetContact(capsule with { Feet = capsule.Feet + movement * time }, triangle);
            float closingSpeed = -Vector3.Dot(movement, contact.Normal);
            // The closest features define a separating plane for these convex
            // shapes. A translation away from that plane cannot hit this triangle.
            if (closingSpeed <= 0.0000001f)
            {
                hit = default;
                return false;
            }

            float gap = contact.Distance - capsule.Radius - SkinWidth;
            if (gap <= DistanceTolerance)
            {
                hit = new(time, contact.Normal, triangle);
                return true;
            }

            // Conservative advancement to the separating plane. Unlike endpoint
            // overlap tests, this cannot step across a thin triangle between frames.
            float nextTime = time + gap / closingSpeed;
            if (nextTime > maximumTime)
            {
                hit = default;
                return false;
            }
            if (nextTime <= time)
                break;
            time = nextTime;
        }

        // Exhausting the numerical budget stops at the last safe position; it
        // must not be interpreted as an unobstructed path.
        hit = new(time, contact.Normal, triangle);
        return true;
    }

    public static bool Overlaps(BoundingBox a, BoundingBox b) =>
        a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
        a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
        a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;

    private static void ClosestSegmentTriangle(Vector3 start, Vector3 end,
        Level.TriangleCollider triangle, out Vector3 onSegment, out Vector3 onTriangle)
    {
        Vector3 direction = end - start;
        float denominator = Vector3.Dot(direction, triangle.Normal);
        if (MathF.Abs(denominator) > DistanceTolerance)
        {
            float time = Vector3.Dot(triangle.A - start, triangle.Normal) / denominator;
            Vector3 crossing = start + direction * time;
            if (time >= 0f && time <= 1f && IsInside(crossing, triangle))
            {
                onSegment = onTriangle = crossing;
                return;
            }
        }

        Vector3 bestAxis = start;
        Vector3 bestSurface = ClosestPointOnTriangle(start, triangle);
        float bestDistance = Vector3.DistanceSquared(bestAxis, bestSurface);
        Consider(end, ClosestPointOnTriangle(end, triangle));
        Edge(triangle.A, triangle.B);
        Edge(triangle.B, triangle.C);
        Edge(triangle.C, triangle.A);
        onSegment = bestAxis;
        onTriangle = bestSurface;

        void Consider(Vector3 axis, Vector3 surface)
        {
            float distance = Vector3.DistanceSquared(axis, surface);
            if (distance >= bestDistance)
                return;
            bestDistance = distance;
            bestAxis = axis;
            bestSurface = surface;
        }

        void Edge(Vector3 a, Vector3 b)
        {
            ClosestSegments(start, end, a, b, out Vector3 axis, out Vector3 edge);
            Consider(axis, edge);
        }
    }

    private static Vector3 ClosestPointOnTriangle(Vector3 point, Level.TriangleCollider triangle)
    {
        Vector3 projected = point - triangle.Normal * Vector3.Dot(point - triangle.A, triangle.Normal);
        if (IsInside(projected, triangle))
            return projected;

        Vector3 ab = ClosestPointOnSegment(point, triangle.A, triangle.B);
        Vector3 bc = ClosestPointOnSegment(point, triangle.B, triangle.C);
        Vector3 ca = ClosestPointOnSegment(point, triangle.C, triangle.A);
        Vector3 closest = Vector3.DistanceSquared(point, ab) <= Vector3.DistanceSquared(point, bc) ? ab : bc;
        return Vector3.DistanceSquared(point, closest) <= Vector3.DistanceSquared(point, ca) ? closest : ca;
    }

    private static bool IsInside(Vector3 point, Level.TriangleCollider triangle)
    {
        // Oriented edge tests in the actual 3D plane, including vertical faces.
        const float tolerance = 0.000001f;
        return Vector3.Dot(Vector3.Cross(triangle.B - triangle.A, point - triangle.A), triangle.Normal) >= -tolerance &&
            Vector3.Dot(Vector3.Cross(triangle.C - triangle.B, point - triangle.B), triangle.Normal) >= -tolerance &&
            Vector3.Dot(Vector3.Cross(triangle.A - triangle.C, point - triangle.C), triangle.Normal) >= -tolerance;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 edge = b - a;
        float lengthSquared = edge.LengthSquared();
        return lengthSquared <= Tiny ? a :
            a + edge * MathHelper.Clamp(Vector3.Dot(point - a, edge) / lengthSquared, 0f, 1f);
    }

    private static void ClosestSegments(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1,
        out Vector3 onA, out Vector3 onB)
    {
        Vector3 a = a1 - a0;
        Vector3 b = b1 - b0;
        Vector3 offset = a0 - b0;
        float aa = Vector3.Dot(a, a);
        float bb = Vector3.Dot(b, b);
        float bo = Vector3.Dot(b, offset);
        float s, t;
        if (aa <= Tiny && bb <= Tiny)
        {
            onA = a0;
            onB = b0;
            return;
        }
        if (aa <= Tiny)
        {
            s = 0f;
            t = MathHelper.Clamp(bo / bb, 0f, 1f);
        }
        else
        {
            float ao = Vector3.Dot(a, offset);
            if (bb <= Tiny)
            {
                t = 0f;
                s = MathHelper.Clamp(-ao / aa, 0f, 1f);
            }
            else
            {
                float ab = Vector3.Dot(a, b);
                float denominator = aa * bb - ab * ab;
                s = denominator > Tiny ? MathHelper.Clamp((ab * bo - ao * bb) / denominator, 0f, 1f) : 0f;
                t = (ab * s + bo) / bb;
                if (t < 0f)
                {
                    t = 0f;
                    s = MathHelper.Clamp(-ao / aa, 0f, 1f);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = MathHelper.Clamp((ab - ao) / aa, 0f, 1f);
                }
            }
        }
        onA = a0 + a * s;
        onB = b0 + b * t;
    }
}
