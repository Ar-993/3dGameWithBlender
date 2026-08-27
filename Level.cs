using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace _3DLight
{
    public class Level
    {
        private CompiledModel? model;
        private readonly List<Platform> platforms = [];

        public sealed record Platform(int Id, string Name, BoundingBox Bounds)
        {
            public float SurfaceY => Bounds.Max.Y;

            public bool ContainsHorizontal(Vector3 position, float margin = 0f) =>
                position.X >= Bounds.Min.X + margin && position.X <= Bounds.Max.X - margin &&
                position.Z >= Bounds.Min.Z + margin && position.Z <= Bounds.Max.Z - margin;
        }

        public void LoadContent(
            ContentManager content,
            string modelPath,
            string texturePath = "Assets/level.fbm/palette_0")
        {
            var graphicsService = (IGraphicsDeviceService?)
                content.ServiceProvider.GetService(typeof(IGraphicsDeviceService));

            GraphicsDevice graphicsDevice = graphicsService?.GraphicsDevice
                ?? throw new InvalidOperationException("GraphicsDevice unavailable.");

            Texture2D texture = content.Load<Texture2D>(texturePath);
            Effect toonEffect = content.Load<Effect>("ToonShader");
            ReplaceModel(CompiledModel.Load(
                graphicsDevice,
                modelPath,
                texture,
                toonEffect));
        }

        public void ReloadContent(
            ContentManager content,
            byte[] modelBytes,
            string texturePath = "Assets/level.fbm/palette_0")
        {
            var graphicsService = (IGraphicsDeviceService?)
                content.ServiceProvider.GetService(typeof(IGraphicsDeviceService));

            GraphicsDevice graphicsDevice = graphicsService?.GraphicsDevice
                ?? throw new InvalidOperationException("GraphicsDevice unavailable.");

            Texture2D texture = content.Load<Texture2D>(texturePath);
            Effect toonEffect = content.Load<Effect>("ToonShader");

            ReplaceModel(CompiledModel.LoadFromBytes(
                graphicsDevice,
                modelBytes,
                texture,
                toonEffect));
        }

        private void ReplaceModel(CompiledModel replacement)
        {
            List<Platform> replacementPlatforms;

            try
            {
                replacementPlatforms = replacement.BuildPlatforms();
            }
            catch
            {
                replacement.Dispose();
                throw;
            }

            CompiledModel? previousModel = model;
            model = replacement;
            platforms.Clear();
            platforms.AddRange(replacementPlatforms);
            previousModel?.Dispose();
        }

        public bool TryGetMarkerPosition(string markerName, out Vector3 position)
        {
            if (model is not null)
                return model.TryGetNodePosition(markerName, out position);

            position = default;
            return false;
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

        public Vector3 ResolveCameraPosition(
            Vector3 target,
            Vector3 desiredPosition,
            float cameraRadius,
            float minimumDistance)
        {
            Vector3 offset = desiredPosition - target;
            float desiredDistance = offset.Length();

            if (desiredDistance <= 0.0001f)
                return target;

            Vector3 direction = offset / desiredDistance;
            var ray = new Ray(target, direction);
            float allowedDistance = desiredDistance;

            foreach (Platform platform in platforms)
            {
                BoundingBox bounds = platform.Bounds;
                var expandedBounds = new BoundingBox(
                    bounds.Min - new Vector3(cameraRadius),
                    bounds.Max + new Vector3(cameraRadius));

                float? hitDistance = ray.Intersects(expandedBounds);

                if (hitDistance is { } distance &&
                    distance >= 0f &&
                    distance < allowedDistance)
                {
                    allowedDistance = distance;
                }
            }

            // Небольшой отступ не даёт near plane камеры войти в стену.
            allowedDistance = MathHelper.Clamp(
                allowedDistance - 0.05f,
                minimumDistance,
                desiredDistance);
            return target + direction * allowedDistance;
        }

        public void Draw(Matrix view, Matrix projection)
        {
            model?.Draw(Matrix.Identity, view, projection);
        }

    }
}
