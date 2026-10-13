using System.Reflection;
using _3DLight;
using Ars3D.Items.Physics;
using _3DLight.Items;
using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;
using Matrix = System.Numerics.Matrix4x4;

internal static class RigidItemChecks
{
    private static readonly Vector3 SantaHalfExtents = new(0.087f, 0.25f, 0.054f);

    public static void Run(Action<string, Action> run)
    {
        run("rigid item rotation preserves the supplied bottom pivot and world center", () =>
        {
            RigidBody body = Body(new(2f, 3f, 4f));
            Quaternion rotation = Quaternion.CreateFromYawPitchRoll(0.4f, 0.7f, -0.3f);
            Vector3 pivot = new(7f, 2f, -3f);
            body.Reset(pivot, new(1f, 2f, 3f), rotation, new(0.7f, 0.3f, -0.5f));
            Near(body.Position, pivot);
            Near(body.Center, pivot + Vector3.Transform(Vector3.UnitY * 0.25f, rotation));
            Matrix modelWorld = Matrix.CreateTranslation(0f, -0.25f, 0f) *
                Matrix.CreateFromQuaternion(body.Orientation) * Matrix.CreateTranslation(body.Center);
            Near(Vector3.Transform(Vector3.Zero, modelWorld), pivot);
            Require(body.Bounds.Contains(body.Center),
                "Body bounds do not contain its center");
        });

        run("a thrown rigid item rotates as one finite solid body in flight", () =>
        {
            RigidBody body = Body(new(0f, 4f, 0f));
            body.Reset(body.Position, new(3f, 1f, -4f), Quaternion.Identity, new(2f, 0.7f, -1f));
            Quaternion original = body.Orientation;
            body.Update(new Level().ItemWorld, 0.15f);
            Require(MathF.Abs(Quaternion.Dot(original, body.Orientation)) < 0.995f,
                "Angular velocity did not rotate the solid item");
            Require(body.Center.X > 0.4f && body.Center.Z < -0.5f && body.Velocity.Y < 0f,
                "Translation and gravity were lost during rotation");
            CheckFinite(body);
        });

        run("a spinning rigid toy tumbles onto its side and eventually sleeps", () =>
        {
            Level floor = Floor();
            RigidBody body = Body(new(0f, 3f, 0f));
            body.Reset(body.Position, Vector3.Zero, Quaternion.Identity, new(2.8f, 0.65f, 1.1f));
            Advance(body, floor, 600);
            Vector3 longAxis = Vector3.Transform(Vector3.UnitY, body.Orientation);
            Require(MathF.Abs(longAxis.Y) < 0.75f,
                $"Toy was forced upright instead of tumbling: axis={longAxis}");
            Require(body.IsResting && body.Velocity == Vector3.Zero && body.AngularVelocity == Vector3.Zero,
                $"Toy did not rest: v={body.Velocity}, angular={body.AngularVelocity}");
            Near(body.Bounds.Min.Y, CapsuleCollision.SkinWidth, 0.003f);
            Quaternion restingPose = body.Orientation;
            Advance(body, floor, 60);
            Require(MathF.Abs(Quaternion.Dot(restingPose, body.Orientation)) > 0.999999f,
                "A sleeping rigid item changed its pose");
        });

        run("different rigid throws retain different finite landing poses", () =>
        {
            Level floor = Floor();
            RigidBody left = Body(new(-2f, 2f, 0f));
            RigidBody right = Body(new(2f, 2f, 0f));
            left.Reset(left.Position, Vector3.Zero, Quaternion.Identity, new(2.5f, 0.6f, 0.7f));
            right.Reset(right.Position, Vector3.Zero, Quaternion.Identity, new(-1.8f, -0.4f, 1.4f));
            Advance(left, floor, 600);
            Advance(right, floor, 600);
            Require(MathF.Abs(Quaternion.Dot(left.Orientation, right.Orientation)) < 0.99f,
                "Different throws snapped to a single fixed landing pose");
            CheckFinite(left);
            CheckFinite(right);
        });

        run("rigid item contact impulses create angular motion on an off-center landing", () =>
        {
            RigidBody body = Body(new(0f, 0.4f, 0f));
            body.Reset(body.Position, (-Vector3.UnitY) * 3f,
                Quaternion.CreateFromAxisAngle((-Vector3.UnitZ), 0.55f), Vector3.Zero);
            float largestSpin = 0f;
            Level floor = Floor();
            for (int frame = 0; frame < 45; frame++)
            {
                body.Update(floor.ItemWorld, 1f / 60f);
                largestSpin = MathF.Max(largestSpin, body.AngularVelocity.Length());
                CheckFinite(body);
            }
            Require(largestSpin > 0.5f, "Off-center contact did not apply torque to the whole item");
        });

        run("a fast rotating rigid item cannot cross a thin mesh wall", () =>
        {
            Level level = new();
            Quad(level, "Wall", new(0f, -5f, -8f), new(0f, 8f, -8f),
                new(0f, 8f, 8f), new(0f, -5f, 8f));
            RigidBody body = Body(new(-4f, 1f, 0f));
            body.Reset(body.Position, new(150f, 0f, 0f),
                Quaternion.CreateFromYawPitchRoll(0.3f, 0.6f, 0.2f), new(4f, 3f, -2f));
            body.Update(level.ItemWorld, 0.1f);
            Require(body.Bounds.Max.X <= 0.003f, $"Rigid body crossed the wall: {body.Bounds}");
            CheckFinite(body);
        });

        run("a fast spinning rigid item is caught by a zero thickness floor", () =>
        {
            RigidBody body = Body(new(0f, 3f, 0f));
            body.Reset(body.Position, (-Vector3.UnitY) * 260f,
                Quaternion.CreateFromYawPitchRoll(0.3f, -0.4f, 0.5f), new(5f, -2f, 3f));
            body.Update(Floor().ItemWorld, 0.1f);
            Require(body.Bounds.Min.Y >= -0.002f, $"Rigid body crossed the floor: {body.Bounds}");
            CheckFinite(body);
        });

        run("a sleeping rigid item wakes when its support is removed", () =>
        {
            RigidBody body = Body(new(0f, 0.02f, 0f));
            Advance(body, Floor(), 300);
            Require(body.IsResting, "Item did not enter sleep on its original floor");
            float originalY = body.Center.Y;
            body.Update(new Level().ItemWorld, 0.1f);
            Require(!body.IsResting && body.Center.Y < originalY && body.Velocity.Y < 0f,
                "Removing the floor left the rigid item suspended");
        });
    }

