using System;
using System.Collections.Generic;
using System.Linq;
using _3DLight.Collision;
using Microsoft.Xna.Framework;

namespace _3DLight;

public partial class Level
{
    private const float GroundProbeDistance = 0.025f;
    private const int MaximumSlideIterations = 8;

    private IReadOnlyList<TriangleCollider> GetCollisionCandidates(
        BoundingBox bounds)
    {
        if (collisionIndex is null ||
            collisionIndex.SourceCount != meshColliders.Count)
        {
            collisionIndex = new TriangleSpatialIndex(meshColliders);
        }

        collisionIndex.Query(
            bounds,
            collisionCandidateIndices);

        collisionCandidates.Clear();

        foreach (int index in collisionCandidateIndices)
            collisionCandidates.Add(collisionIndex[index]);

        return collisionCandidates;
    }

    private bool CastCapsule(
        CapsuleShape capsule,
        Vector3 movement,
        out CapsuleCollision.Hit hit)
    {
        if (movement.LengthSquared() <= 0.000000000001f)
        {
            hit = default;
            return false;
        }

        BoundingBox startBounds = capsule.Bounds;

        BoundingBox sweptBounds = new(
            Vector3.Min(
                startBounds.Min,
                startBounds.Min + movement)
                - new Vector3(CapsuleCollision.SkinWidth),

            Vector3.Max(
                startBounds.Max,
                startBounds.Max + movement)
                + new Vector3(CapsuleCollision.SkinWidth));

        return CapsuleCollision.Cast(
            capsule,
            movement,
            GetCollisionCandidates(sweptBounds),
            out hit);
    }

    public Vector3 MoveCharacter(
        Vector3 position,
        Vector3 movement,
        float radius,
        float height,
        Platform? currentPlatform,
        ref float verticalVelocity,
        out Platform? groundPlatform)
    {
        if (!float.IsFinite(radius) || radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));

