using System.Reflection;
using _3DLight;
using _3DLight.Items;
using Microsoft.Xna.Framework;

internal static class ItemChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("items fall under gravity and settle on a thin mesh floor", () =>
        {
            Level level = Floor();
            WorldItem item = new("Santa", new(0f, 3f, 0f));
            item.UpdatePhysics(level, 1f / 60f);
            Require(item.Velocity.Y < 0f && item.Position.Y < 3f, "Gravity was not applied");
            for (int frame = 0; frame < 360; frame++)
                item.UpdatePhysics(level, 1f / 60f);
            Near(item.Position.Y, CapsuleCollision.SkinWidth);
            Require(item.IsResting && item.Velocity == Vector3.Zero, "Item kept bouncing at rest");
        });

        run("a fast physical item cannot cross a zero thickness wall", () =>
        {
            Level level = new();
            Quad(level, "Wall", new(0f, -4f, -4f), new(0f, 4f, -4f),
                new(0f, 4f, 4f), new(0f, -4f, 4f));
            Vector3 velocity = new(200f, 0f, 0f);
            Vector3 position = level.MoveItem(new(-4f, 1f, 0f), 0.24f, 0.7f,
                0.1f, ref velocity, out _);
            Require(position.X <= -0.24f, $"Item crossed the wall: {position}");
            Require(velocity.X <= 0f, "Throw velocity was not deflected");
        });

        run("item recovery removes an initial floor overlap", () =>
        {
            Vector3 velocity = Vector3.Zero;
            Vector3 position = Floor().MoveItem(new(0f, -0.08f, 0f), 0.24f, 0.7f,
                1f / 60f, ref velocity, out _);
            Near(position.Y, CapsuleCollision.SkinWidth);
        });

        run("gravity slides physical items down a steep triangle slope", () =>
        {
            Level level = new();
            Quad(level, "Slope", new(-5f, -2.5f, -4f), new(5f, 2.5f, -4f),
                new(5f, 2.5f, 4f), new(-5f, -2.5f, 4f));
            WorldItem item = new("Santa", new(0f, 0.15f, 0f));
            for (int frame = 0; frame < 90; frame++)
                item.UpdatePhysics(level, 1f / 60f);
            Require(item.Position.X < -0.15f && item.Position.Y < 0f,
                $"Slope did not affect item motion: {item.Position}");
        });

        run("one invisible slot picks the nearest item on the E rising edge", () =>
        {
            Level level = new();
            ItemInteraction interaction = new();
            WorldItem far = new("Far", new(1.2f, 0f, 0f));
            WorldItem near = new("Near", new(0.4f, 0f, 0f));
            interaction.Add(far);
            interaction.Add(near);
            Update(interaction, level, false);
            Require(interaction.NearbyItem == near, "Nearest item did not expose a prompt candidate");
            Update(interaction, level, true);
            Require(interaction.HeldItem == near && near.IsHeld && !far.IsHeld,
                "Pickup did not fill exactly one slot");
            Require(interaction.NearbyItem is null, "Held inventory still showed another pickup");
            Update(interaction, level, true);
            Require(interaction.HeldItem == near, "Holding E immediately threw the item");
            Update(interaction, level, false);
            Update(interaction, level, true);
            Require(interaction.HeldItem is null && !near.IsHeld && near.Velocity.Z < -7f,
                "Second E press did not throw in the look direction");
        });

        run("held items leave world physics until they are released", () =>
        {
            WorldItem item = new("Santa", new(0f, 3f, 0f));
            item.PickUp();
            item.UpdatePhysics(new Level(), 0.2f);
            Require(item.Position.Y == 3f && item.Velocity == Vector3.Zero,
                "Held object continued falling");
            item.Release(new(0f, 2f, 0f), Vector3.Zero);
            item.UpdatePhysics(new Level(), 0.1f);
            Require(item.Position.Y < 2f && item.Velocity.Y < 0f,
                "Released object did not return to physics");
        });

        run("pitched throws start at the carried camera offset and follow the look direction", () =>
        {
            Level level = new();
            WorldItem item = new("Santa", new(0.4f, 0f, 0f));
            ItemInteraction interaction = new();
            interaction.Add(item);
            Update(interaction, level, true);
            Vector3 eye = new(0f, 1.6f, 0f);
            Vector3 forward = new(0f, 0.6f, -0.8f);
            interaction.Update(level, 0f, Vector3.Zero, eye, forward, false);
            interaction.Update(level, 0f, Vector3.Zero, eye, forward, true);

            Vector3 cameraUp = Vector3.Cross(Vector3.Right, forward);
            Vector3 heldOffset = ItemMath.ToGame(interaction.Settings.HeldOffset);
            Vector3 expected = eye + Vector3.Right * heldOffset.X +
                cameraUp * heldOffset.Y - forward * heldOffset.Z;
            Require(Vector3.Distance(item.Position, expected) < 0.0001f,
                $"Throw detached from the visible held model: {item.Position}, expected {expected}");
            Vector3 expectedVelocity = forward * 7.5f + Vector3.Up * 1.7f;
            Require(Vector3.Distance(item.Velocity, expectedVelocity) < 0.0001f,
                $"Throw did not follow camera pitch: {item.Velocity}");
        });

        run("nearby pickup is blocked by walls and by reach distance", () =>
        {
            Level level = new();
            Quad(level, "Wall", new(0.5f, -1f, -4f), new(0.5f, 4f, -4f),
                new(0.5f, 4f, 4f), new(0.5f, -1f, 4f));
            ItemInteraction behindWall = new();
            behindWall.Add(new("Blocked", new(1f, 0f, 0f)));
            Update(behindWall, level, true);
            Require(behindWall.HeldItem is null && behindWall.NearbyItem is null,
                "Picked an item through a wall");
            ItemInteraction farAway = new();
            farAway.Add(new("Distant", new(3f, 0f, 0f)));
            Update(farAway, new Level(), true);
            Require(farAway.HeldItem is null && farAway.NearbyItem is null,
                "Picked an item outside arm's reach");
        });

        run("throw release is swept to the near side of an adjacent wall", () =>
        {
            Level level = new();
            Quad(level, "Wall", new(0f, -1f, -4f), new(0f, 4f, -4f),
                new(0f, 4f, 4f), new(0f, -1f, 4f));
            WorldItem item = new("Santa", new(-1f, 0f, 0f));
            ItemInteraction interaction = new();
            interaction.Add(item);
            Vector3 eye = new(-0.65f, 1.6f, 0f);
            interaction.Update(level, 0f, new(-0.65f, 0f, 0f), eye, Vector3.Right, true);
            Require(interaction.HeldItem == item, "Fixture item was not picked up");
            interaction.Update(level, 0f, new(-0.65f, 0f, 0f), eye, Vector3.Right, false);
            interaction.Update(level, 0f, new(-0.65f, 0f, 0f), eye, Vector3.Right, true);
            Require(item.Position.X <= -item.Radius,
                $"Item was released through the wall: {item.Position}");
            item.UpdatePhysics(level, 0.1f);
            Require(item.Position.X <= -item.Radius, "Thrown item crossed the adjacent wall");
        });

        run("looking upward cannot release an item through a nearby wall", () =>
        {
            Level level = new();
            Quad(level, "Wall", new(0f, -1f, -4f), new(0f, 4f, -4f),
                new(0f, 4f, 4f), new(0f, -1f, 4f));
            WorldItem item = new("Santa", new(-0.6f, 0f, 0f), radius: 0.12f, height: 0.5f);
            ItemInteraction interaction = new();
            interaction.Add(item);
            Vector3 feet = new(-0.35f, 0f, 0f);
            Vector3 eye = feet + Vector3.Up * 1.6f;
            Vector3 forward = Vector3.Normalize(new Vector3(1f, 1f, 0f));
            interaction.Update(level, 0f, feet, eye, forward, true);
            Require(interaction.HeldItem == item, "Fixture item was not picked up");
            interaction.Update(level, 0f, feet, eye, forward, false);
            interaction.Update(level, 0f, feet, eye, forward, true);
            Require(item.Position.X <= -item.Radius,
                $"Camera pitch put the release point behind the wall: {item.Position}");
            item.UpdatePhysics(level, 0.1f);
            Require(item.Position.X <= -item.Radius,
                $"The upward throw crossed the wall: {item.Position}");
        });

        run("disabled interaction drops the held item and consumes held E", () =>
        {
            Level level = new();
            WorldItem item = new("Santa", new(0.4f, 0f, 0f));
            ItemInteraction interaction = new();
            interaction.Add(item);
            Update(interaction, level, true);
            interaction.Update(level, 0f, Vector3.Zero, new(0f, 1.6f, 0f),
                Vector3.Forward, true, canInteract: false);
            Require(interaction.HeldItem is null && !item.IsHeld && item.Velocity == Vector3.Zero,
                "Disabled character kept or threw the item");
            Update(interaction, level, true);
            Require(interaction.HeldItem is null, "Returning while E was held auto-picked the item");
            Update(interaction, level, false);
            Update(interaction, level, true);
            Require(interaction.HeldItem == item, "Dropped item could not be picked up again");
        });

        run("paused input suppression avoids an accidental pickup on resume", () =>
        {
            ItemInteraction interaction = new();
            interaction.Add(new("Santa", new(0.4f, 0f, 0f)));
            interaction.SuppressInput(true);
            Update(interaction, new Level(), true);
            Require(interaction.HeldItem is null, "E held in a menu triggered pickup");
        });
    }

    private static void Update(ItemInteraction interaction, Level level, bool eDown) =>
        interaction.Update(level, 0f, Vector3.Zero, new(0f, 1.6f, 0f), Vector3.Forward, eDown);

    private static Level Floor()
    {
        Level level = new();
        Quad(level, "Floor", new(-8f, 0f, -8f), new(8f, 0f, -8f),
            new(8f, 0f, 8f), new(-8f, 0f, 8f));
        return level;
    }

    private static void Quad(Level level, string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        var triangles = (List<Level.TriangleCollider>)typeof(Level)
            .GetField("meshColliders", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(level)!;
        Add(a, b, c);
        Add(a, c, d);
        void Add(Vector3 first, Vector3 second, Vector3 third) => triangles.Add(new(
            name, first, second, third,
            Vector3.Normalize(Vector3.Cross(second - first, third - first)),
            new(Vector3.Min(first, Vector3.Min(second, third)),
                Vector3.Max(first, Vector3.Max(second, third)))));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Near(float actual, float expected) =>
        Require(MathF.Abs(actual - expected) < 0.002f, $"Expected {expected}, got {actual}");
}
