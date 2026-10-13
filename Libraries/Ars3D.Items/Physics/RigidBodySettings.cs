using System;

namespace Ars3D.Items.Physics;

/// <summary>Immutable tuning for the existing item simulation, in meters and seconds.</summary>
public sealed record RigidBodySettings
{
    public static RigidBodySettings Default { get; } = new();
    public float Gravity { get; init; } = 18f;
    public float MaximumStep { get; init; } = 1f / 180f;
    public float MaximumUpdateTime { get; init; } = 0.25f;
    public float MaximumAngularStep { get; init; } = 0.04f;
    public float MaximumAngularSpeed { get; init; } = 35f;
    public int MaximumImpacts { get; init; } = 12;
    public int ContactIterations { get; init; } = 10;
    public int RecoveryIterations { get; init; } = 6;
    public float RecoveryTolerance { get; init; } = 0.00001f;
    public float Friction { get; init; } = 0.65f;
    public float Restitution { get; init; } = 0.12f;
    public float BounceSpeed { get; init; } = 1.2f;
    public float AirLinearDamping { get; init; } = 0.06f;
    public float AirAngularDamping { get; init; } = 0.18f;
    public float SupportAngularDamping { get; init; } = 1.6f;
    public float QuietLinearSpeedSquared { get; init; } = 0.0144f;
    public float QuietSurfaceSpeedSquared { get; init; } = 0.04f;
    public float QuietLinearDamping { get; init; } = 5f;
    public float QuietAngularDamping { get; init; } = 6f;
    public float SleepLinearSpeed { get; init; } = 0.06f;
    public float SleepSurfaceSpeed { get; init; } = 0.075f;
    public float SleepDelay { get; init; } = 0.6f;
    public float MinimumSupportNormalY { get; init; } = 0.45f;
    public float WakePenetration { get; init; } = 0.002f;

    internal void Validate()
    {
        if (MaximumImpacts <= 0 || ContactIterations <= 0 || RecoveryIterations <= 0)
            throw new ArgumentException("Physics iteration counts must be positive.");
        foreach (float value in new[] { MaximumStep, MaximumUpdateTime, MaximumAngularStep,
                     MaximumAngularSpeed, SleepDelay })
        {
            if (!float.IsFinite(value) || value <= 0f)
                throw new ArgumentException("Physics step, angular limits and sleep delay must be positive and finite.");
        }
        foreach (float value in new[] { Gravity, RecoveryTolerance, Friction, Restitution, BounceSpeed,
                     AirLinearDamping, AirAngularDamping, SupportAngularDamping, QuietLinearSpeedSquared,
                     QuietSurfaceSpeedSquared, QuietLinearDamping, QuietAngularDamping, SleepLinearSpeed,
                     SleepSurfaceSpeed, WakePenetration })
        {
            if (!float.IsFinite(value) || value < 0f)
                throw new ArgumentException("Physics rates and tolerances must be nonnegative and finite.");
        }
        if (!float.IsFinite(MinimumSupportNormalY) || MinimumSupportNormalY < -1f || MinimumSupportNormalY > 1f)
            throw new ArgumentException("Support normal must be between -1 and 1.");
    }
}
