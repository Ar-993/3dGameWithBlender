using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using _3DLight;

public class Player
{
    // Стартовые координаты персонажа
    public Vector3 Position { get; set; } = new Vector3(850f, 17.5f, 45f);
    public float RotationY { get; set; }
    public float Speed { get; set; } = 400f;

    // --- ФИЗИКА И ПРЫЖОК ---
    private float verticalVelocity = 0f;          // Скорость по оси Y
    private const float Gravity = -14f;           // Сила гравитации (падение)
    private const float JumpImpulse = 45f;        // Сила толчка при прыжке
    private const float CollisionRadius = 0.35f;
    private const float CollisionHeight = 1.8f;
    private bool isGrounded;

    public Level.Platform? CurrentPlatform { get; private set; }

    private KeyboardState previousKeyboard;
    private readonly CharacterAnimator animator = new();

    public void LoadContent(GraphicsDevice graphicsDevice, string animsFolder)
    {
        var playerAnims = new Dictionary<string, string>
        {
            { "Idle", "Idle.fbx" },
            { "Run",  "Run.fbx" },
            { "Jump", "Jump.fbx" }
        };

        animator.LoadContent(graphicsDevice, animsFolder, playerAnims);
    }

    public void Update(GameTime gameTime, KeyboardState keyboard, float cameraYaw, Level level)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
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
            RotationY = MathF.Atan2(rotatedDir.X, rotatedDir.Z);
        }

        // 2. ПРЫЖОК НА ПРОБЕЛ
        if (keyboard.IsKeyDown(Keys.Space) && previousKeyboard.IsKeyUp(Keys.Space) && isGrounded)
        {
            verticalVelocity = JumpImpulse;
            isGrounded = false;
        }

        // 3. ГРАВИТАЦИЯ
        verticalVelocity += Gravity * deltaTime;

        Vector3 movement = horizontalMovement;
        movement.Y = verticalVelocity * deltaTime;
        Position = level.MoveCharacter(
            Position,
            movement,
            CollisionRadius,
            CollisionHeight,
            ref verticalVelocity,
            out Level.Platform? platform);

        CurrentPlatform = platform;
        isGrounded = platform is not null;

        // 5. УПРАВЛЕНИЕ АНИМАЦИЯМИ
        if (!isGrounded)
        {
            animator.Play("Jump", loop: false); // Анимация прыжка в воздухе
        }
        else if (isMoving)
        {
            animator.Play("Run", loop: true);   // Бег
        }
        else
        {
            animator.Play("Idle", loop: true);  // Покой
        }

        animator.Update(deltaTime);
        previousKeyboard = keyboard;
    }

    public void Draw(Matrix view, Matrix projection)
    {
        float modelScale = 0.01f;
        Matrix world = Matrix.CreateScale(modelScale) * Matrix.CreateRotationY(RotationY) * Matrix.CreateTranslation(Position);
        animator.Draw(world, view, projection);
    }
}
