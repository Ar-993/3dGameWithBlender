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

    public Level.Platform? CurrentPlatform => character.CurrentPlatform;

    private KeyboardState previousKeyboard;

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

        if (attackPressed && !IsDead && !flyKick.IsAttacking)
            attack.TryStart();

        if (flyKickPressed && !IsDead && !attack.IsAttacking)
            flyKick.TryStart();

        attack.Update(deltaTime);
        flyKick.Update(deltaTime);

        Vector3 moveDir = Vector3.Zero;
        Vector3 horizontalMovement = Vector3.Zero;

        bool isMovementBlocked = IsDead || attack.IsAttacking || flyKick.IsAttacking;

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
        if (!isMovementBlocked &&
            keyboard.IsKeyDown(Keys.Space) &&
            previousKeyboard.IsKeyUp(Keys.Space))
        {
            character.TryJump(JumpImpulse);
        }

        character.Move(level, horizontalMovement, deltaTime);

        // Управление анимациями
        if (IsDead)
        {
            character.Play("Die", loop: false, deltaTime);
        }
        else if (flyKick.IsAttacking)
        {
            character.Play("FlyKick", loop: false, deltaTime);
        }
        else if (attack.IsAttacking)
        {
            character.Play("Attack", loop: false, deltaTime);
        }
        else if (!character.IsGrounded)
        {
            character.Play("Jump", loop: false, deltaTime);
        }
        else if (isMoving)
        {
            character.Play("Run", loop: true, deltaTime);
        }
        else
        {
            character.Play("Idle", loop: true, deltaTime);
        }

        previousKeyboard = keyboard;
    }

    public void Draw(Matrix view, Matrix projection, Effect? customEffect = null)
    {
        character.Draw(view, projection, customEffect);
    }
}
