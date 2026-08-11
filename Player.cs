using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using _3DLight;

public class Player
{
    private readonly CharacterFacade character;

    public Vector3 Position => character.Position;
    public float RotationY => character.RotationY;
    public float Speed { get; set; } = 6f;

    private const float JumpImpulse = 30f;

    public Level.Platform? CurrentPlatform => character.CurrentPlatform;

    private KeyboardState previousKeyboard;

    internal Player(CharacterFacade character) => this.character = character;

    public void LoadContent(GraphicsDevice graphicsDevice, string animsFolder)
    {
        var playerAnims = new Dictionary<string, string>
        {
            { "Idle", "Idle.fbx" },
            { "Run",  "Run.fbx" },
            { "Jump", "Jump.fbx" }
        };

        character.LoadContent(graphicsDevice, animsFolder, playerAnims);
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
            character.RotationY = MathF.Atan2(rotatedDir.X, rotatedDir.Z);
        }

        // 2. ПРЫЖОК НА ПРОБЕЛ
        if (keyboard.IsKeyDown(Keys.Space) && previousKeyboard.IsKeyUp(Keys.Space))
            character.TryJump(JumpImpulse);

        character.Move(level, horizontalMovement, deltaTime);

        // 5. УПРАВЛЕНИЕ АНИМАЦИЯМИ
        if (!character.IsGrounded)
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
