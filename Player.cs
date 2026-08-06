using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

public class Player
{
    public Vector3 Position { get; set; } = new Vector3(830f, 20f, 50f);
    public float RotationY { get; set; }
    public float Speed { get; set; } = 6f;

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

        // Читаем WASD
        if (keyboard.IsKeyDown(Keys.W)) moveDir.Z = -1;
        if (keyboard.IsKeyDown(Keys.S)) moveDir.Z = 1;
        if (keyboard.IsKeyDown(Keys.A)) moveDir.X = -1;
        if (keyboard.IsKeyDown(Keys.D)) moveDir.X = 1;

        if (moveDir != Vector3.Zero)
        {
            moveDir.Normalize();

            // Поворачиваем направление движения относительно того, куда смотрит камера
            Matrix cameraRotation = Matrix.CreateRotationY(cameraYaw);
            Vector3 rotatedDir = Vector3.TransformNormal(moveDir, cameraRotation);

            Position += rotatedDir * Speed * deltaTime;
            RotationY = MathF.Atan2(rotatedDir.X, rotatedDir.Z);

            animator.Play("Run", loop: true);
        }
        else
        {
            animator.Play("Idle", loop: true);
        }

        animator.Update(deltaTime);
    }

    public void Draw(Matrix view, Matrix projection)
    {
        Matrix world = Matrix.CreateRotationY(RotationY) * Matrix.CreateTranslation(Position);
        animator.Draw(world, view, projection);
    }
}