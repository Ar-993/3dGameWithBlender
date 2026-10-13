using System;
using System.Numerics;

namespace Ars3D.Items.Physics;

// Mutable center-of-mass state and unit-mass box inertia. The public facade
// exposes this state read-only; collision impulses are the only solver writes.
internal sealed class BodyMotion
{
    private readonly RoundedBoxShape shape;
    private readonly RigidBodySettings settings;
    private readonly Vector3 inverseInertia;
    public Vector3 Center { get; set; }
    public Vector3 Velocity { get; set; }
    public Quaternion Orientation { get; set; } = Quaternion.Identity;
    public Vector3 AngularVelocity { get; set; }
    public Vector3 BottomPosition => Center - Vector3.Transform(shape.LocalCenter, Orientation);

    public BodyMotion(RoundedBoxShape shape, RigidBodySettings settings)
    {
        this.shape = shape;
        this.settings = settings;
        Vector3 half = shape.HalfExtents;
        inverseInertia = new(3f / (half.Y * half.Y + half.Z * half.Z),
            3f / (half.X * half.X + half.Z * half.Z),
            3f / (half.X * half.X + half.Y * half.Y));
    }

    public void Reset(Vector3 bottomPosition, Vector3 velocity, Quaternion orientation, Vector3 angularVelocity)
    {
        if (!Finite(bottomPosition) || !Finite(velocity) || !Finite(angularVelocity) ||
            !float.IsFinite(orientation.X) || !float.IsFinite(orientation.Y) ||
            !float.IsFinite(orientation.Z) || !float.IsFinite(orientation.W) ||
            orientation.LengthSquared() < 0.000001f)
            throw new ArgumentException("Rigid item pose and velocities must be finite.");
        Orientation = Quaternion.Normalize(orientation);
        Center = bottomPosition + Vector3.Transform(shape.LocalCenter, Orientation);
        Velocity = velocity;
        AngularVelocity = LimitAngularSpeed(angularVelocity);
    }

    public void ApplyForces(float seconds)
    {
        Velocity = (Velocity - Vector3.UnitY * (settings.Gravity * seconds)) *
            MathF.Exp(-settings.AirLinearDamping * seconds);
        AngularVelocity *= MathF.Exp(-settings.AirAngularDamping * seconds);
    }

    public Vector3 PointVelocity(Vector3 lever) => Velocity + Vector3.Cross(AngularVelocity, lever);

    public float ImpulseDenominator(Vector3 lever, Vector3 direction) =>
        1f + Vector3.Dot(direction,
            Vector3.Cross(WorldInverseInertia(Vector3.Cross(lever, direction)), lever));

    public void ApplyImpulse(Vector3 lever, Vector3 impulse)
    {
        Velocity += impulse;
        AngularVelocity = LimitAngularSpeed(AngularVelocity + WorldInverseInertia(Vector3.Cross(lever, impulse)));
    }

    public Vector3 LimitAngularSpeed(Vector3 velocity)
    {
        float speed = velocity.Length();
        return speed > settings.MaximumAngularSpeed ? velocity * (settings.MaximumAngularSpeed / speed) : velocity;
    }

    public Quaternion PredictOrientation(float seconds)
    {
        float speed = AngularVelocity.Length();
        return speed < 0.000001f ? Orientation : Quaternion.Normalize(Quaternion.Concatenate(Orientation,
            Quaternion.CreateFromAxisAngle(AngularVelocity / speed, speed * seconds)));
    }

    private Vector3 WorldInverseInertia(Vector3 torque)
    {
        Vector3 localTorque = Vector3.Transform(torque, Quaternion.Conjugate(Orientation));
        return Vector3.Transform(localTorque * inverseInertia, Orientation);
    }

    public static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
