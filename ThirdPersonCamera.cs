using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace _3DLight
{
    public class ThirdPersonCamera
    {
        public Vector3 Position { get; private set; }
        public float Yaw { get; private set; } = 0f;
        public float Pitch { get; private set; } = 0.25f;    // Приятный наклон сверху
        public float Distance { get; set; } = 4.5f;          // Оптимальная дистанция
        public float TargetHeight { get; set; } = 1.2f;      // Взгляд в плечи/затылок

        public Matrix View { get; private set; }
        public Matrix Projection { get; private set; }

        public void UpdateMouse(Point windowCenter, MouseState mouseState)
        {
            Yaw -= (mouseState.X - windowCenter.X) * 0.004f;
            Pitch -= (mouseState.Y - windowCenter.Y) * 0.004f;
            Pitch = MathHelper.Clamp(Pitch, 0f, 1f);
        }

        public void UpdateMatrices(Vector3 targetPosition, float aspectRatio)
        {
            var targetLookAt = targetPosition + new Vector3(0f, TargetHeight, 0f);

            var cameraForward = Vector3.Normalize(new Vector3(
                MathF.Sin(Yaw) * MathF.Cos(Pitch),
                MathF.Sin(Pitch),
                MathF.Cos(Yaw) * MathF.Cos(Pitch)));

            Position = targetLookAt - cameraForward * Distance;

            View = Matrix.CreateLookAt(Position, targetLookAt, Vector3.Up);
            Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(70),
                aspectRatio,
                0.1f, 1000f);
        }

    }
}