using System.Reflection;
using _3DLight;
using _3DLight.Gameplay;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

internal static class FirstPersonChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("first person eye follows the capsule from inside its head", () =>
        {
            FirstPersonCamera camera = new();
            foreach (Vector3 feet in new[] { Vector3.Zero, new Vector3(7f, 3f, -11f) })
            {
                camera.ApplyMouseDelta(125f, -100f);
                camera.UpdateMatrices(feet, 16f / 9f);
                CapsuleShape capsule = new(feet, 0.35f, 1.8f);
                Vector3 nearestAxisPoint = Vector3.Clamp(camera.Position, capsule.AxisStart, capsule.AxisEnd);
                Require(Vector3.Distance(camera.Position, nearestAxisPoint) < capsule.Radius,
                    $"The camera eye left the collision capsule: {camera.Position}");
                Near(camera.Position.X, feet.X);
                Near(camera.Position.Z, feet.Z);
                Require(camera.Position.Y > feet.Y + capsule.Height / 2f &&
                    camera.Position.Y < feet.Y + capsule.Height,
                    "The eye is not inside the upper half of the character");
            }
        });

        run("initial first person view faces forward from the player eye", () =>
        {
            FirstPersonCamera camera = new();
            camera.UpdateMatrices(new(4f, 2f, 8f), 16f / 9f);
            Near(camera.Forward, Vector3.Forward);
            CheckView(camera);
        });

        run("mouse movement right turns right and movement up looks up", () =>
        {
            FirstPersonCamera right = new();
            right.ApplyMouseDelta(150f, 0f);
            right.UpdateMatrices(Vector3.Zero, 16f / 9f);
            Require(right.Forward.X > 0f && right.Forward.Z < 0f,
                $"Moving the mouse right did not turn right: {right.Forward}");
            Near(right.Forward.Y, 0f);

            FirstPersonCamera up = new();
            up.ApplyMouseDelta(0f, -150f);
            up.UpdateMatrices(Vector3.Zero, 16f / 9f);
            Require(up.Forward.Y > 0f && up.Forward.Z < 0f,
                $"Moving the mouse up did not look up: {up.Forward}");
            Near(up.Forward.X, 0f);
            CheckView(right);
            CheckView(up);
        });

        run("pitch limits prevent flipping and keep the view invertible", () =>
        {
            FirstPersonCamera camera = new();
            foreach (float verticalDelta in new[] { -100000f, 100000f })
            {
                camera.ApplyMouseDelta(700f, verticalDelta);
                camera.UpdateMatrices(new(9f, -2f, 4f), 16f / 9f);
                float limitedPitch = camera.Pitch;
                Require(MathF.Abs(limitedPitch) < MathHelper.PiOver2,
                    "The camera reached or crossed the singular vertical pitch");
                Require(MathF.Abs(camera.Forward.Y) > 0.99f,
                    "A large mouse movement did not reach the intended pitch limit");
                Require(verticalDelta < 0f ? camera.Forward.Y > 0f : camera.Forward.Y < 0f,
                    "The pitch limit inverted the look direction");
                CheckView(camera);

                camera.ApplyMouseDelta(0f, verticalDelta);
                camera.UpdateMatrices(camera.Position - Vector3.Up * camera.EyeHeight, 16f / 9f);
                Near(camera.Pitch, limitedPitch);
                CheckView(camera);
            }
        });

        run("W follows camera yaw on the ground while looking up or down", () =>
        {
            foreach (float horizontalDelta in new[] { 0f, 180f, -390f, 1600f })
            {
                foreach (float verticalDelta in new[] { -100000f, 0f, 100000f })
                {
                    FirstPersonCamera camera = new();
                    camera.ApplyMouseDelta(horizontalDelta, verticalDelta);
                    camera.UpdateMatrices(Vector3.Zero, 16f / 9f);
                    Vector3 walking = FirstPersonController.GetMoveDirection(Movement(Keys.W), camera.Yaw);
                    Vector3 groundForward = new(camera.Forward.X, 0f, camera.Forward.Z);
                    groundForward.Normalize();
                    Near(walking.Y, 0f);
                    Near(walking.Length(), 1f);
                    Require(Vector3.Dot(walking, groundForward) > 0.9999f,
                        $"W disagrees with the view heading: movement={walking}, view={camera.Forward}");
                }
            }
        });

        run("diagonal movement has cardinal speed and opposing keys cancel", () =>
        {
            foreach (float yaw in new[] { 0f, MathHelper.PiOver2, -1.2f })
            {
                Vector3 forward = FirstPersonController.GetMoveDirection(Movement(Keys.W), yaw);
                Vector3 right = FirstPersonController.GetMoveDirection(Movement(Keys.D), yaw);
                Vector3 diagonal = FirstPersonController.GetMoveDirection(Movement(Keys.W, Keys.D), yaw);
                Near(diagonal.Length(), forward.Length());
                Near(diagonal.Length(), right.Length());
                Require(Vector3.Dot(diagonal, forward) > 0f && Vector3.Dot(diagonal, right) > 0f,
                    "Diagonal input lost one of its two movement directions");
                Near(FirstPersonController.GetMoveDirection(Movement(Keys.W, Keys.S), yaw), Vector3.Zero);
                Near(FirstPersonController.GetMoveDirection(Movement(Keys.A, Keys.D), yaw), Vector3.Zero);
                Near(FirstPersonController.GetMoveDirection(Movement(Keys.W, Keys.S, Keys.A, Keys.D), yaw), Vector3.Zero);
                Near(FirstPersonController.GetMoveDirection(Movement(Keys.W, Keys.S, Keys.D), yaw), right);
            }

            CharacterPhysicsComponent physics = new(new(0f, 2f, 0f), 0.35f, 1.8f, gravity: 0f);
            FirstPersonController controller = new(physics);
            Vector3 start = physics.Position;
            controller.Update(Movement(Keys.W, Keys.D), false, 0.7f, new Level(), 0.5f, 4f,
                canMove: true, canJump: true);
            Near(Vector3.Distance(start, physics.Position), 2f);
            Near(physics.Position.Y, start.Y);
        });

        run("Space jumps on a new press without repeating after landing", () =>
        {
            Level floor = Floor();
            CharacterPhysicsComponent physics = GroundedCharacter(floor);
            FirstPersonController controller = new(physics);
            GameplayInputState input = new();
            input.Enter(new(), default, true);
            float groundedHeight = physics.Position.Y;
            Step(controller, input, new(Keys.Space), floor);
            Require(!physics.IsGrounded && physics.Position.Y > groundedHeight && physics.VerticalVelocity > 0f,
                "A fresh Space press did not lift the capsule off the floor");

            for (int frame = 0; frame < 240 && !physics.IsGrounded; frame++)
                Step(controller, input, new(Keys.Space), floor);
            Require(physics.IsGrounded, "The jump did not return to the floor");
            for (int frame = 0; frame < 30; frame++)
            {
                Step(controller, input, new(Keys.Space), floor);
                Require(physics.IsGrounded, "Holding Space triggered another jump after landing");
            }

            Step(controller, input, new(), floor);
            Step(controller, input, new(Keys.Space), floor);
            Require(!physics.IsGrounded && physics.VerticalVelocity > 0f,
                "Releasing and pressing Space did not permit the next jump");
        });

        run("synchronized held Space does not jump when gameplay resumes", () =>
        {
            Level floor = Floor();
            CharacterPhysicsComponent physics = GroundedCharacter(floor);
            FirstPersonController controller = new(physics);
            GameplayInputState input = new();
            input.Enter(new(Keys.Space), default, true);
            Step(controller, input, new(Keys.Space), floor);
            Require(physics.IsGrounded, "Held Space from a menu triggered a jump on resume");
            Step(controller, input, new(), floor);
            Step(controller, input, new(Keys.Space), floor);
            Require(!physics.IsGrounded && physics.VerticalVelocity > 0f,
                "Input synchronization prevented a subsequent fresh jump press");
        });
    }

    private static void CheckView(FirstPersonCamera camera)
    {
        Matrix inverse = Matrix.Invert(camera.View);
        Near(Vector3.Transform(Vector3.Zero, inverse), camera.Position);
        Near(Vector3.TransformNormal(Vector3.Forward, inverse), camera.Forward);
        Near(Vector3.Transform(camera.Position, camera.View), Vector3.Zero);
        Near(Vector3.Transform(camera.Position + camera.Forward * 5f, camera.View), new(0f, 0f, -5f));
        Near(camera.Forward.Length(), 1f);
    }

    private static CharacterPhysicsComponent GroundedCharacter(Level floor)
    {
        CharacterPhysicsComponent physics = new(new(0f, 0.05f, 0f), 0.35f, 1.8f, -28f);
        for (int frame = 0; frame < 60; frame++)
            physics.Move(floor, Vector3.Zero, 1f / 60f);
        Require(physics.IsGrounded, "The test capsule did not settle on its floor");
        return physics;
    }

    private static Vector2 Movement(params Keys[] keys) =>
        new GameplayInputState().Update(new KeyboardState(keys), default, true, new(0, 0, 320, 180)).Move;

    private static void Step(FirstPersonController controller, GameplayInputState input,
        KeyboardState keyboard, Level floor)
    {
        GameplayInputFrame frame = input.Update(keyboard, default, true, new(0, 0, 320, 180));
        controller.Update(frame.Move, frame.JumpPressed, 0f, floor, 1f / 60f, 4f,
            canMove: true, canJump: true);
    }

    private static Level Floor()
    {
        Level floor = new();
        var triangles = (List<Level.TriangleCollider>)typeof(Level)
            .GetField("meshColliders", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(floor)!;
        Vector3 a = new(-8f, 0f, -8f), b = new(8f, 0f, -8f),
            c = new(8f, 0f, 8f), d = new(-8f, 0f, 8f);
        Add(a, b, c);
        Add(a, c, d);
        return floor;

        void Add(Vector3 first, Vector3 second, Vector3 third) => triangles.Add(new(
            "Floor", first, second, third,
            Vector3.Normalize(Vector3.Cross(second - first, third - first)),
            new(Vector3.Min(first, Vector3.Min(second, third)),
                Vector3.Max(first, Vector3.Max(second, third)))));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Near(Vector3 actual, Vector3 expected) =>
        Require(Vector3.Distance(actual, expected) <= 0.002f, $"Expected {expected}, got {actual}");

    private static void Near(float actual, float expected) =>
        Require(MathF.Abs(actual - expected) <= 0.002f, $"Expected {expected}, got {actual}");
}
