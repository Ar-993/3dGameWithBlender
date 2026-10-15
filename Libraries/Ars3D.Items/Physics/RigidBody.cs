using System.Numerics;
using Ars3D.Items.Geometry;

namespace Ars3D.Items.Physics;

/// <summary>Состояние, движение и покой одного твёрдого тела.</summary>
public sealed class RigidBody
{
    private readonly RoundedBoxShape shape;
    private readonly BodyCollision collision;
    private readonly Vector3 inverseInertia;
    private float quietTime;

    public RigidBodySettings Settings { get; }
    public Vector3 Position => Center - Vector3.Transform(shape.LocalCenter, Orientation);
    public Vector3 Center { get; internal set; }
    public Vector3 Velocity { get; internal set; }
    public Quaternion Orientation { get; internal set; } = Quaternion.Identity;
    public Vector3 AngularVelocity { get; internal set; }
    public bool IsResting { get; private set; }
    public Vector3 HalfExtents => shape.HalfExtents;
    public float BoundingRadius => shape.BoundingRadius;
    public Aabb Bounds => shape.GetBounds(Center, Orientation);

    public RigidBody(Vector3 bottomPosition, float radius, float height,
        Vector3? halfExtents = null, RigidBodySettings? settings = null)
    {
        Settings = settings ?? RigidBodySettings.Default;
        Settings.Validate();
        shape = new RoundedBoxShape(radius, height, halfExtents);
        Vector3 half = shape.HalfExtents;
        inverseInertia = new(3f / (half.Y * half.Y + half.Z * half.Z),
            3f / (half.X * half.X + half.Z * half.Z),
            3f / (half.X * half.X + half.Y * half.Y));
        collision = new BodyCollision(shape, this, new ContactSolver(Settings), Settings);
        Reset(bottomPosition, Vector3.Zero, Quaternion.Identity, Vector3.Zero);
    }

    public void Reset(Vector3 bottomPosition, Vector3 velocity, Quaternion orientation, Vector3 angularVelocity)
    {
        ValidateResetArguments(bottomPosition, velocity, orientation, angularVelocity);

        Orientation = Quaternion.Normalize(orientation);
        Center = bottomPosition + Vector3.Transform(shape.LocalCenter, Orientation);
        Velocity = velocity;
        AngularVelocity = LimitAngularSpeed(angularVelocity);
        Wake();
    }

    public void Update(ICollisionWorld world, float seconds)
    {
        if (!float.IsFinite(seconds) || seconds <= 0f)
            return;
        if (!CanSimulate(world))
            return;

        Simulate(world, MathF.Min(seconds, Settings.MaximumUpdateTime));
    }

    // Шаги симуляции.
    private bool CanSimulate(ICollisionWorld world)
    {
        if (!IsResting)
            return true;

        collision.Gather(world);
        if (collision.HasSupport() && collision.MaximumPenetration() <= Settings.WakePenetration)
            return false;

        Wake();
        return true;
    }

    private void Simulate(ICollisionWorld world, float remaining)
    {
        while (remaining > 0.000001f && !IsResting)
        {
            float stepSeconds = GetStepDuration(remaining);
            Step(world, stepSeconds);
            remaining -= stepSeconds;
        }
    }

    private float GetStepDuration(float remaining)
    {
        float duration = MathF.Min(remaining, Settings.MaximumStep);
        return LimitStepByRotation(duration);
    }

    private void Step(ICollisionWorld world, float seconds)
    {
        ApplyForces(seconds);
        collision.SolveContacts(world);
        collision.Move(world, seconds);
        collision.RecoverOverlaps(world);
        collision.SolveContacts(world);
        UpdateRestingState(collision.HasSupport(), seconds);
    }

    // Движение, инерция и импульсы.
    private void ApplyForces(float seconds)
    {
        Velocity = (Velocity - Vector3.UnitY * (Settings.Gravity * seconds)) *
            MathF.Exp(-Settings.AirLinearDamping * seconds);
        AngularVelocity *= MathF.Exp(-Settings.AirAngularDamping * seconds);
    }

    internal Vector3 PointVelocity(Vector3 lever) => Velocity + Vector3.Cross(AngularVelocity, lever);

    internal float ImpulseDenominator(Vector3 lever, Vector3 direction) =>
        1f + Vector3.Dot(direction,
            Vector3.Cross(WorldInverseInertia(Vector3.Cross(lever, direction)), lever));

    internal void ApplyImpulse(Vector3 lever, Vector3 impulse)
    {
        Velocity += impulse;
        AngularVelocity = LimitAngularSpeed(AngularVelocity + WorldInverseInertia(Vector3.Cross(lever, impulse)));
    }

    private Vector3 LimitAngularSpeed(Vector3 velocity)
    {
        float speed = velocity.Length();
        return speed > Settings.MaximumAngularSpeed ? velocity * (Settings.MaximumAngularSpeed / speed) : velocity;
    }

    internal float LimitStepByRotation(float seconds)
    {
        float angularSpeed = AngularVelocity.Length();
        if (angularSpeed > 0.0001f)
            return MathF.Min(seconds, Settings.MaximumAngularStep / angularSpeed);

        return seconds;
    }

    internal Quaternion PredictOrientation(float seconds)
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

    internal static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    // Затухание, сон и пробуждение.
    private void Wake()
    {
        quietTime = 0f;
        IsResting = false;
    }

    private void UpdateRestingState(bool supported, float seconds)
    {
        if (supported)
            ApplySupportDamping(seconds);

        AngularVelocity = LimitAngularSpeed(AngularVelocity);
        UpdateRestTimer(supported, seconds);
    }

    private void ApplySupportDamping(float seconds)
    {
        AngularVelocity *= MathF.Exp(-Settings.SupportAngularDamping * seconds);
        if (!HasLowMotion(Settings.QuietLinearSpeedSquared, Settings.QuietSurfaceSpeedSquared))
            return;

        Velocity *= MathF.Exp(-Settings.QuietLinearDamping * seconds);
        AngularVelocity *= MathF.Exp(-Settings.QuietAngularDamping * seconds);
    }

    private bool HasLowMotion(float linearThresholdSquared, float surfaceThresholdSquared)
    {
        float linearSpeedSquared = Velocity.LengthSquared();
        float surfaceSpeedSquared = AngularVelocity.LengthSquared() * BoundingRadius * BoundingRadius;
        return linearSpeedSquared < linearThresholdSquared && surfaceSpeedSquared < surfaceThresholdSquared;
    }

    private void UpdateRestTimer(bool supported, float seconds)
    {
        bool quiet = supported && HasLowMotion(
            Settings.SleepLinearSpeed * Settings.SleepLinearSpeed,
            Settings.SleepSurfaceSpeed * Settings.SleepSurfaceSpeed);
        quietTime = quiet ? quietTime + seconds : 0f;
        if (quietTime >= Settings.SleepDelay)
            Sleep();
    }

    private void Sleep()
    {
        Velocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        IsResting = true;
    }

    private static void ValidateResetArguments(Vector3 bottomPosition, Vector3 velocity,
        Quaternion orientation, Vector3 angularVelocity)
    {
        if (!Finite(bottomPosition) || !Finite(velocity) || !Finite(angularVelocity) ||
            !IsValidOrientation(orientation))
        {
            throw new ArgumentException("Rigid item pose and velocities must be finite.");
        }
    }

    private static bool IsValidOrientation(Quaternion orientation) =>
        float.IsFinite(orientation.X) && float.IsFinite(orientation.Y) &&
        float.IsFinite(orientation.Z) && float.IsFinite(orientation.W) &&
        orientation.LengthSquared() >= 0.000001f;
}
