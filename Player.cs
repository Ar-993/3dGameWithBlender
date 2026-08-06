using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

public class Player
{
    // Стартовые координаты персонажа
    public Vector3 Position { get; set; } = new Vector3(850f, 17.5f, 45f);
    public float RotationY { get; set; }
    public float Speed { get; set; } = 6f;

    // --- ФИЗИКА И ПРЫЖОК ---
    private float verticalVelocity = 0f;          // Скорость по оси Y
    private const float Gravity = -28f;           // Сила гравитации (падение)
    private const float JumpImpulse = 30f;        // Сила толчка при прыжке
    private const float GroundY = 0f;          // Высота поверхности зеленой платформы
    private bool isGrounded = true;               // Находится ли игрок на земле

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

    public void Update(GameTime gameTime, KeyboardState keyboard, float cameraYaw)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Vector3 moveDir = Vector3.Zero;

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

            Position += rotatedDir * Speed * deltaTime;
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

        // Применяем вертикальную скорость к позиции Y
        Vector3 currentPos = Position;
        currentPos.Y += verticalVelocity * deltaTime;

        // 4. ПРИЗЕМЛЕНИЕ НА ПОЛ
        if (currentPos.Y <= GroundY)
        {
            currentPos.Y = GroundY;
            verticalVelocity = 0f;
            isGrounded = true;
        }

        Position = currentPos;

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