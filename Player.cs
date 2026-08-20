using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;

public class Player
{
    private readonly CharacterFacade character;
    private readonly StatsComponent stats;
    private readonly CharacterAnimationComponent animation;
    private readonly AttackComponent attack;
    private readonly AttackComponent flyKick = new(
        damage: 30,
        range: 1.6f,
        hitTimeNormalized: 0.45f);
    public Vector3 Position => character.Position;
    public float RotationY => character.RotationY;
    public float Speed { get; set; } = 6f;
    public float CollisionRadius => character.CollisionRadius;
    public int Health => stats.Health;
    public int MaxHealth => stats.MaxHealth;
    public bool IsDead => stats.IsDead;
    public bool AttackShouldDealDamage =>
        attack.ShouldDealDamage || flyKick.ShouldDealDamage;
    public int AttackDamage => flyKick.ShouldDealDamage
        ? flyKick.Damage
        : attack.Damage;
    public float AttackRange => flyKick.ShouldDealDamage
        ? flyKick.Range
        : attack.Range;

    private const float JumpImpulse = 30f;
    private const float BadFallThreshold = 5f;
    private const float MovingLandingReleaseTime = 0.08f;

    public Level.Platform? CurrentPlatform => character.CurrentPlatform;

    private KeyboardState previousKeyboard;
    private float jumpAnimationTimeScale = 1f;
    private float fallingDuration;
    private float landingAnimationDuration;
    private float landingAnimationTimeRemaining;
    private bool wasAirborne;

    internal Player(CharacterFacade character)
    {
        this.character = character;
        stats = character.GetComponent<StatsComponent>();
        animation =
        character.GetComponent<CharacterAnimationComponent>();

        attack =
            character.GetComponent<AttackComponent>();
    }

    public void TakeDamage(int damage)
    {
        stats.TakeDamage(damage);
        Console.WriteLine($"Игрок получил {damage} урона. HP: {Health}");
    }

    public void LoadContent(
    GraphicsDevice graphicsDevice,
    ContentManager content,
    string animsFolder)
    {
        string knightFolder = Path.Combine(animsFolder, "Player");

        Texture2D knightTexture =
            content.Load<Texture2D>("Assets/Player/rogue_texture");
        Effect toonEffect = content.Load<Effect>("ToonShader");

        var playerAnims = new Dictionary<string, string>
    {
        { "TPose",  "RogueTPose.fbx"  },
        { "Idle",   "RogueIdle.fbx"   },
        { "Run",    "RogueRun.fbx"    },
        { "Jump",   "RogueJump.fbx"   },
        { "Falling", "RogueFalling.fbx" },
        { "FallingBad", "RogueFallingBad.fbx" },
        { "Landing", "RogueFallingToLanding.fbx" },
        { "Attack", "RogueAttack.fbx" },
        { "FlyKick", "RogueFlyKick.fbx" },
        { "Hurt",   "RogueHurt.fbx"   },
        { "Die",    "RogueDeath.fbx"  }
    };

        character.LoadContent(
            graphicsDevice,
            knightFolder,
            playerAnims,
            knightTexture,
            toonEffect);

        attack.SetDuration(animation.GetClipDuration("Attack"));
        flyKick.SetDuration(animation.GetClipDuration("FlyKick"));
        landingAnimationDuration = animation.GetClipDuration("Landing");

        float jumpClipDuration = animation.GetClipDuration("Jump");
        float jumpRiseDuration =
            character.CalculateJumpRiseDuration(JumpImpulse);

        if (jumpRiseDuration > 0f)
            jumpAnimationTimeScale = jumpClipDuration / jumpRiseDuration;
    }

    public void Update(
    GameTime gameTime,
    KeyboardState keyboard,
    float cameraYaw,
    Level level,
    bool attackPressed,
    bool flyKickPressed)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        landingAnimationTimeRemaining = MathF.Max(
            0f,
            landingAnimationTimeRemaining - deltaTime);

        if (attackPressed && !IsDead && !flyKick.IsAttacking)
            attack.TryStart();

