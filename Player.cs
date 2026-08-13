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
    public Vector3 Position => character.Position;
    public float RotationY => character.RotationY;
    public float Speed { get; set; } = 6f;
    public int Health => stats.Health;
    public int MaxHealth => stats.MaxHealth;
    public bool IsDead => stats.IsDead;
    public bool AttackShouldDealDamage => attack.ShouldDealDamage;
    public int AttackDamage => attack.Damage;
    public float AttackRange => attack.Range;

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
        string knightFolder = Path.Combine(animsFolder, "Knight");

        Texture2D knightTexture =
            content.Load<Texture2D>("Assets/Knight/knight_texture");

        var playerAnims = new Dictionary<string, string>
    {
        { "TPose",  "KnightTPose.fbx"  },
        { "Idle",   "KnightIdle.fbx"   },
        { "Run",    "KnightRun.fbx"    },
        { "Jump",   "KnightJump.fbx"   },
        { "Attack", "KnightAttack.fbx" },
        { "Hurt",   "KnightHurt.fbx"   },
        { "Die",    "KnightDie.fbx"    }
    };

        character.LoadContent(
            graphicsDevice,
            knightFolder,
            playerAnims,
            knightTexture);

        attack.SetDuration(animation.GetClipDuration("Attack"));
    }

    public void Update(GameTime gameTime,KeyboardState keyboard,float cameraYaw,Level level,bool attackPressed)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (attackPressed && !IsDead)
            attack.TryStart();

        attack.Update(deltaTime);


        Vector3 moveDir = Vector3.Zero;
        Vector3 horizontalMovement = Vector3.Zero;

        // 1. Считываем движение WASD
        if (keyboard.IsKeyDown(Keys.W)) moveDir.Z = -1;
        if (keyboard.IsKeyDown(Keys.S)) moveDir.Z = 1;
        if (keyboard.IsKeyDown(Keys.A)) moveDir.X = -1;
        if (keyboard.IsKeyDown(Keys.D)) moveDir.X = 1;

        bool isMoving = moveDir != Vector3.Zero;

        if (isMoving)
        {
            moveDir.Normalize();

            // Поворачиваем вектор движения относительно взгляда камеры
            Matrix cameraRotation = Matrix.CreateRotationY(cameraYaw);
            Vector3 rotatedDir = Vector3.TransformNormal(moveDir, cameraRotation);

            horizontalMovement = rotatedDir * Speed * deltaTime;
            character.RotationY = MathF.Atan2(rotatedDir.X, rotatedDir.Z);
        }

        // Движение и прыжок разрешены во время атаки.
        if (!IsDead &&
            keyboard.IsKeyDown(Keys.Space) &&
            previousKeyboard.IsKeyUp(Keys.Space))
            character.TryJump(JumpImpulse);

        character.Move(level, horizontalMovement, deltaTime);

        // 5. УПРАВЛЕНИЕ АНИМАЦИЯМИ
        if (IsDead)
        {
            character.Play("Die", loop: false, deltaTime);
        }
        else if (attack.IsAttacking)
        {
            // Пока Attack перекрывает всё тело, но физика ходьбы и прыжка работает.
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

    public void Draw(Matrix view, Matrix projection)
    {
        character.Draw(view, projection);
    }
}
