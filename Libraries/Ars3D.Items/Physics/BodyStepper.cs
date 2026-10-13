using System;

namespace Ars3D.Items.Physics;

internal sealed class BodyStepper(RoundedBoxShape shape, BodyMotion motion, ContactSolver solver,
    BodyCollision collision, RestingState resting, RigidBodySettings settings)
{
    public void Update(ICollisionWorld world, float seconds)
    {
        if (!float.IsFinite(seconds) || seconds <= 0f)
            return;
        if (resting.IsResting)
        {
            collision.Gather(world);
            if (collision.HasSupport() && collision.MaximumPenetration() <= settings.WakePenetration)
                return;
            resting.Wake();
        }
        float remaining = MathF.Min(seconds, settings.MaximumUpdateTime);
        while (remaining > 0.000001f)
        {
            float angularSpeed = motion.AngularVelocity.Length();
            float step = MathF.Min(remaining, settings.MaximumStep);
            if (angularSpeed > 0.0001f)
                step = MathF.Min(step, settings.MaximumAngularStep / angularSpeed);
            Step(world, step);
            remaining -= step;
            if (resting.IsResting)
                break;
        }
    }

    private void Step(ICollisionWorld world, float seconds)
    {
        motion.ApplyForces(seconds);
        collision.Gather(world);
        solver.Solve(motion, collision.Contacts, allowBounce: false);
        collision.Move(world, seconds);
        collision.RecoverOverlaps(world);
        collision.Gather(world);
        solver.Solve(motion, collision.Contacts, allowBounce: false);
        resting.Update(motion, shape.BoundingRadius, collision.HasSupport(), seconds);
    }
}
