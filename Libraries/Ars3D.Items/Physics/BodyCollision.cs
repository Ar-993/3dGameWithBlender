using System.Collections.Generic;
using System.Numerics;

namespace Ars3D.Items.Physics;

// Traversal advances one common pose to the earliest sphere contact. Geometry,
// continuous casts and coplanar seam handling remain the host world's job.
internal sealed class BodyCollision(RoundedBoxShape shape, RigidBody body,
    ContactSolver solver, RigidBodySettings settings)
{
    private List<BodyContact> Contacts { get; } = [];

    public void Gather(ICollisionWorld world)
    {
        Contacts.Clear();
        foreach (Vector3 offset in shape.SphereOffsets)
            world.GetSphereContacts(body.Center + Vector3.Transform(offset, body.Orientation),
                shape.SphereRadius, Contacts);
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
        foreach (Vector3 localOffset in shape.SphereOffsets)
        {
            Vector3 offset = Vector3.Transform(localOffset, body.Orientation);
            Vector3 start = body.Center + offset;
            Vector3 end = targetCenter + Vector3.Transform(localOffset, targetOrientation);
            if (world.SweepSphere(start, end - start, shape.SphereRadius, body.PointVelocity(offset), out var hit) &&
                (!found || hit.Fraction < fraction))
            {
                fraction = hit.Fraction;
                firstHit = hit;
                found = true;
            }
        }
        return found;
    }

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
