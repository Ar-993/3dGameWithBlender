using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

internal sealed class CharacterFacade
{
    private readonly GameEntity entity;
    private readonly CharacterPhysicsComponent physics;
    private readonly CharacterAnimationComponent animation;

    public Vector3 Position => physics.Position;
    public float CollisionRadius => physics.Radius;
    public bool IsGrounded => physics.IsGrounded;
    public float VerticalVelocity => physics.VerticalVelocity;
    public Level.Platform? CurrentPlatform => physics.CurrentPlatform;

    public float RotationY
    {
        get => animation.RotationY;
        set => animation.RotationY = value;
    }

    public CharacterFacade(GameEntity entity)
    {
        this.entity = entity;
        physics = entity.Get<CharacterPhysicsComponent>();
        animation = entity.Get<CharacterAnimationComponent>();
    }

    public void LoadContent(
        GraphicsDevice graphicsDevice,
        string animationsFolder,
        Dictionary<string, string> animations,
        Texture2D texture,
        Effect toonEffect) =>
        animation.LoadContent(graphicsDevice, animationsFolder, animations, texture, toonEffect);

    public bool TryJump(float impulse) => physics.TryJump(impulse);

    public void SpawnAt(Vector3 position) => physics.SpawnAt(position);

    public float CalculateJumpRiseDuration(float impulse) =>
        physics.CalculateRiseDuration(impulse);

    public T GetComponent<T>() where T : class, IGameComponent => entity.Get<T>();

    public bool HasComponent<T>() where T : class, IGameComponent => entity.Has<T>();

    public void Move(Level level, Vector3 horizontalMovement, float deltaTime) =>
        physics.Move(level, horizontalMovement, deltaTime);

    public void Play(string clipName, bool loop) =>
        animation.Play(clipName, loop);

    public void PlaySegment(
        string stateName,
        string clipName,
        float rangeStartNormalized,
        float rangeEndNormalized,
        bool loop) =>
        animation.PlaySegment(
            stateName,
            clipName,
            rangeStartNormalized,
            rangeEndNormalized,
            loop);

    public void UpdateAnimation(float deltaTime) =>
        animation.Update(deltaTime);

    public void Draw(Matrix view, Matrix projection) =>
        animation.Draw(Position, view, projection);
}
