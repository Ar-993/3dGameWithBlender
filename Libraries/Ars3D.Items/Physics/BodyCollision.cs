using System.Collections.Generic;
using System.Numerics;

namespace Ars3D.Items.Physics;

// Traversal advances one common pose to the earliest sphere contact. Geometry,
// continuous casts and coplanar seam handling remain the host world's job.
internal sealed class BodyCollision(RoundedBoxShape shape, BodyMotion motion,
    ContactSolver solver, RigidBodySettings settings)
{
    public List<BodyContact> Contacts { get; } = [];

    public void Gather(ICollisionWorld world)
    {
        Contacts.Clear();
        foreach (Vector3 offset in shape.SphereOffsets)
            world.GetSphereContacts(motion.Center + Vector3.Transform(offset, motion.Orientation),
                shape.SphereRadius, Contacts);
    }

    public void Move(ICollisionWorld world, float seconds)
    {
        float remaining = seconds;
        for (int impact = 0; impact < settings.MaximumImpacts && remaining > 0.000001f; impact++)
        {
            float angularSpeed = motion.AngularVelocity.Length();
            float interval = angularSpeed > 0.0001f
                ? System.MathF.Min(remaining, settings.MaximumAngularStep / angularSpeed) : remaining;
            Vector3 targetCenter = motion.Center + motion.Velocity * interval;
            Quaternion targetOrientation = motion.PredictOrientation(interval);
            bool hitAnything = FindFirstHit(world, targetCenter, targetOrientation, out SweepHit firstHit);
            float fraction = hitAnything ? firstHit.Fraction : 1f;
            motion.Center = Vector3.Lerp(motion.Center, targetCenter, fraction);
            motion.Orientation = Quaternion.Normalize(Quaternion.Slerp(motion.Orientation, targetOrientation, fraction));
            remaining -= interval * fraction;
            if (!hitAnything)
                continue;
            Gather(world);
            Contacts.Add(firstHit.Contact);
            solver.Solve(motion, Contacts, allowBounce: true);
        }
    }

    private bool FindFirstHit(ICollisionWorld world, Vector3 targetCenter,
        Quaternion targetOrientation, out SweepHit firstHit)
    {
        firstHit = default;
        float fraction = 1f;
        bool found = false;
        foreach (Vector3 localOffset in shape.SphereOffsets)
        {
            Vector3 offset = Vector3.Transform(localOffset, motion.Orientation);
            Vector3 start = motion.Center + offset;
            Vector3 end = targetCenter + Vector3.Transform(localOffset, targetOrientation);
            if (world.SweepSphere(start, end - start, shape.SphereRadius, motion.PointVelocity(offset), out var hit) &&
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
            motion.Center += deepest.Normal * deepest.Penetration;
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
