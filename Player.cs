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
    private enum PlayerAnimationState
    {
        TPose,
        Idle,
        Run,
        Jump,
        Falling,
        FallingBad,
        Landing,
        Attack,
        FlyKick,
        Hurt,
        Die
    }

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
    public bool IsAnimationBlocked => isAnimationBlocked;
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
    private bool isAnimationBlocked;

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
        { nameof(PlayerAnimationState.TPose), "RogueTPose.fbx" },
        { nameof(PlayerAnimationState.Idle), "RogueIdle.fbx" },
        { nameof(PlayerAnimationState.Run), "RogueRun.fbx" },
        { nameof(PlayerAnimationState.Jump), "RogueJump.fbx" },
        { nameof(PlayerAnimationState.Falling), "RogueFalling.fbx" },
        { nameof(PlayerAnimationState.FallingBad), "RogueFallingBad.fbx" },
        { nameof(PlayerAnimationState.Landing), "RogueFallingToLanding.fbx" },
        { nameof(PlayerAnimationState.Attack), "RogueAttack.fbx" },
        { nameof(PlayerAnimationState.FlyKick), "RogueFlyKick.fbx" },
        { nameof(PlayerAnimationState.Hurt), "RogueHurt.fbx" },
        { nameof(PlayerAnimationState.Die), "RogueDeath.fbx" }
    };

        character.LoadContent(
            graphicsDevice,
            knightFolder,
            playerAnims,
            knightTexture,
            toonEffect);

        attack.SetDuration(animation.GetClipDuration(
            nameof(PlayerAnimationState.Attack)));
        flyKick.SetDuration(animation.GetClipDuration(
            nameof(PlayerAnimationState.FlyKick)));
        landingAnimationDuration = animation.GetClipDuration(
            nameof(PlayerAnimationState.Landing));

        float jumpClipDuration = animation.GetClipDuration(
            nameof(PlayerAnimationState.Jump));
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
        UpdateAirAnimationState(deltaTime, isMoving);

        PlayerAnimationState animationState = ResolveAnimationState(isMoving);
        float animationTimeScale = PlayAnimation(animationState);
        character.UpdateAnimation(deltaTime * animationTimeScale);

        previousKeyboard = keyboard;
    }

    private void UpdateAirAnimationState(float deltaTime, bool isMoving)
    {
        bool isFalling =
            !character.IsGrounded && character.VerticalVelocity <= 0f;
        bool landedThisFrame = wasAirborne && character.IsGrounded;

        fallingDuration = isFalling
            ? fallingDuration + deltaTime
            : 0f;

        if (landedThisFrame)
            landingAnimationTimeRemaining = landingAnimationDuration;

        if (fallingDuration >= BadFallThreshold)
            isAnimationBlocked = true;

        wasAirborne = !character.IsGrounded;

        if (isMoving && landingAnimationTimeRemaining > 0f)
        {
            landingAnimationTimeRemaining = MathF.Min(
                landingAnimationTimeRemaining,
                MovingLandingReleaseTime);
        }
    }

    private PlayerAnimationState ResolveAnimationState(bool isMoving)
    {
        if (IsDead)
            return PlayerAnimationState.Die;

        if (IsAnimationBlocked)
            return PlayerAnimationState.FallingBad;

        if (flyKick.IsAttacking)
            return PlayerAnimationState.FlyKick;

        if (attack.IsAttacking)
            return PlayerAnimationState.Attack;

        if (!character.IsGrounded && character.VerticalVelocity > 0f)
            return PlayerAnimationState.Jump;

        if (!character.IsGrounded)
            return PlayerAnimationState.Falling;

        if (landingAnimationTimeRemaining > 0f)
            return PlayerAnimationState.Landing;

        return isMoving
            ? PlayerAnimationState.Run
            : PlayerAnimationState.Idle;
    }

    private float PlayAnimation(PlayerAnimationState state)
    {
        bool loop = state is
            PlayerAnimationState.Idle or
            PlayerAnimationState.Run or
            PlayerAnimationState.Falling or
            PlayerAnimationState.FallingBad;

        character.Play(state.ToString(), loop);

        return state == PlayerAnimationState.Jump
            ? jumpAnimationTimeScale
            : 1f;
    }

    public void Draw(Matrix view, Matrix projection)
    {
        character.Draw(view, projection);
    }
}
