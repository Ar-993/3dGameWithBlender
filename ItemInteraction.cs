using Ars3D.Items;
using _3DLight.Items;
using Microsoft.Xna.Framework;

namespace _3DLight;

// Game glue only: input comes from LightGame, rules come from Ars3D.Items.
internal sealed class ItemInteraction
{
    private readonly ItemInteractionController controller;
    private readonly List<WorldItem> items = [];
    private readonly Dictionary<Guid, WorldItem> views = [];

    public ItemInteractionSettings Settings => controller.Settings;
    public IReadOnlyList<WorldItem> Items => items;
    public WorldItem? HeldItem => FindView(controller.HeldItem);
    public WorldItem? NearbyItem => FindView(controller.NearbyItem);
    public int SelectedSlot => controller.SelectedSlot;
    public WorldItem? GetSlot(int index) => FindView(controller.Slots[index]);

    public ItemInteraction(ItemInteractionSettings? settings = null) =>
        controller = new ItemInteractionController(settings);

    public void Add(WorldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (views.ContainsKey(item.State.InstanceId))
            return;
        controller.Add(item.State);
        item.MarkRegistered();
        views.Add(item.State.InstanceId, item);
        items.Add(item);
    }

    public void SuppressInput(bool actionDown) => controller.SyncInput(actionDown);

    public void Update(
    Level level,
    float seconds,
    Vector3 playerFeet,
    Vector3 eye,
    Vector3 forward,
    bool eDown,
    bool canInteract = true,
    int selectedSlot = -1,
    bool dropPressed = false)
    {
        var playerPosition = ItemMath.ToLibrary(playerFeet);
        var eyePosition = ItemMath.ToLibrary(eye);
        var lookDirection = ItemMath.ToLibrary(forward);

        controller.Update(
            world: level.ItemWorld,
            seconds: seconds,
            playerFeet: playerPosition,
            eyePosition: eyePosition,
            forward: lookDirection,
            actionDown: eDown,
            canInteract: canInteract,
            selectedSlot: selectedSlot,
            dropPressed: dropPressed);
    }

    public bool TryGetHeldLight(Vector3 eye, Vector3 forward, out Vector3 origin, out Vector3 direction)
    {
        origin = default;
        direction = forward;
        if (HeldItem is not { Renderer: { } renderer } held ||
            !string.Equals(held.State.Definition.Id, "flashlight", StringComparison.OrdinalIgnoreCase))
            return false;
        ItemPose pose = controller.GetHeldPose(ItemMath.ToLibrary(eye), ItemMath.ToLibrary(forward));
        if (!renderer.TryGetMarker(pose, "LightOrigin", out origin) ||
            !renderer.TryGetMarker(pose, "LightTarget", out Vector3 target))
            return false;
        direction = target - origin;
        if (direction.LengthSquared() < 0.000001f)
            return false;
        direction.Normalize();
        return true;
    }

    public void Draw(Matrix view, Matrix projection, SceneLighting lighting)
    {
        foreach (WorldItem item in items)
            item.DrawWorld(view, projection, lighting);
        if (HeldItem is { } held)
        {
            Matrix cameraWorld = Matrix.Invert(view);
            ItemPose pose = controller.GetHeldPose(ItemMath.ToLibrary(cameraWorld.Translation),
                ItemMath.ToLibrary(cameraWorld.Forward));
            held.DrawHeld(pose, view, projection, lighting);
        }
    }

    private WorldItem? FindView(ItemInstance? state) =>
        state is not null ? views[state.InstanceId] : null;
}