    private static RigidBody Body(Vector3 feet) => new(feet, 0.12f, 0.5f, SantaHalfExtents);

    private static void Advance(RigidBody body, Level level, int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            body.Update(level.ItemWorld, 1f / 60f);
            CheckFinite(body);
        }
    }

    private static Level Floor()
    {
        Level level = new();
        Quad(level, "Floor", new(-12f, 0f, -12f), new(12f, 0f, -12f),
            new(12f, 0f, 12f), new(-12f, 0f, 12f));
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
            name, ItemMath.ToGame(first), ItemMath.ToGame(second), ItemMath.ToGame(third), ItemMath.ToGame(Vector3.Normalize(Vector3.Cross(second - first, third - first))),
            new(ItemMath.ToGame(Vector3.Min(first, Vector3.Min(second, third))), ItemMath.ToGame(Vector3.Max(first, Vector3.Max(second, third))))));
    }

    private static void CheckFinite(RigidBody body)
    {
        Require(Finite(body.Center) && Finite(body.Position) && Finite(body.Velocity) && Finite(body.AngularVelocity),
            "Rigid body produced a non-finite state");
        Near(body.Orientation.LengthSquared(), 1f, 0.0001f);
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void Near(Vector3 actual, Vector3 expected) =>
        Require(Vector3.Distance(actual, expected) <= 0.0001f, $"Expected {expected}, got {actual}");

    private static void Near(float actual, float expected, float tolerance) =>
        Require(MathF.Abs(actual - expected) <= tolerance, $"Expected {expected}, got {actual}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
