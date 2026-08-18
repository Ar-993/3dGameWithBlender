using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace _3DLight
{
    public class Level
    {
        private CompiledModel model = null!;
        private readonly List<Platform> platforms = [];

        public sealed record Platform(int Id, string Name, BoundingBox Bounds)
        {
            public float SurfaceY => Bounds.Max.Y;

            public bool ContainsHorizontal(Vector3 position, float margin = 0f) =>
                position.X >= Bounds.Min.X + margin && position.X <= Bounds.Max.X - margin &&
                position.Z >= Bounds.Min.Z + margin && position.Z <= Bounds.Max.Z - margin;
        }

        public void LoadContent(ContentManager content, string modelPath)
        {
            var graphics = (IGraphicsDeviceService?)content.ServiceProvider.GetService(typeof(IGraphicsDeviceService));
            Texture2D texture = content.Load<Texture2D>("Assets/level.fbm/palette_0");
            Effect toonEffect = content.Load<Effect>("ToonShader");
            model = CompiledModel.Load(graphics?.GraphicsDevice ?? throw new InvalidOperationException("GraphicsDevice unavailable."), modelPath, texture, toonEffect);
            platforms.Clear();
            platforms.AddRange(model.BuildPlatforms());
        }

        public Vector3 MoveCharacter(
            Vector3 position,
            Vector3 movement,
            float radius,
            float height,
            ref float verticalVelocity,
            out Platform? groundPlatform)
        {
            groundPlatform = null;

            // 1. Смещение по оси X и разрешение столкновений
            position.X += movement.X;
            BoundingBox boundsX = CreateCharacterBounds(position, radius, height);
            foreach (Platform platform in platforms)
            {
                if (Overlaps(boundsX, platform.Bounds))
                {
                    if (movement.X > 0f)
                        position.X = platform.Bounds.Min.X - radius;
                    else if (movement.X < 0f)
                        position.X = platform.Bounds.Max.X + radius;

                    boundsX = CreateCharacterBounds(position, radius, height);
                }
            }

            // 2. Смещение по оси Z и разрешение столкновений
            position.Z += movement.Z;
            BoundingBox boundsZ = CreateCharacterBounds(position, radius, height);
            foreach (Platform platform in platforms)
            {
                if (Overlaps(boundsZ, platform.Bounds))
                {
                    if (movement.Z > 0f)
                        position.Z = platform.Bounds.Min.Z - radius;
                    else if (movement.Z < 0f)
                        position.Z = platform.Bounds.Max.Z + radius;

                    boundsZ = CreateCharacterBounds(position, radius, height);
                }
            }

            // 3. Смещение по оси Y (падение / прыжок) и проверка приземления/потолка
            position.Y += movement.Y;
            BoundingBox boundsY = CreateCharacterBounds(position, radius, height);

            foreach (Platform platform in platforms)
            {
                if (!Overlaps(boundsY, platform.Bounds))
                    continue;

                // Движение вниз — приземление на верхнюю грань блока
                if (movement.Y <= 0f && (position.Y - movement.Y) >= platform.Bounds.Max.Y - 0.2f)
                {
                    position.Y = platform.Bounds.Max.Y;
                    verticalVelocity = 0f;
                    groundPlatform = platform;
                    boundsY = CreateCharacterBounds(position, radius, height);
                }
                // Движение вверх — удар головой о нижнюю грань блока
                else if (movement.Y > 0f)
                {
                    position.Y = platform.Bounds.Min.Y - height;
                    verticalVelocity = 0f;
                    boundsY = CreateCharacterBounds(position, radius, height);
                }
            }

            return position;
        }

        private static BoundingBox CreateCharacterBounds(Vector3 feetPosition, float radius, float height) =>
            new(
                new Vector3(feetPosition.X - radius, feetPosition.Y, feetPosition.Z - radius),
                new Vector3(feetPosition.X + radius, feetPosition.Y + height, feetPosition.Z + radius));

        private static bool Overlaps(BoundingBox first, BoundingBox second) =>
            first.Min.X < second.Max.X && first.Max.X > second.Min.X &&
            first.Min.Y < second.Max.Y && first.Max.Y > second.Min.Y &&
            first.Min.Z < second.Max.Z && first.Max.Z > second.Min.Z;

        public void Draw(Matrix view, Matrix projection)
        {
            model.Draw(Matrix.Identity, view, projection);
        }

    }
}
