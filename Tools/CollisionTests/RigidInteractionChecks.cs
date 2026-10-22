using System.Reflection;
using _3DLight;
using _3DLight.Items;
using Microsoft.Xna.Framework;

internal static class RigidInteractionChecks
{
    private static readonly Vector3 ToyHalfExtents = new(0.09f, 0.25f, 0.06f);

    public static void Run(Action<string, Action> run)
    {
        run("sideways rigid item keeps its model bottom pivot consistent with its center of mass", () =>
        {
            WorldItem item = Toy(new(2f, 3f, 4f));
            Quaternion sideways = Quaternion.CreateFromAxisAngle(Vector3.Forward, -MathHelper.PiOver2);
            item.Release(new(2f, 3f, 4f), Vector3.Zero, sideways, Vector3.Zero);

            Near(item.Position, new(2f, 3f, 4f));
            Near(item.Center, new(1.75f, 3f, 4f));
            Near(Vector3.Transform(Vector3.Up, item.Orientation), Vector3.Left);
            // The normalized model's head is half a metre along its local up axis.
            Near(item.Position + Vector3.Transform(Vector3.Up * item.Height, item.Orientation),
                new(1.5f, 3f, 4f));
            Require(item.Bounds.Contains(item.Center) != ContainmentType.Disjoint,
                "Rigid bounds did not contain the actual center of mass");

            item.Release(item.Position, Vector3.Zero, sideways, Vector3.Backward * 3f);
            item.UpdatePhysics(new Level(), 0.1f);
            Near(new(item.Center.X, 0f, item.Center.Z), new(1.75f, 0f, 4f));
            Require(MathF.Abs(item.Position.X - 2f) > 0.005f,
                "Rotation stayed anchored at the model's bottom rather than the center of mass");
            Near(item.Position + Vector3.Transform(item.LocalCenter, item.Orientation), item.Center);
        });

        run("rotated item bounds extend pickup reach while a wall still blocks pickup", () =>
        {
            Vector3 eye = new(0f, 1.6f, 0f);
            WorldItem reachable = SidewaysToy();
            Require(reachable.Position.X - reachable.Radius > 2f && reachable.Bounds.Min.X < 2f,
                "Fixture must be reachable by its sideways shape rather than an upright origin");
            var interaction = new ItemInteraction();
            interaction.Add(reachable);
            interaction.Update(Floor(), 0f, Vector3.Zero, eye, Vector3.Forward, false);
            Require(interaction.NearbyItem == reachable, "The sideways item's near end was out of reach");
            interaction.Update(Floor(), 0f, Vector3.Zero, eye, Vector3.Forward, true);
            Require(interaction.HeldItem == reachable, "The nearby rotated body could not be picked up");

            Level blockedLevel = Floor();
            Wall(blockedLevel, 1f);
            var blocked = new ItemInteraction();
            blocked.Add(SidewaysToy());
            blocked.Update(blockedLevel, 0f, Vector3.Zero, eye, Vector3.Forward, true);
            Require(blocked.NearbyItem is null && blocked.HeldItem is null,
                "The rotated pickup shape bypassed wall occlusion");
        });

        run("E pickup stops rigid angular motion and T throw restores tumbling", () =>
        {
            Level level = new();
            WorldItem item = Toy(new(0.4f, 0f, 0f), new(3f, 1f, 0.25f));
            var interaction = new ItemInteraction();
            interaction.Add(item);
            Vector3 eye = new(0f, 1.6f, 0f);
            Require(item.AngularVelocity.LengthSquared() > 1f, "Fixture did not start spinning");
            interaction.Update(level, 0f, Vector3.Zero, eye, Vector3.Forward, true);
            Require(interaction.HeldItem == item && item.IsHeld, "E did not pick up the rigid item");
            Near(item.AngularVelocity, Vector3.Zero);
            Near(item.Velocity, Vector3.Zero);
            Quaternion heldOrientation = item.Orientation;
            interaction.Update(level, 0.1f, Vector3.Zero, eye, Vector3.Forward, true);
            SameRotation(item.Orientation, heldOrientation);
            Require(interaction.HeldItem == item, "Holding E released the body without a new press");

            interaction.Update(level, 0f, Vector3.Zero, eye, Vector3.Forward, false);
            interaction.Update(level, 0f, Vector3.Zero, eye, Vector3.Forward, false, dropPressed: true);
            Require(interaction.HeldItem is null && !item.IsHeld,
                "T did not release the rigid item");
            Require(item.AngularVelocity.LengthSquared() > 1f && item.Velocity.Z < -7f,
                "Throwing did not restore physical spin and forward motion");
            Quaternion releaseOrientation = item.Orientation;
            item.UpdatePhysics(level, 1f / 30f);
            Require(MathF.Abs(Quaternion.Dot(item.Orientation, releaseOrientation)) < 0.9999f,
                "The released body's visible orientation did not advance");
        });

        run("pitched rigid release preserves the held camera matrix pivot, center and orientation", () =>
        {
            Level level = new();
            Vector3 feet = new(3f, 0.4f, -4f);
            Vector3 eye = new(3f, 2f, -4f);
            Vector3 forward = Vector3.Normalize(new Vector3(-0.35f, 0.65f, -0.67f));
            WorldItem item = Toy(new(3.2f, 0.5f, -4f));
            var interaction = new ItemInteraction();
            interaction.Add(item);
            interaction.Update(level, 0f, feet, eye, forward, true);
            Require(interaction.HeldItem == item, "Fixture rigid item was not picked up");
            interaction.Update(level, 0f, feet, eye, forward, false);

            Matrix cameraWorld = Matrix.Invert(Matrix.CreateLookAt(eye, eye + forward, Vector3.Up));
            Matrix heldWorld = Matrix.CreateRotationY(item.State.Definition.HeldYaw) *
                Matrix.CreateTranslation(ItemMath.ToGame(item.State.Definition.Interaction.HeldOffset)) * cameraWorld;
            interaction.Update(level, 0f, feet, eye, forward, true);

            Near(item.Position, Vector3.Transform(Vector3.Zero, heldWorld));
            Near(item.Center, Vector3.Transform(item.LocalCenter, heldWorld));
            foreach (Vector3 axis in new[] { Vector3.Right, Vector3.Up, Vector3.Forward })
                Near(Vector3.Transform(axis, item.Orientation), Vector3.TransformNormal(axis, heldWorld));
            Require(Vector3.Dot(Vector3.Transform(Vector3.Backward, item.Orientation), forward) < -0.999f,
                "Santa's face points away from the player while held");
            Vector3 localHead = Vector3.Up * item.Height;
            Near(item.Position + Vector3.Transform(localHead, item.Orientation),
                Vector3.Transform(localHead, heldWorld));
        });

        run("upward rigid throw beside a wall keeps the complete rotated body on the near side", () =>
        {
            Level level = Floor();
            Wall(level, 0f);
            Vector3 feet = new(-0.35f, 0f, 0f);
            Vector3 eye = feet + Vector3.Up * 1.6f;
            Vector3 forward = Vector3.Normalize(new Vector3(1f, 1f, 0.25f));
            WorldItem item = Toy(new(-0.65f, 0.2f, 0f));
            var interaction = new ItemInteraction();
            interaction.Add(item);
            interaction.Update(level, 0f, feet, eye, forward, true);
            Require(interaction.HeldItem == item, "Fixture rigid item was not picked up");
            interaction.Update(level, 0f, feet, eye, forward, false);
            interaction.Update(level, 0f, feet, eye, forward, false, dropPressed: true);
            Require(item.Bounds.Max.X <= 0.003f,
                $"Release placed part of the rotated toy through the wall: {item.Bounds}");

            for (int step = 0; step < 120; step++)
            {
                item.UpdatePhysics(level, 1f / 120f);
                Require(item.Bounds.Max.X <= 0.003f,
                    $"The tumbling toy crossed the thin wall on step {step}: {item.Bounds}");
            }
        });
    }

