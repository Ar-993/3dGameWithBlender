using Ars3D.Items.Physics.Collision;
using System.Collections.Generic;
using System.Numerics;

namespace Ars3D.Items.Physics;

// Traversal advances one common pose to the earliest collider contact. Geometry,
// continuous casts and coplanar seam handling remain the host world's job.
internal sealed class BodyCollision(RigidBodyShape shape, RigidBody body,
    ContactSolver solver, RigidBodySettings settings)
{
    private List<BodyContact> Contacts { get; } = [];

    public void Gather(ICollisionWorld world)
    {
        Contacts.Clear();
        foreach (ColliderCapsule part in shape.Parts)
        {
            Vector3 start = body.Center + Vector3.Transform(part.Start, body.Orientation);
            if (part.Start == part.End)
            {
                world.GetSphereContacts(start, part.Radius, Contacts);
                continue;
            }

            Vector3 end = body.Center + Vector3.Transform(part.End, body.Orientation);
            if (world is ICapsuleCollisionWorld capsules)
                capsules.GetCapsuleContacts(start, end, part.Radius, Contacts);
            else
            {
                int samples = FallbackSampleCount(part);
                for (int index = 0; index <= samples; index++)
                    world.GetSphereContacts(Vector3.Lerp(start, end, index / (float)samples),
                        part.Radius, Contacts);
            }
        }
    }

    public void SolveContacts(ICollisionWorld world)
    {
        Gather(world);
        solver.Solve(body, Contacts, allowBounce: false);
    }

    public void Move(ICollisionWorld world, float seconds)
    {
        float remaining = seconds;
        for (int step = 0; step < settings.MaximumImpacts && remaining > 0.000001f; step++)
        {
            float stepSeconds = body.LimitStepByRotation(remaining);
            remaining -= MoveStep(world, stepSeconds);
        }
    }

    private float MoveStep(ICollisionWorld world, float seconds)
    {
        Vector3 targetCenter = body.Center + body.Velocity * seconds;
        Quaternion targetOrientation = body.PredictOrientation(seconds);
        bool hitAnything = FindFirstHit(world, targetCenter, targetOrientation, out SweepHit firstHit);
        float fraction = hitAnything ? firstHit.Fraction : 1f;

        MoveToPose(targetCenter, targetOrientation, fraction);
        if (hitAnything)
            ResolveImpact(world, firstHit.Contact);

        return seconds * fraction;
    }

    private void MoveToPose(Vector3 targetCenter, Quaternion targetOrientation, float fraction)
    {
        body.Center = Vector3.Lerp(body.Center, targetCenter, fraction);
        body.Orientation = Quaternion.Normalize(
            Quaternion.Slerp(body.Orientation, targetOrientation, fraction));
    }

    private void ResolveImpact(ICollisionWorld world, BodyContact contact)
    {
        Gather(world);
        Contacts.Add(contact);
        solver.Solve(body, Contacts, allowBounce: true);
    }

    private bool FindFirstHit(ICollisionWorld world, Vector3 targetCenter,
        Quaternion targetOrientation, out SweepHit firstHit)
    {
        firstHit = default;
        float fraction = 1f;
        bool found = false;
        foreach (ColliderCapsule part in shape.Parts)
        {
            if (SweepPart(world, part, targetCenter, targetOrientation, out var hit) &&
                (!found || hit.Fraction < fraction))
            {
                fraction = hit.Fraction;
                firstHit = hit;
                found = true;
            }
        }
        return found;
    }

    private bool SweepPart(ICollisionWorld world, ColliderCapsule part, Vector3 targetCenter,
        Quaternion targetOrientation, out SweepHit firstHit)
    {
        Vector3 startOffset = Vector3.Transform(part.Start, body.Orientation);
        Vector3 start = body.Center + startOffset;
        Vector3 startMovement = targetCenter + Vector3.Transform(part.Start, targetOrientation) - start;
        if (part.Start == part.End)
            return world.SweepSphere(start, startMovement, part.Radius, body.PointVelocity(startOffset), out firstHit);

        Vector3 endOffset = Vector3.Transform(part.End, body.Orientation);
        Vector3 end = body.Center + endOffset;
        Vector3 endMovement = targetCenter + Vector3.Transform(part.End, targetOrientation) - end;
        if (world is ICapsuleCollisionWorld capsules)
            return capsules.SweepCapsule(start, end, startMovement, endMovement, part.Radius,
                body.PointVelocity(startOffset), body.PointVelocity(endOffset), out firstHit);

        // Older hosts can still use custom colliders through their sphere queries.
        firstHit = default;
        bool found = false;
        int samples = FallbackSampleCount(part);
        for (int index = 0; index <= samples; index++)
        {
            float amount = index / (float)samples;
            if (world.SweepSphere(Vector3.Lerp(start, end, amount),
                Vector3.Lerp(startMovement, endMovement, amount), part.Radius,
                body.PointVelocity(Vector3.Lerp(startOffset, endOffset, amount)), out var hit) &&
                (!found || hit.Fraction < firstHit.Fraction))
            {
                found = true;
                firstHit = hit;
            }
        }
        return found;
    }

    private static int FallbackSampleCount(ColliderCapsule part) =>
        Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(part.Start, part.End) / (part.Radius * 1.35f)));

    public void RecoverOverlaps(ICollisionWorld world)
    {
        for (int iteration = 0; iteration < settings.RecoveryIterations; iteration++)
        {
            Gather(world);
            BodyContact deepest = default;
            foreach (BodyContact contact in Contacts)
                if (contact.Penetration > deepest.Penetration)
                    deepest = contact;
            if (deepest.Penetration <= settings.RecoveryTolerance)
                break;
            body.Center += deepest.Normal * deepest.Penetration;
        }
    }

    public bool HasSupport()
    {
        foreach (BodyContact contact in Contacts)
            if (contact.Normal.Y > settings.MinimumSupportNormalY)
                return true;
        return false;
    }

    public float MaximumPenetration()
    {
        float deepest = 0f;
        foreach (BodyContact contact in Contacts)
            deepest = System.MathF.Max(deepest, contact.Penetration);
        return deepest;
    }
}