        if (!float.IsFinite(height) || height < 2f * radius)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                "Capsule height must include both hemispheres.");
        }

        groundPlatform = null;

        var capsule = RecoverPenetration(
            new CapsuleShape(position, radius, height));

        bool canUseSteps =
            currentPlatform is not null && movement.Y <= 0f;

        Vector3 horizontalMovement =
            new(movement.X, 0f, movement.Z);

        SlideResult horizontal = MoveCapsule(
            capsule,
            horizontalMovement,
            horizontalPass: true);

        if (canUseSteps &&
            horizontal.HitWall &&
            TryCapsuleStep(
                capsule,
                horizontalMovement,
                horizontal.Position,
                out Vector3 stepped))
        {
            horizontal = horizontal with
            {
                Position = stepped
            };
        }

        capsule = capsule with
        {
            Feet = horizontal.Position
        };

        SlideResult vertical = MoveCapsule(
            capsule,
            new Vector3(0f, movement.Y, 0f),
            horizontalPass: false);

        capsule = capsule with
        {
            Feet = vertical.Position
        };

        if (movement.Y > 0f && vertical.HitCeiling)
            verticalVelocity = 0f;

        if (movement.Y <= 0f)
        {
            float probeDistance = canUseSteps
                ? MaximumStepHeight + GroundProbeDistance
                : GroundProbeDistance;

            Vector3 probe = Vector3.Down * probeDistance;

            if (CastCapsule(capsule, probe, out var groundHit) &&
                IsWalkable(groundHit))
            {
                capsule = capsule with
                {
                    Feet = capsule.Feet +
                           probe * groundHit.Fraction
                };

                groundPlatform = GetSupport(groundHit.Triangle);
                verticalVelocity = 0f;
            }
        }

        return capsule.Feet;
    }

    private CapsuleShape RecoverPenetration(
        CapsuleShape capsule)
    {
        for (int iteration = 0; iteration < 12; iteration++)
        {
            float deepest = 0.00001f;
            Vector3 normal = Vector3.Zero;

            BoundingBox bounds = capsule.Bounds;
            bounds.Min -= new Vector3(CapsuleCollision.SkinWidth);
            bounds.Max += new Vector3(CapsuleCollision.SkinWidth);

            foreach (TriangleCollider triangle in
                     GetCollisionCandidates(bounds))
            {
                if (!CapsuleCollision.Overlaps(
                        bounds,
                        triangle.Bounds))
                {
                    continue;
                }

                var contact = CapsuleCollision.GetContact(
                    capsule,
                    triangle);

                if (contact.Penetration <= deepest)
                    continue;

                deepest = contact.Penetration;
                normal = contact.Normal;
            }

            if (normal == Vector3.Zero)
                break;

            capsule = capsule with
            {
                Feet = capsule.Feet + normal * deepest
            };
        }

        return capsule;
    }

    private readonly record struct SlideResult(
        Vector3 Position,
        bool HitWall,
        bool HitCeiling);

    private SlideResult MoveCapsule(
        CapsuleShape capsule,
        Vector3 movement,
        bool horizontalPass)
    {
        Vector3 remaining = movement;

        bool hitWall = false;
        bool hitCeiling = false;

        Span<Vector3> planes =
            stackalloc Vector3[MaximumSlideIterations];

        int planeCount = 0;

        for (int iteration = 0;
             iteration < MaximumSlideIterations &&
             remaining.LengthSquared() > 0.0000000001f;
             iteration++)
        {
            if (!CastCapsule(capsule, remaining, out var hit))
            {
                capsule = capsule with
                {
                    Feet = capsule.Feet + remaining
                };

                break;
            }

            capsule = capsule with
            {
                Feet = capsule.Feet +
                       remaining * hit.Fraction
            };

            remaining *= 1f - hit.Fraction;

            bool walkable = IsWalkable(hit);

            hitWall |=
                (!walkable ||
                 hit.Normal.Y < MinimumGroundNormalY) &&
                hit.Normal.Y > -MinimumGroundNormalY;

            hitCeiling |= hit.Normal.Y < -0.01f;

            if (!horizontalPass &&
                ((movement.Y < 0f && walkable) ||
                 (movement.Y > 0f && hit.Normal.Y < -0.01f)))
            {
                break;
            }

            Vector3 normal = hit.Normal;

            if (horizontalPass && !walkable && normal.Y > 0f)
            {
                normal.Y = 0f;

                if (normal.LengthSquared() < 0.00000001f)
                    break;

                normal.Normalize();
            }

            planes[planeCount++] = normal;

            remaining = ClipAgainstPlanes(
                remaining,
                planes[..planeCount]);
        }

        return new SlideResult(
            capsule.Feet,
            hitWall,
            hitCeiling);
    }

    private static Vector3 ClipAgainstPlanes(
        Vector3 movement,
        ReadOnlySpan<Vector3> planes)
    {
        for (int pass = 0; pass < planes.Length + 1; pass++)
        {
            foreach (Vector3 normal in planes)
            {
                float intoSurface =
                    Vector3.Dot(movement, normal);

                if (intoSurface < 0f)
                    movement -= normal * intoSurface;
            }
        }

        foreach (Vector3 normal in planes)
        {
            if (Vector3.Dot(movement, normal) < -0.00001f)
                return Vector3.Zero;
        }

        return movement;
    }

    private bool TryCapsuleStep(
        CapsuleShape capsule,
        Vector3 movement,
        Vector3 blockedPosition,
        out Vector3 result)
    {
        result = blockedPosition;

        if (movement.LengthSquared() < 0.00000001f)
            return false;

        Vector3 up = Vector3.Up * MaximumStepHeight;

        if (CastCapsule(capsule, up, out var ceiling))
            up *= ceiling.Fraction;

        if (up.Y <= CapsuleCollision.SkinWidth)
            return false;

        CapsuleShape raised = capsule with
        {
            Feet = capsule.Feet + up
        };

        SlideResult forward = MoveCapsule(
            raised,
            movement,
            horizontalPass: true);

        Vector3 direction = Vector3.Normalize(movement);

        float oldProgress = Vector3.Dot(
            blockedPosition - capsule.Feet,
            direction);

        float stepProgress = Vector3.Dot(
            forward.Position - capsule.Feet,
            direction);

        if (stepProgress <= oldProgress + 0.0001f)
            return false;

        raised = raised with
        {
            Feet = forward.Position
        };

        float dropDistance =
            raised.Feet.Y -
            capsule.Feet.Y +
            GroundProbeDistance;

        if (dropDistance <= 0f)
            return false;

        Vector3 down = Vector3.Down * dropDistance;

        if (!CastCapsule(raised, down, out var landing) ||
            !IsWalkable(landing))
        {
            return false;
        }

        Vector3 destination =
            raised.Feet + down * landing.Fraction;

        if (destination.Y >
            capsule.Feet.Y +
            MaximumStepHeight +
            CapsuleCollision.SkinWidth)
        {
            return false;
        }

        if (destination.Y <
            capsule.Feet.Y - GroundProbeDistance)
        {
            return false;
        }

        result = destination;
        return true;
    }

    private static bool IsWalkable(
        CapsuleCollision.Hit hit) =>
        hit.Normal.Y > 0.01f &&
        MathF.Abs(hit.Triangle.Normal.Y) >= MinimumGroundNormalY;

    private Platform GetSupport(TriangleCollider triangle) =>
        triangle.SupportPlatform ??
        platforms.FirstOrDefault(
            platform => platform.Name == triangle.Name) ??
        new Platform(
            meshColliders.IndexOf(triangle),
            triangle.Name,
            triangle.Bounds);
}