using _3DLight;
using Microsoft.Xna.Framework;

internal static class InventoryChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("E fills four distinct slots without duplicating registered items", () =>
        {
            var inventory = CreateInventory();
            var level = new Level();
            for (int index = 0; index < 4; index++)
            {
                Update(inventory, level);
                Update(inventory, level, pickUp: true);
                Require(inventory.SelectedSlot == index, "Pickup did not select its destination slot");
                Require(inventory.HeldItem == inventory.GetSlot(index), "Held item differs from the selected slot");
                Update(inventory, level, pickUp: true);
                Require(Contents(inventory).Count() == index + 1, "Holding E repeated pickup");
            }

            Update(inventory, level);
            Update(inventory, level, pickUp: true);
            Require(Contents(inventory).Count() == 4, "A fifth item entered the full inventory");
            Require(Contents(inventory).Distinct().Count() == 4, "One item occupies several slots");
            Require(inventory.Items.Count == 5, "Pickup created or removed world instances");
            Require(inventory.Items.Count(item => item.IsHeld) == 4, "Stored items still participate in the world");
        });

        run("slot selection and T release share one authoritative held item", () =>
        {
            var inventory = CreateInventory();
            var level = new Level();
            for (int index = 0; index < 3; index++)
            {
                Update(inventory, level);
                Update(inventory, level, pickUp: true);
            }
            WorldItem released = inventory.GetSlot(1)!;
            Update(inventory, level, slot: 1);
            Require(inventory.HeldItem == released, "Selecting slot 2 did not change the held model");
            Require(Contents(inventory).All(item => item.IsHeld), "Switching slots released stored items");

            Update(inventory, level, drop: true);
            Require(inventory.GetSlot(1) is null && inventory.HeldItem is null,
                "T left a stale held reference, duplicating the world and carried model");
            Require(!released.IsHeld && released.Velocity.Z < 0f, "T did not release the selected physical item");
            Require(Contents(inventory).Count() == 2 && Contents(inventory).All(item => item.IsHeld),
                "T changed other slots");
            Update(inventory, level, drop: true);
            Require(Contents(inventory).Count() == 2, "Dropping from an empty slot released another item");

            Update(inventory, level, pickUp: true);
            Require(inventory.SelectedSlot == 1 && inventory.HeldItem == inventory.GetSlot(1),
                "Pickup did not refill the selected empty slot");
            Require(inventory.Items.Count == 5, "Repeated pickup cloned a registered item");
        });

        run("death releases all slots without retaining a carried reference", () =>
        {
            var inventory = CreateInventory();
            var level = new Level();
            for (int index = 0; index < 4; index++)
            {
                Update(inventory, level);
                Update(inventory, level, pickUp: true);
            }
            inventory.Update(level, 0f, Vector3.Zero, new(0f, 1.6f, 0f),
                Vector3.Forward, false, canInteract: false);
            Require(!Contents(inventory).Any() && inventory.HeldItem is null, "Death left occupied slots");
            Require(inventory.Items.All(item => !item.IsHeld), "Death left a hidden physical item");
        });
    }

    private static ItemInteraction CreateInventory()
    {
        var inventory = new ItemInteraction();
        for (int index = 0; index < 5; index++)
            inventory.Add(new WorldItem($"Item {index}", new(index * 0.1f, 1f, -1f), 0.1f, 0.2f));
        return inventory;
    }

    private static IEnumerable<WorldItem> Contents(ItemInteraction inventory)
    {
        for (int index = 0; index < 4; index++)
            if (inventory.GetSlot(index) is { } item)
                yield return item;
    }

    private static void Update(ItemInteraction inventory, Level level,
        bool pickUp = false, int slot = -1, bool drop = false)
    {
        inventory.Update(level, 0f, Vector3.Zero, new(0f, 1.6f, 0f),
            Vector3.Forward, pickUp, selectedSlot: slot, dropPressed: drop);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
