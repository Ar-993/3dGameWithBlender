using System;

namespace Ars3D.Items.Physics;

internal sealed class RestingState(RigidBodySettings settings)
{
    private float quietTime;
    public bool IsResting { get; private set; }

    public void Wake()
    {
        quietTime = 0f;
        IsResting = false;
    }

    public void Update(BodyMotion motion, float boundingRadius, bool supported, float seconds)
    {
        if (supported)
        {
            motion.AngularVelocity *= MathF.Exp(-settings.SupportAngularDamping * seconds);
            if (motion.Velocity.LengthSquared() < settings.QuietLinearSpeedSquared &&
                motion.AngularVelocity.LengthSquared() * boundingRadius * boundingRadius < settings.QuietSurfaceSpeedSquared)
            {
                motion.Velocity *= MathF.Exp(-settings.QuietLinearDamping * seconds);
                motion.AngularVelocity *= MathF.Exp(-settings.QuietAngularDamping * seconds);
            }
        }
        motion.AngularVelocity = motion.LimitAngularSpeed(motion.AngularVelocity);
        bool quiet = supported && motion.Velocity.LengthSquared() < settings.SleepLinearSpeed * settings.SleepLinearSpeed &&
            motion.AngularVelocity.LengthSquared() * boundingRadius * boundingRadius <
                settings.SleepSurfaceSpeed * settings.SleepSurfaceSpeed;
        quietTime = quiet ? quietTime + seconds : 0f;
        if (quietTime >= settings.SleepDelay)
        {
            motion.Velocity = System.Numerics.Vector3.Zero;
            motion.AngularVelocity = System.Numerics.Vector3.Zero;
            IsResting = true;
        }
    }
}
