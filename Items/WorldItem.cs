using Ars3D.Items;
using Ars3D.Rendering;
using _3DLight.Items;
using Microsoft.Xna.Framework;
using _3DLight;

namespace Items;

// MonoGame view of an item. All physical and inventory state lives in the library.
internal sealed class WorldItem
{
    private ItemModelRenderer? renderer;
    private bool isRegistered;

    public ItemInstance State { get; private set; }
    public ItemModelRenderer? Renderer => renderer;
    public string Name => State.Definition.DisplayName;
    public Vector3 Position => ItemMath.ToGame(State.Position);
    public Vector3 Center => ItemMath.ToGame(State.Center);
    public Vector3 Velocity => ItemMath.ToGame(State.Velocity);
    public Vector3 AngularVelocity => ItemMath.ToGame(State.AngularVelocity);
    public Quaternion Orientation => ItemMath.ToGame(State.Orientation);
    public BoundingBox Bounds => ItemMath.ToGame(State.Bounds);
    public float Radius => State.Definition.Radius;
    public float Height => State.Definition.Height;
    public Vector3 LocalCenter => ItemMath.ToGame(State.LocalCenter);
    public bool IsHeld => State.IsHeld;
    public bool IsResting => State.IsResting;
    public bool HasRigidBody => true;

    public WorldItem(ItemInstance state) => State = state;

    internal WorldItem(string name, Vector3 position, float radius = 0.24f, float height = 0.7f)
        : this(new ItemInstance(new ItemDefinition(name, name, radius, height), ItemMath.ToLibrary(position))) { }

    public void AttachModel(CompiledModel model, Matrix normalization) =>
        renderer = new ItemModelRenderer(model, normalization);

    // Setup helper for a manually created item; catalog items already have their shape.
    internal void EnableRigidBody(Vector3 halfExtents, Vector3 initialSpin)
    {
        if (isRegistered)
            throw new InvalidOperationException("Configure the item before registering it for interaction.");
        ItemDefinition previous = State.Definition;
        var definition = new ItemDefinition(previous.Id, previous.DisplayName, Radius, Height,
            ItemMath.ToLibrary(halfExtents), ItemMath.ToLibrary(initialSpin), previous.HeldYaw,
            previous.BodySettings, previous.Interaction);
        State = new ItemInstance(definition, State.Position, State.Orientation, State.Velocity,
            ItemMath.ToLibrary(initialSpin), State.InstanceId);
    }

    internal void MarkRegistered() => isRegistered = true;
    public void UpdatePhysics(Level level, float seconds) => State.UpdatePhysics(level.ItemWorld, seconds);
    internal void PickUp() => State.PickUp();
    internal void Release(Vector3 position, Vector3 velocity, Quaternion? orientation = null, Vector3? spin = null) =>
        State.Release(ItemMath.ToLibrary(position), ItemMath.ToLibrary(velocity),
            orientation.HasValue ? ItemMath.ToLibrary(orientation.Value) : null,
            spin.HasValue ? ItemMath.ToLibrary(spin.Value) : null);

    internal void DrawWorld(Matrix view, Matrix projection, SceneLighting lighting)
    {
        if (!IsHeld)
            renderer?.Draw(State.WorldPose, view, projection, lighting);
    }

    internal void DrawHeld(ItemPose pose, Matrix view, Matrix projection, SceneLighting lighting) =>
        renderer?.Draw(pose, view, projection, lighting);
}
