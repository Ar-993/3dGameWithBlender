using System.Numerics;
using Ars3D.Items.Geometry;
using Ars3D.Items.Physics;

namespace Ars3D.Items;

// Physical and inventory state only. A game can attach its own renderer by InstanceId.
public sealed class ItemInstance
{
    public Guid InstanceId { get; }
    public ItemDefinition Definition { get; }
    public RigidBody Body { get; }
    public bool IsHeld { get; private set; }
    public Vector3 Position => Body.Position;
    public Vector3 Center => Body.Center;
    public Vector3 Velocity => Body.Velocity;
    public Quaternion Orientation => Body.Orientation;
    public Vector3 AngularVelocity => Body.AngularVelocity;
    public bool IsResting => Body.IsResting;
    public Vector3 LocalCenter => Vector3.UnitY * (Definition.Height * 0.5f);
    public float BoundingRadius => Body.BoundingRadius;
    public Aabb Bounds => Body.Bounds;
    public ItemPose WorldPose => new(Position, Orientation);

    public ItemInstance(
        ItemDefinition definition,
        Vector3 bottomPosition,
        Quaternion? orientation = null,
        Vector3? velocity = null,
        Vector3? angularVelocity = null,
        Guid? instanceId = null)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        InstanceId = instanceId ?? Guid.NewGuid();
        if (InstanceId == Guid.Empty)
            throw new ArgumentException("An item instance ID cannot be empty.", nameof(instanceId));
        Body = new RigidBody(bottomPosition, definition.Radius, definition.Height,
            definition.HalfExtents, definition.BodySettings);
        Body.Reset(bottomPosition, velocity ?? Vector3.Zero,
            orientation ?? Quaternion.Identity, angularVelocity ?? definition.SpawnAngularVelocity);
    }

    public void UpdatePhysics(ICollisionWorld world, float seconds)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!IsHeld)
            Body.Update(world, seconds);
    }

    public void PickUp()
    {
        Body.Reset(Position, Vector3.Zero, Orientation, Vector3.Zero);
        IsHeld = true;
    }

    public void Release(
        Vector3 bottomPosition,
        Vector3 velocity,
        Quaternion? orientation = null,
        Vector3? angularVelocity = null)
    {
        Body.Reset(bottomPosition, velocity, orientation ?? Orientation, angularVelocity ?? Vector3.Zero);
        IsHeld = false;
    }
}
