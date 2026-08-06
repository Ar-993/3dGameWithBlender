using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;

namespace _3DLight
{
    public class Player
    {
        public Vector3 Position = new Vector3(830f, 20f, 50f);
        public float ManualFloorY = 0f;
        public float ModelScale = 0.01f;
        public float RotationY { get; private set; } = 0f;

        private float velocityY = 0f;
        private bool isGrounded = false;
        private const float RunSpeed = 8f;
        private const float JumpForce = 16f;
        private const float Gravity = -28f;

        private readonly PlayerAnimator animator = new();

        public void LoadContent(ContentManager content)
        {
            animator.LoadContent(content);
        }

        public void Update(GameTime gameTime, KeyboardState keyboard, float cameraYaw)
        {
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // --- WASD ДВИЖЕНИЕ ---
            var moveForward = Vector3.Normalize(new Vector3(MathF.Sin(cameraYaw), 0, MathF.Cos(cameraYaw)));
            var moveRight = Vector3.Normalize(Vector3.Cross(moveForward, Vector3.Up));

            var moveDirection = Vector3.Zero;
            if (keyboard.IsKeyDown(Keys.W)) moveDirection += moveForward;
            if (keyboard.IsKeyDown(Keys.S)) moveDirection -= moveForward;
            if (keyboard.IsKeyDown(Keys.D)) moveDirection += moveRight;
            if (keyboard.IsKeyDown(Keys.A)) moveDirection -= moveRight;

            float horizontalSpeed = 0f;
            if (moveDirection != Vector3.Zero)
            {
                moveDirection.Normalize();
                horizontalSpeed = RunSpeed;
                Position += moveDirection * horizontalSpeed * dt;
                RotationY = MathF.Atan2(moveDirection.X, moveDirection.Z);
            }

            // --- ПРЫЖОК ---
            if (keyboard.IsKeyDown(Keys.Space) && isGrounded)
            {
                velocityY = JumpForce;
                isGrounded = false;
            }

            // --- ГРАВИТАЦИЯ ---
            velocityY += Gravity * dt;
            Position += new Vector3(0, velocityY * dt, 0);

            if (Position.Y <= ManualFloorY)
            {
                Position = new Vector3(Position.X, ManualFloorY, Position.Z);
                velocityY = 0f;
                isGrounded = true;
            }

            animator.Update(horizontalSpeed, isGrounded, dt);
        }

        public void Draw(Matrix view, Matrix projection, GameTime gameTime)
        {
            Matrix playerWorld = Matrix.CreateScale(ModelScale) * Matrix.CreateRotationY(RotationY) * Matrix.CreateTranslation(Position);
            animator.Draw(playerWorld, view, projection, gameTime);
        }
    }
}