    private static WorldItem Toy(Vector3 position, Vector3 spin = default)
    {
        var item = new WorldItem("Santa", position, radius: 0.12f, height: 0.5f);
        item.EnableRigidBody(ToyHalfExtents, spin);
        return item;
    }

    private static WorldItem SidewaysToy()
    {
        WorldItem item = Toy(new(2.45f, 1.6f, 0f));
        item.Release(item.Position, Vector3.Zero,
            Quaternion.CreateFromAxisAngle(Vector3.Forward, -MathHelper.PiOver2), Vector3.Zero);
        return item;
    }

    private static Level Floor()
    {
        Level level = new();
        Quad(level, "Floor", new(-8f, 0f, -8f), new(8f, 0f, -8f),
            new(8f, 0f, 8f), new(-8f, 0f, 8f));
        return level;
    }

    private static void Wall(Level level, float x) =>
        Quad(level, "Wall", new(x, -2f, -8f), new(x, 6f, -8f),
            new(x, 6f, 8f), new(x, -2f, 8f));

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

    private static void SameRotation(Quaternion actual, Quaternion expected) =>
        Require(MathF.Abs(Quaternion.Dot(actual, expected)) >= 0.99999f,
            $"Orientation changed while held: {actual}, expected {expected}");

    private static void Near(Vector3 actual, Vector3 expected) =>
        Require(float.IsFinite(actual.X) && float.IsFinite(actual.Y) && float.IsFinite(actual.Z) &&
            Vector3.Distance(actual, expected) <= 0.0002f,
            $"Expected {expected}, got {actual}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
