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

        var monsterAnims = new Dictionary<string, string>
        {
            { "Idle", "SkeletonIdle.fbx" },
            { "Run",  "SkeletonRun.fbx"  },
            { "Attack", "SkeletonAttack.fbx" },
            { "Hurt", "SkeletonHurt.fbx" },
            { "Die", "SkeletonDeath.fbx" }
        };

        character.LoadContent(graphicsDevice, skeletonFolder, monsterAnims, skeletonTexture);
        ai.SetAttackDuration(animation.GetClipDuration("Attack"));
        ai.SetHurtDuration(animation.GetClipDuration("Hurt"));
    }

    public void TakeDamage(int damage) => stats.TakeDamage(damage);

    public void Update(Player player, Level level, float deltaTime)
    {
        bool samePlatform = CurrentPlatform is not null &&
                            player.CurrentPlatform is not null &&
                            CurrentPlatform.Id == player.CurrentPlatform.Id;

        ai.Update(Position, player.Position, samePlatform, deltaTime);
        Vector3 horizontalMovement = Vector3.Zero;

        if (ai.Direction != Vector3.Zero)
            character.RotationY = MathF.Atan2(ai.Direction.X, ai.Direction.Z);

        if (ai.State == AIComponent.AiState.Chasing)
        {
            Vector3 newPos = Position + ai.Direction * Speed * deltaTime;
            if (CurrentPlatform!.ContainsHorizontal(newPos, character.CollisionRadius))
                horizontalMovement = ai.Direction * Speed * deltaTime;
        }

        Vector3 oldPosition = Position;
        character.Move(level, horizontalMovement, deltaTime);

        bool isRunning = horizontalMovement != Vector3.Zero &&
                         (Position.X != oldPosition.X || Position.Z != oldPosition.Z);

        switch (ai.State)
        {
            case AIComponent.AiState.Attacking:
                animation.Play("Attack", loop: false, deltaTime);
                break;
            case AIComponent.AiState.Hurt:
                animation.Play("Hurt", loop: false, deltaTime);
                break;
            case AIComponent.AiState.Dead:
                // Клип не зациклен: после завершения модель остаётся на последнем кадре.
                animation.Play("Die", loop: false, deltaTime);
                break;
            case AIComponent.AiState.Chasing when isRunning:
                animation.Play("Run", loop: true, deltaTime);
                break;
            default:
                animation.Play("Idle", loop: true, deltaTime);
                break;
        }

        if (ai.ShouldDealDamage && !player.IsDead)
            player.TakeDamage(10);
    }

    public void Draw(Matrix view, Matrix projection)
    {
        character.Draw(view, projection);
    }
}