using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace _3DLight
{
    public class ThirdPersonCamera
    {
        public Vector3 Position { get; private set; }

        // Поворот вокруг персонажа
        public float Yaw { get; private set; } = 0f;

        // Наклон камеры (в радианах). 0.35f ≈ 20 градусов над персонажем
        public float Pitch { get; private set; } = 0.35f;

        // Расстояние от камеры до персонажа
        public float Distance { get; set; } = 4.5f;

        // Высота точки, на которую смотрит камера (уровень головы/плеч)
        public float TargetHeight { get; set; } = 1.4f;

        public Matrix View { get; private set; }
        public Matrix Projection { get; private set; }

        public void UpdateMouse(Point windowCenter, MouseState mouseState)
        {
            // Поворот влево/вправо
            Yaw -= (mouseState.X - windowCenter.X) * 0.003f;

            // Движение мыши вверх поднимает камеру ВЫШЕ над персонажем
            Pitch += (mouseState.Y - windowCenter.Y) * 0.003f;

            // Ограничиваем наклон: от -5 градусов (чуть ниже ног) до +75 градусов (вид почти сверху)
            Pitch = MathHelper.Clamp(Pitch, MathHelper.ToRadians(-5f), MathHelper.ToRadians(75f));
        }

        public void UpdateMatrices(Vector3 targetPosition, float aspectRatio)
        {
            // Точка, на которую направлен взгляд (центр тела / голова)
            Vector3 targetLookAt = targetPosition + new Vector3(0f, TargetHeight, 0f);

            // Вычисляем офсет камеры над и позади персонажа
            float cosPitch = MathF.Cos(Pitch);
            float sinPitch = MathF.Sin(Pitch);

            Vector3 cameraOffset = new Vector3(
                Distance * MathF.Sin(Yaw) * cosPitch,
                Distance * sinPitch,                  // Положительный Pitch поднимет позицию Y
                Distance * MathF.Cos(Yaw) * cosPitch
            );

            // Позиция камеры = Точка взгляда + Смещение
            Position = targetLookAt + cameraOffset;

            // Строим матрицу вида
            View = Matrix.CreateLookAt(Position, targetLookAt, Vector3.Up);

            // Матрица проекции
            Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(65),
                aspectRatio,
                0.1f, 1000f);
        }
    }
}