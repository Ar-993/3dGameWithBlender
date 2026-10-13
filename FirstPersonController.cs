using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace _3DLight;

internal sealed class FirstPersonController
{
    private const float JumpImpulse = 7.5f;
    private readonly CharacterPhysicsComponent physics;
    private bool jumpWasDown;

    public float Yaw { get; private set; }

    public FirstPersonController(CharacterPhysicsComponent physics) =>
        this.physics = physics;

    public void SynchronizeInput(KeyboardState keyboard) =>
        jumpWasDown = keyboard.IsKeyDown(Keys.Space);

    public void Update(KeyboardState keyboard, float yaw, Level level,
        float seconds, float speed, bool canMove, bool canJump, Vector3 forcedMovement)
    {
        Yaw = yaw;
        Vector3 movement = forcedMovement;
        if (canMove)
            movement += GetMoveDirection(keyboard, yaw) * speed * seconds;

        bool jumpDown = keyboard.IsKeyDown(Keys.Space);
        if (canJump && jumpDown && !jumpWasDown)
            physics.TryJump(JumpImpulse);
        jumpWasDown = jumpDown;
        physics.Move(level, movement, seconds);
    }

    internal static Vector3 GetMoveDirection(KeyboardState keyboard, float yaw)
    {
        Vector3 local = new(
            (keyboard.IsKeyDown(Keys.D) ? 1f : 0f) - (keyboard.IsKeyDown(Keys.A) ? 1f : 0f),
            0f,
            (keyboard.IsKeyDown(Keys.S) ? 1f : 0f) - (keyboard.IsKeyDown(Keys.W) ? 1f : 0f));
        if (local == Vector3.Zero)
            return Vector3.Zero;
        return Vector3.TransformNormal(Vector3.Normalize(local), Matrix.CreateRotationY(yaw));
    }
}
