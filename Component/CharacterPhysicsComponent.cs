using _3DLight;
using Microsoft.Xna.Framework;

internal sealed class CharacterPhysicsComponent : IGameComponent
{
    private readonly float gravity;
    private float verticalVelocity;

    public Vector3 Position { get; private set; }
    public float Radius { get; }
    public float Height { get; }
    public Level.Platform? CurrentPlatform { get; private set; }
    public bool IsGrounded => CurrentPlatform is not null;
    public float VerticalVelocity => verticalVelocity;

    public float CalculateRiseDuration(float initialVelocity)
    {
        if (initialVelocity <= 0f || gravity >= 0f)
            return 0f;

        return initialVelocity / -gravity;
    }

    public CharacterPhysicsComponent(Vector3 startPosition, float radius, float height, float gravity)
    {
        Position = startPosition;
        Radius = radius;
        Height = height;
        this.gravity = gravity;
    }

    public bool TryJump(float impulse)
    {
        if (!IsGrounded)
            return false;

        verticalVelocity = impulse;
        CurrentPlatform = null;
        return true;
    }

    public void SpawnAt(Vector3 position)
    {
        Position = position;
        verticalVelocity = 0f;
        CurrentPlatform = null;
    }

    public void Move(Level level, Vector3 horizontalMovement, float deltaTime)
    {
        verticalVelocity += gravity * deltaTime;
        Vector3 movement = horizontalMovement;
        movement.Y = verticalVelocity * deltaTime;

        Position = level.MoveCharacter(
            Position,
            movement,
            Radius,
            Height,
            ref verticalVelocity,
            out Level.Platform? platform);
        CurrentPlatform = platform;
    }
}
