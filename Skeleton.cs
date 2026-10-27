using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using _3DLight;
using Ars3D.Rendering;

public class Skeleton : IDisposable
{
    private const string UpperBodyRootBone = "spine";

    private readonly CharacterPhysicsComponent physics;
    private readonly SkeletonAIComponent ai;
    private readonly CharacterAnimationComponent animation;
    private readonly StatsComponent stats;

    public Vector3 Position => physics.Position;
    public float RotationY => animation.RotationY;
    public float Speed { get; set; } = 3.5f;
    public int Health => stats.Health;
    public int MaxHealth => stats.MaxHealth;
    public bool IsDead => stats.IsDead;

    public Level.Platform? CurrentPlatform => physics.CurrentPlatform;

    internal Skeleton(
        CharacterPhysicsComponent physics,
        CharacterAnimationComponent animation,
        StatsComponent stats,
        SkeletonAIComponent ai)
    {
        this.physics = physics;
        this.animation = animation;
        this.stats = stats;
        this.ai = ai;
        ai.BindStats(stats);
    }

    public void LoadContent(
        GraphicsDevice graphicsDevice,
        ContentManager content)
    {
        Texture2D skeletonTexture =
            content.Load<Texture2D>("Assets/Skeleton/skeleton_texture");
        Effect toonEffect = content.Load<Effect>("ToonShader");

        animation.LoadContent(
            graphicsDevice,
            modelAssetName: "skeleton",
            initialClip: "Idle",
            texture: skeletonTexture,
            toonEffect: toonEffect);

        ai.SetAttackDuration(animation.GetClipDuration("Attack"));
        ai.SetHurtDuration(animation.GetClipDuration("Hurt"));
    }

    public void TakeDamage(int damage)
    {
        int appliedDamage = stats.TakeDamage(damage);

        if (appliedDamage > 0 && !IsDead)
            animation.Restart("Hurt", loop: false);
    }

    public void SpawnAt(Vector3 position) => physics.SpawnAt(position);

    public void Update(Player player, Level level, float deltaTime)
    {
        bool samePlatform = CurrentPlatform is not null &&
                            player.CurrentPlatform is not null &&
                            CurrentPlatform.Id == player.CurrentPlatform.Id;

        ai.Update(Position, player.Position, samePlatform, deltaTime);
        Vector3 horizontalMovement = Vector3.Zero;

        bool canMoveTowardPlayer =
            ai.State is AIComponent.AiState.Chasing or AIComponent.AiState.Attacking;

        // Во время атаки скелет преследует цель, не делая свой шаг внутрь её радиуса.
        if (canMoveTowardPlayer &&
            ai.Direction != Vector3.Zero &&
            CurrentPlatform is { } currentPlatform)
        {
            animation.RotationY = MathF.Atan2(ai.Direction.X, ai.Direction.Z);

            float movementDistance = Speed * deltaTime;

            if (ai.State == AIComponent.AiState.Attacking)
            {
                Vector3 difference = player.Position - Position;
                difference.Y = 0f;

                float minimumDistance =
                    physics.Radius + player.CollisionRadius;
                float availableDistance =
                    MathF.Max(0f, difference.Length() - minimumDistance);

                movementDistance = MathF.Min(movementDistance, availableDistance);
            }

            Vector3 requestedMovement = ai.Direction * movementDistance;
            Vector3 newPosition = Position + requestedMovement;

            if (currentPlatform.ContainsHorizontal(
                    newPosition,
                    physics.Radius))
            {
                horizontalMovement = requestedMovement;
            }
        }

        Vector3 oldPosition = Position;

        // Во время Hurt и Dead движение равно Vector3.Zero; Attacking сохраняет преследование.
        physics.Move(level, horizontalMovement, deltaTime);

        bool isRunning = horizontalMovement != Vector3.Zero &&
                         (Position.X != oldPosition.X || Position.Z != oldPosition.Z);

        if (ai.State == AIComponent.AiState.Attacking)
        {
            animation.SetUpperBodyOverlay(
                "Attack",
                UpperBodyRootBone,
                ai.AttackElapsedSeconds);

            animation.Play(
                isRunning ? "Run" : "Idle",
                loop: true);
        }
        else
        {
            animation.ClearUpperBodyOverlay();

            switch (ai.State)
            {
                case AIComponent.AiState.Hurt:
                    animation.Play("Hurt", loop: false);
                    break;
                case AIComponent.AiState.Dead:
                    animation.Play("Die", loop: false);
                    break;
                case AIComponent.AiState.Chasing when isRunning:
                    animation.Play("Run", loop: true);
                    break;
                default:
                    animation.Play("Idle", loop: true);
                    break;
            }
        }

        animation.Update(deltaTime);

        if (ai.ShouldDealDamage &&
            !player.IsDead &&
            ai.IsTargetInAttackRange(Position, player.Position))
        {
            player.TakeDamage(10);
        }
    }

    public void Draw(
        Matrix view,
        Matrix projection,
        SceneLighting lighting)
    {
        animation.Draw(Position, view, projection, lighting);
    }

    public void Dispose() => animation.Dispose();
}