        if (flyKickPressed && !IsDead && !attack.IsAttacking)
            flyKick.TryStart();

        attack.Update(deltaTime);
        flyKick.Update(deltaTime);

        Vector3 moveDir = Vector3.Zero;
        Vector3 horizontalMovement = Vector3.Zero;

        bool isMovementBlocked = IsDead || flyKick.IsAttacking;
        bool isJumpBlocked = isMovementBlocked || attack.IsAttacking;

        // 1. Обычное движение WASD (если не заблокировано)
        if (!isMovementBlocked)
        {
            if (keyboard.IsKeyDown(Keys.W)) moveDir.Z = -1;
            if (keyboard.IsKeyDown(Keys.S)) moveDir.Z = 1;
            if (keyboard.IsKeyDown(Keys.A)) moveDir.X = -1;
            if (keyboard.IsKeyDown(Keys.D)) moveDir.X = 1;

            if (moveDir != Vector3.Zero)
            {
                moveDir.Normalize();
                Matrix cameraRotation = Matrix.CreateRotationY(cameraYaw);
                Vector3 rotatedDir = Vector3.TransformNormal(moveDir, cameraRotation);

                horizontalMovement = rotatedDir * Speed * deltaTime;
                character.RotationY = MathF.Atan2(rotatedDir.X, rotatedDir.Z);
            }
        }
        // 2. Небольшой пролет вперед во время FlyKick
        else if (flyKick.IsAttacking)
        {
            // Вычисляем направление взгляда игрока
            Vector3 forward = new Vector3(MathF.Sin(RotationY), 0f, MathF.Cos(RotationY));

            float flyKickDashSpeed = Speed * 1.2f;
            horizontalMovement = forward * flyKickDashSpeed * deltaTime;
        }

        bool isMoving = moveDir != Vector3.Zero;

        // Прыжок доступен только когда управление не заблокировано
        if (!isJumpBlocked &&
            keyboard.IsKeyDown(Keys.Space) &&
            previousKeyboard.IsKeyUp(Keys.Space))
        {
            character.TryJump(JumpImpulse);
        }

        character.Move(level, horizontalMovement, deltaTime);

        bool landedThisFrame = wasAirborne && character.IsGrounded;

        if (landedThisFrame)
        {
            fallingDuration = 0f;
            landingAnimationTimeRemaining = landingAnimationDuration;
        }
        else if (!character.IsGrounded && character.VerticalVelocity <= 0f)
        {
            fallingDuration += deltaTime;
        }
        else
        {
            fallingDuration = 0f;
        }

        wasAirborne = !character.IsGrounded;

        if (isMoving && landingAnimationTimeRemaining > 0f)
        {
            landingAnimationTimeRemaining = MathF.Min(
                landingAnimationTimeRemaining,
                MovingLandingReleaseTime);
        }

        // Управление анимациями
        float animationTimeScale = 1f;

        if (IsDead)
        {
            character.Play("Die", loop: false);
        }
        else if (flyKick.IsAttacking)
        {
            character.Play("FlyKick", loop: false);
        }
        else if (attack.IsAttacking)
        {
            character.Play("Attack", loop: false);
        }
        else if (!character.IsGrounded && character.VerticalVelocity > 0f)
        {
            character.Play("Jump", loop: false);
            animationTimeScale = jumpAnimationTimeScale;
        }
        else if (!character.IsGrounded)
        {
            string fallingClip = fallingDuration >= BadFallThreshold
                ? "FallingBad"
                : "Falling";

            character.Play(fallingClip, loop: true);
        }
        else if (landingAnimationTimeRemaining > 0f)
        {
            character.Play("Landing", loop: false);
        }
        else if (isMoving)
        {
            character.Play("Run", loop: true);
        }
        else
        {
            character.Play("Idle", loop: true);
        }

        character.UpdateAnimation(deltaTime * animationTimeScale);

        previousKeyboard = keyboard;
    }

    public void Draw(Matrix view, Matrix projection)
    {
        character.Draw(view, projection);
    }
}
