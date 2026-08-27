using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using _3DLight;

public class Skeleton
{
    private readonly CharacterFacade character;
    private readonly SkeletonAIComponent ai;
    private readonly CharacterAnimationComponent animation;
    private readonly StatsComponent stats;

    public Vector3 Position => character.Position;
    public float RotationY => character.RotationY;
    public float Speed { get; set; } = 3.5f;
    public int Health => stats.Health;
    public int MaxHealth => stats.MaxHealth;
    public bool IsDead => stats.IsDead;

    public Level.Platform? CurrentPlatform => character.CurrentPlatform;

    internal Skeleton(CharacterFacade character)
    {
        this.character = character;
        ai = character.GetComponent<SkeletonAIComponent>();
        animation = character.GetComponent<CharacterAnimationComponent>();
        stats = character.GetComponent<StatsComponent>();
        ai.BindStats(stats);
    }

    public void LoadContent(GraphicsDevice graphicsDevice, ContentManager content, string animsFolder)
    {
        string skeletonFolder = Path.Combine(animsFolder, "Skeleton");
        Texture2D skeletonTexture = content.Load<Texture2D>("Assets/Skeleton/skeleton_texture");
        Effect toonEffect = content.Load<Effect>("ToonShader");

        var monsterAnims = new Dictionary<string, string>
        {
            { "Idle", "SkeletonIdle.fbx" },
            { "Run",  "SkeletonRun.fbx"  },
            { "Attack", "SkeletonAttack.fbx" },
            { "Hurt", "SkeletonHurt.fbx" },
            { "Die", "SkeletonDeath.fbx" }
        };

        character.LoadContent(graphicsDevice, skeletonFolder, monsterAnims, skeletonTexture, toonEffect);
        ai.SetAttackDuration(animation.GetClipDuration("Attack"));
        ai.SetHurtDuration(animation.GetClipDuration("Hurt"));
    }

    public void TakeDamage(int damage)
    {
        int appliedDamage = stats.TakeDamage(damage);

        if (appliedDamage > 0 && !IsDead)
            animation.Restart("Hurt", loop: false);
    }

    public void SpawnAt(Vector3 position) => character.SpawnAt(position);

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
            character.RotationY = MathF.Atan2(ai.Direction.X, ai.Direction.Z);

            float movementDistance = Speed * deltaTime;

            if (ai.State == AIComponent.AiState.Attacking)
            {
                Vector3 difference = player.Position - Position;
                difference.Y = 0f;

                float minimumDistance =
                    character.CollisionRadius + player.CollisionRadius;
                float availableDistance =
                    MathF.Max(0f, difference.Length() - minimumDistance);

                movementDistance = MathF.Min(movementDistance, availableDistance);
            }

            Vector3 requestedMovement = ai.Direction * movementDistance;
            Vector3 newPosition = Position + requestedMovement;

            if (currentPlatform.ContainsHorizontal(
                    newPosition,
                    character.CollisionRadius))
            {
                horizontalMovement = requestedMovement;
            }
        }

        Vector3 oldPosition = Position;

        // Во время Hurt и Dead движение равно Vector3.Zero; Attacking сохраняет преследование.
        character.Move(level, horizontalMovement, deltaTime);

        bool isRunning = horizontalMovement != Vector3.Zero &&
                         (Position.X != oldPosition.X || Position.Z != oldPosition.Z);

        switch (ai.State)
        {
            case AIComponent.AiState.Attacking:
                animation.Play("Attack", loop: false);
                break;
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

        animation.Update(deltaTime);

        if (ai.ShouldDealDamage &&
            !player.IsDead &&
            ai.IsTargetInAttackRange(Position, player.Position))
        {
            player.TakeDamage(10);
        }
    }

    public void Draw(Matrix view, Matrix projection)
    {
        character.Draw(view, projection);
    }
}
