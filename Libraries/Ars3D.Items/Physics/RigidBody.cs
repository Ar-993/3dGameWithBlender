using System.Numerics;
using Ars3D.Items.Geometry;

namespace Ars3D.Items.Physics;

/// <summary>A single solid item's pose, velocities and collision simulation.</summary>
public sealed class RigidBody
{
    private readonly RoundedBoxShape shape;
    private readonly BodyMotion motion;
    private readonly RestingState resting;
    private readonly BodyStepper stepper;
    public RigidBodySettings Settings { get; }
    public Vector3 Position => motion.BottomPosition;
    public Vector3 Center => motion.Center;
    public Vector3 Velocity => motion.Velocity;
    public Quaternion Orientation => motion.Orientation;
    public Vector3 AngularVelocity => motion.AngularVelocity;
    public bool IsResting => resting.IsResting;
    public Vector3 HalfExtents => shape.HalfExtents;
    public float BoundingRadius => shape.BoundingRadius;
    public Aabb Bounds => shape.GetBounds(motion.Center, motion.Orientation);

    public RigidBody(Vector3 bottomPosition, float radius, float height,
        Vector3? halfExtents = null, RigidBodySettings? settings = null)
    {
        Settings = settings ?? RigidBodySettings.Default;
        Settings.Validate();
        shape = new RoundedBoxShape(radius, height, halfExtents);
        motion = new BodyMotion(shape, Settings);
        resting = new RestingState(Settings);
        var solver = new ContactSolver(Settings);
        var collision = new BodyCollision(shape, motion, solver, Settings);
        stepper = new BodyStepper(shape, motion, solver, collision, resting, Settings);
        Reset(bottomPosition, Vector3.Zero, Quaternion.Identity, Vector3.Zero);
    }

    /// <summary>Resets the rotated model bottom pivot and wakes its solid body.</summary>
    public void Reset(Vector3 bottomPosition, Vector3 velocity, Quaternion orientation, Vector3 angularVelocity)
    {
        motion.Reset(bottomPosition, velocity, orientation, angularVelocity);
        resting.Wake();
    }

    public void Update(ICollisionWorld world, float seconds) => stepper.Update(world, seconds);
}
