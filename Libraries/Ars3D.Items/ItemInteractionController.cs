using System.Collections.ObjectModel;
using System.Numerics;

namespace Ars3D.Items;

// The caller supplies its chosen action button as a Boolean. No input framework is required.
public sealed class ItemInteractionController
{
    private readonly List<ItemInstance> items = [];
    private readonly Dictionary<Guid, ItemInstance> itemsById = [];
    private readonly ReadOnlyCollection<ItemInstance> readOnlyItems;
    private bool wasActionDown;

    public IReadOnlyList<ItemInstance> Items => readOnlyItems;
    public SingleSlotInventory Inventory { get; }
    public ItemInteractionSettings Settings { get; }
    public ItemInstance? HeldItem => Inventory.HeldItem;
    public ItemInstance? NearbyItem { get; private set; }

    public ItemInteractionController(
        ItemInteractionSettings? settings = null,
        SingleSlotInventory? inventory = null)
    {
        Settings = settings ?? new ItemInteractionSettings();
        Inventory = inventory ?? new SingleSlotInventory();
        readOnlyItems = items.AsReadOnly();
    }

    public void Add(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (itemsById.TryGetValue(item.InstanceId, out ItemInstance? existing))
        {
            if (ReferenceEquals(existing, item))
                return;
            throw new ArgumentException("An item with this instance ID is already registered.", nameof(item));
        }
        itemsById.Add(item.InstanceId, item);
        items.Add(item);
    }

    // Call while paused or while a menu owns input to consume an already-held button.
    public void SyncInput(bool actionDown)
    {
        wasActionDown = actionDown;
        NearbyItem = null;
    }

    public void Update(
        IItemInteractionWorld world,
        float seconds,
        Vector3 playerFeet,
        Vector3 eyePosition,
        Vector3 forward,
        bool actionDown,
        bool canInteract = true)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (ItemInstance item in items)
            item.UpdatePhysics(world, seconds);

        bool pressed = actionDown && !wasActionDown;
        wasActionDown = actionDown;
        NearbyItem = null;

        if (!canInteract)
        {
            ItemRelease.Apply(Inventory, world, eyePosition, forward, Settings, throwItem: false);
            return;
        }

        if (HeldItem is null)
            NearbyItem = ItemReachQuery.FindNearest(items, world, playerFeet, eyePosition, Settings.PickupDistance);
        if (!pressed)
            return;

        if (HeldItem is not null)
            ItemRelease.Apply(Inventory, world, eyePosition, forward, Settings, throwItem: true);
        else if (NearbyItem is not null && Inventory.TryPickUp(NearbyItem))
            NearbyItem = null;
    }

    // Use this same pose to draw the held model and to release it without a visual jump.
    public ItemPose GetHeldPose(Vector3 eyePosition, Vector3 forward)
    {
        ItemInstance item = HeldItem ?? throw new InvalidOperationException("The inventory is empty.");
        return HeldItemPlacement.CreatePose(item, eyePosition, HeldItemPlacement.CreateFrame(forward), Settings);
    }
}
