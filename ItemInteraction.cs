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

    public void Update(Level level, float seconds, Vector3 playerFeet, Vector3 eye,
        Vector3 forward, bool eDown, bool canInteract = true) =>
        controller.Update(level.ItemWorld, seconds, ItemMath.ToLibrary(playerFeet),
            ItemMath.ToLibrary(eye), ItemMath.ToLibrary(forward), eDown, canInteract);

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
