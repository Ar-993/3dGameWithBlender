using _3DLight;
using Microsoft.Xna.Framework;

namespace Camera;

internal sealed class FirstPersonController
{
    private const float JumpImpulse = 7.5f;
    private readonly CharacterPhysicsComponent physics;

    public float Yaw { get; private set; }

    public FirstPersonController(CharacterPhysicsComponent physics) =>
        this.physics = physics;

    public void Update(Vector2 movementInput, bool jumpPressed, float yaw, Level level,
        float seconds, float speed, bool canMove, bool canJump)
    {
        Yaw = yaw;
        Vector3 movement = canMove
            ? GetMoveDirection(movementInput, yaw) * speed * seconds
            : Vector3.Zero;

        if (canJump && jumpPressed)
            physics.TryJump(JumpImpulse);
        physics.Move(level, movement, seconds);
    }

    internal static Vector3 GetMoveDirection(Vector2 movementInput, float yaw)
    {
        Vector3 local = new(movementInput.X, 0f, -movementInput.Y);
        if (local.LengthSquared() > 1f)
            local.Normalize();
        return Vector3.TransformNormal(local, Matrix.CreateRotationY(yaw));
    }
}
