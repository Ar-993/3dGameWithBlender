using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace _3DLight
{
    public class Level
    {
        // В модели level_one высота одной ступени равна 0.5.
        // Небольшой запас покрывает погрешность координат после импорта.
        private const float MaximumStepHeight = 0.55f;
        private const float MaximumGroundRecoveryDistance = 0.08f;

        private CompiledModel? model;
        private readonly List<Platform> platforms = [];
        private readonly List<StairRamp> stairRamps = [];
        private BasicEffect? collisionDebugEffect;

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

            EnsureCollisionDebugEffect(graphicsDevice);

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

            EnsureCollisionDebugEffect(graphicsDevice);

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
            List<StairRamp> replacementStairRamps;

            try
            {
                replacementPlatforms = replacement.BuildPlatforms();
                replacementStairRamps = BuildStairRamps(replacementPlatforms);
                replacementPlatforms.RemoveAll(IsStairStepPlatform);
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
            stairRamps.Clear();
            stairRamps.AddRange(replacementStairRamps);
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
            Platform? currentPlatform,
            ref float verticalVelocity,
            out Platform? groundPlatform)
        {
            groundPlatform = null;
            bool canUseSteps = currentPlatform is not null;
            bool steppedUp = false;
            StairRamp? activeRamp = FindReachableRamp(
                position,
                movement,
                currentPlatform);

            // 1. Смещение по оси X и разрешение столкновений
            position.X += movement.X;
            BoundingBox boundsX = CreateCharacterBounds(position, radius, height);
            foreach (Platform platform in platforms)
            {
                if (activeRamp is not null &&
                    IsFloorAtRampExit(platform, activeRamp))
                {
                    continue;
                }

                if (Overlaps(boundsX, platform.Bounds))
                {
                    // Проверяем: это низкая ступенька или высокая стена
                    float stepHeight = platform.Bounds.Max.Y - position.Y;

                    if (canUseSteps &&
                        !steppedUp &&
                        stepHeight > 0f &&
                        stepHeight <= MaximumStepHeight)
                    {
                        // Шагаем вверх: поднимаем позицию игрока на уровень поверхности ступеньки
                        position.Y = platform.Bounds.Max.Y;
                        steppedUp = true;
                        boundsX = CreateCharacterBounds(position, radius, height);
                    }
                    else
                    {
                        // Это высокая стена — блокируем движение по X
                        if (movement.X > 0f)
                            position.X = platform.Bounds.Min.X - radius;
                        else if (movement.X < 0f)
                            position.X = platform.Bounds.Max.X + radius;

                        boundsX = CreateCharacterBounds(position, radius, height);
                    }
                }
            }

            // 2. Смещение по оси Z и разрешение столкновений
            position.Z += movement.Z;
            BoundingBox boundsZ = CreateCharacterBounds(position, radius, height);
            foreach (Platform platform in platforms)
            {
                if (activeRamp is not null &&
                    IsFloorAtRampExit(platform, activeRamp))
                {
                    continue;
                }

                if (Overlaps(boundsZ, platform.Bounds))
                {
                    float stepHeight = platform.Bounds.Max.Y - position.Y;

                    if (canUseSteps &&
                        !steppedUp &&
                        stepHeight > 0f &&
                        stepHeight <= MaximumStepHeight)
                    {
                        // Шагаем вверх
                        position.Y = platform.Bounds.Max.Y;
                        steppedUp = true;
                        boundsZ = CreateCharacterBounds(position, radius, height);
                    }
                    else
                    {
                        // Это высокая стена — блокируем движение по Z
                        if (movement.Z > 0f)
                            position.Z = platform.Bounds.Min.Z - radius;
                        else if (movement.Z < 0f)
                            position.Z = platform.Bounds.Max.Z + radius;

                        boundsZ = CreateCharacterBounds(position, radius, height);
                    }
                }
            }

            if (activeRamp is not null &&
                activeRamp.ContainsHorizontal(position))
            {
                position.Y = activeRamp.GetHeight(position);
                verticalVelocity = 0f;
                groundPlatform = activeRamp.SupportPlatform;
                return position;
            }

            // При спуске удерживаем ноги на ближайшей ступени. Без этого между
            // ступенями персонаж на несколько кадров переходит в состояние падения.
            if (canUseSteps && !steppedUp)
            {
                Platform? stepBelow = FindStepBelow(position);

                if (stepBelow is not null)
                    position.Y = stepBelow.SurfaceY;
            }

            // 3. Смещение по оси Y (падение / прыжок) и проверка приземления/потолка
            StairRamp? landingRamp = movement.Y <= 0f
                ? FindLandingRamp(position, movement.Y)
                : null;

            if (landingRamp is not null)
            {
                position.Y = landingRamp.GetHeight(position);
                verticalVelocity = 0f;
                groundPlatform = landingRamp.SupportPlatform;
                return position;
            }

            float previousFeetY = position.Y;
            float nextFeetY = position.Y + movement.Y;

            if (movement.Y <= 0f)
            {
                // Берём самую высокую поверхность, которую ноги действительно
                // пересекли за этот кадр. Абсолютная высота этажа не важна.
                Platform? landingPlatform = platforms
                    .Where(platform => HorizontallyOverlaps(
                        position,
                        radius,
                        platform.Bounds))
                    .Where(platform =>
                        previousFeetY >= platform.SurfaceY -
                            MaximumGroundRecoveryDistance &&
                        nextFeetY <= platform.SurfaceY)
                    .OrderByDescending(platform => platform.SurfaceY)
                    .FirstOrDefault();

                if (landingPlatform is not null)
                {
                    position.Y = landingPlatform.SurfaceY;
                    verticalVelocity = 0f;
                    groundPlatform = landingPlatform;
                    return position;
                }
            }
            else
            {
                float previousHeadY = previousFeetY + height;
                float nextHeadY = nextFeetY + height;

                // Аналогично ищем ближайшую снизу горизонтальную поверхность,
                // которую пересекла голова, а не условный "потолок этажа".
                Platform? ceilingPlatform = platforms
                    .Where(platform => HorizontallyOverlaps(
                        position,
                        radius,
                        platform.Bounds))
                    .Where(platform =>
                        previousHeadY <= platform.Bounds.Min.Y + 0.001f &&
                        nextHeadY >= platform.Bounds.Min.Y)
                    .OrderBy(platform => platform.Bounds.Min.Y)
                    .FirstOrDefault();

                if (ceilingPlatform is not null)
                {
                    position.Y = ceilingPlatform.Bounds.Min.Y - height;
                    verticalVelocity = 0f;
                    return position;
                }
            }

            position.Y = nextFeetY;

            return position;
        }

        private Platform? FindStepBelow(Vector3 position) =>
            platforms
                .Where(platform =>
                    platform.ContainsHorizontal(position) &&
                    platform.SurfaceY <= position.Y &&
                    position.Y - platform.SurfaceY <= MaximumStepHeight)
                .OrderByDescending(platform => platform.SurfaceY)
                .FirstOrDefault();

        private StairRamp? FindReachableRamp(
            Vector3 position,
            Vector3 movement,
            Platform? currentPlatform)
        {
            if (currentPlatform is null)
                return null;

            var destination = new Vector3(
                position.X + movement.X,
                position.Y,
                position.Z + movement.Z);

            return stairRamps.FirstOrDefault(ramp =>
                ramp.ContainsHorizontal(destination) &&
                MathF.Abs(ramp.GetHeight(destination) - position.Y) <=
                    MaximumStepHeight &&
                (currentPlatform.Id == ramp.SupportPlatform.Id ||
                    ramp.CanEnterThroughEnd(position, destination)));
        }

        private StairRamp? FindLandingRamp(
            Vector3 position,
            float verticalMovement) =>
            stairRamps.FirstOrDefault(ramp =>
            {
                if (!ramp.ContainsHorizontal(position))
                    return false;

                float surfaceY = ramp.GetHeight(position);
                return position.Y >= surfaceY &&
                    position.Y + verticalMovement <= surfaceY;
            });

        private static bool IsFloorAtRampExit(
            Platform platform,
            StairRamp ramp) =>
            IsFloorPlatform(platform.Name) &&
            MathF.Abs(platform.SurfaceY - ramp.MaximumHeight) <= 0.1f;

        private static bool IsFloorPlatform(string platformName) =>
            platformName.StartsWith("Ground", StringComparison.OrdinalIgnoreCase) ||
            platformName.StartsWith("Floor", StringComparison.OrdinalIgnoreCase) ||
            platformName.StartsWith("Bridge", StringComparison.OrdinalIgnoreCase) ||
            platformName.StartsWith("Trail", StringComparison.OrdinalIgnoreCase) ||
            platformName.Contains("Platform", StringComparison.OrdinalIgnoreCase);

        private static List<StairRamp> BuildStairRamps(
            List<Platform> sourcePlatforms)
        {
            var ramps = new List<StairRamp>();

            foreach (IGrouping<string, Platform> group in sourcePlatforms
                .Where(IsStairStepPlatform)
                .GroupBy(GetStaircaseName))
            {
                List<Platform> steps = group
                    .OrderBy(platform => platform.SurfaceY)
                    .ToList();

                if (steps.Count < 2)
                    continue;

                Platform first = steps[0];
                Platform second = steps[1];
                Vector3 firstCenter = GetHorizontalCenter(first.Bounds);
                Vector3 secondCenter = GetHorizontalCenter(second.Bounds);
                bool runsAlongX = MathF.Abs(secondCenter.X - firstCenter.X) >=
                    MathF.Abs(secondCenter.Z - firstCenter.Z);
                float firstCoordinate = runsAlongX
                    ? firstCenter.X
                    : firstCenter.Z;
                float secondCoordinate = runsAlongX
                    ? secondCenter.X
                    : secondCenter.Z;
                bool risesTowardMinimum = secondCoordinate < firstCoordinate;

                float minimumCoordinate = steps.Min(step => runsAlongX
                    ? step.Bounds.Min.X
                    : step.Bounds.Min.Z);
                float maximumCoordinate = steps.Max(step => runsAlongX
                    ? step.Bounds.Max.X
                    : step.Bounds.Max.Z);
                float crossMinimum = steps.Max(step => runsAlongX
                    ? step.Bounds.Min.Z
                    : step.Bounds.Min.X);
                float crossMaximum = steps.Min(step => runsAlongX
                    ? step.Bounds.Max.Z
                    : step.Bounds.Max.X);

                if (crossMinimum >= crossMaximum)
                    continue;

                float stepRise = steps
                    .Zip(steps.Skip(1), (lower, upper) =>
                        upper.SurfaceY - lower.SurfaceY)
                    .Where(rise => rise > 0.001f)
                    .DefaultIfEmpty(MaximumStepHeight)
                    .Min();
                float minimumHeight = first.SurfaceY - stepRise;
                float maximumHeight = steps[^1].SurfaceY;
                float bottomCoordinate = risesTowardMinimum
                    ? maximumCoordinate
                    : minimumCoordinate;
                float topCoordinate = risesTowardMinimum
                    ? minimumCoordinate
                    : maximumCoordinate;

                ramps.Add(new StairRamp(
                    int.MinValue + ramps.Count,
                    group.Key,
                    runsAlongX,
                    bottomCoordinate,
                    topCoordinate,
                    crossMinimum,
                    crossMaximum,
                    minimumHeight,
                    maximumHeight));
            }

            return ramps;
        }

        private static bool IsStairStepPlatform(Platform platform) =>
            platform.Name.Contains(".Step", StringComparison.OrdinalIgnoreCase);

        private static string GetStaircaseName(Platform platform)
        {
            int stepSuffix = platform.Name.LastIndexOf(
                ".Step",
                StringComparison.OrdinalIgnoreCase);
            return stepSuffix >= 0
                ? platform.Name[..stepSuffix]
                : platform.Name;
        }

        private static Vector3 GetHorizontalCenter(BoundingBox bounds) =>
            new(
                (bounds.Min.X + bounds.Max.X) * 0.5f,
                0f,
                (bounds.Min.Z + bounds.Max.Z) * 0.5f);

        private static bool HorizontallyOverlaps(
            Vector3 position,
            float radius,
            BoundingBox bounds) =>
            position.X - radius < bounds.Max.X &&
            position.X + radius > bounds.Min.X &&
            position.Z - radius < bounds.Max.Z &&
            position.Z + radius > bounds.Min.Z;

        private sealed class StairRamp
        {
            private readonly bool runsAlongX;
            private readonly float bottomCoordinate;
            private readonly float topCoordinate;
            private readonly float crossMinimum;
            private readonly float crossMaximum;
            private readonly float minimumHeight;

            public float MaximumHeight { get; }
            public Platform SupportPlatform { get; }

            public StairRamp(
                int id,
                string name,
                bool runsAlongX,
                float bottomCoordinate,
                float topCoordinate,
                float crossMinimum,
                float crossMaximum,
                float minimumHeight,
                float maximumHeight)
            {
                this.runsAlongX = runsAlongX;
                this.bottomCoordinate = bottomCoordinate;
                this.topCoordinate = topCoordinate;
                this.crossMinimum = crossMinimum;
                this.crossMaximum = crossMaximum;
                this.minimumHeight = minimumHeight;
                MaximumHeight = maximumHeight;

                float axisMinimum = MathF.Min(bottomCoordinate, topCoordinate);
                float axisMaximum = MathF.Max(bottomCoordinate, topCoordinate);
                var minimum = runsAlongX
                    ? new Vector3(axisMinimum, minimumHeight, crossMinimum)
                    : new Vector3(crossMinimum, minimumHeight, axisMinimum);
                var maximum = runsAlongX
                    ? new Vector3(axisMaximum, maximumHeight, crossMaximum)
                    : new Vector3(crossMaximum, maximumHeight, axisMaximum);

                SupportPlatform = new Platform(
                    id,
                    name + ".Ramp",
                    new BoundingBox(minimum, maximum));
            }

            public bool ContainsHorizontal(Vector3 position)
            {
                float coordinate = runsAlongX ? position.X : position.Z;
                float crossCoordinate = runsAlongX ? position.Z : position.X;
                float axisMinimum = MathF.Min(bottomCoordinate, topCoordinate);
                float axisMaximum = MathF.Max(bottomCoordinate, topCoordinate);

                return coordinate >= axisMinimum &&
                    coordinate <= axisMaximum &&
                    crossCoordinate >= crossMinimum &&
                    crossCoordinate <= crossMaximum;
            }

            public float GetHeight(Vector3 position)
            {
                float coordinate = runsAlongX ? position.X : position.Z;
                float amount = (coordinate - bottomCoordinate) /
                    (topCoordinate - bottomCoordinate);
                amount = MathHelper.Clamp(amount, 0f, 1f);
                return MathHelper.Lerp(minimumHeight, MaximumHeight, amount);
            }

            public bool CanEnterThroughEnd(
                Vector3 position,
                Vector3 destination)
            {
                const float endTolerance = 0.05f;

                float currentCoordinate = runsAlongX
                    ? position.X
                    : position.Z;
                float destinationCoordinate = runsAlongX
                    ? destination.X
                    : destination.Z;
                float currentProgress =
                    (currentCoordinate - bottomCoordinate) /
                    (topCoordinate - bottomCoordinate);
                float destinationProgress =
                    (destinationCoordinate - bottomCoordinate) /
                    (topCoordinate - bottomCoordinate);

                bool entersAtBottom =
                    currentProgress <= endTolerance &&
                    destinationProgress > currentProgress;
                bool entersAtTop =
                    currentProgress >= 1f - endTolerance &&
                    destinationProgress < currentProgress;

                return entersAtBottom || entersAtTop;
            }

            public void AppendDebugLines(
                List<VertexPositionColor> destination,
                Color color)
            {
                Vector3 bottomLeft = runsAlongX
                    ? new Vector3(bottomCoordinate, minimumHeight, crossMinimum)
                    : new Vector3(crossMinimum, minimumHeight, bottomCoordinate);
                Vector3 bottomRight = runsAlongX
                    ? new Vector3(bottomCoordinate, minimumHeight, crossMaximum)
                    : new Vector3(crossMaximum, minimumHeight, bottomCoordinate);
                Vector3 topLeft = runsAlongX
                    ? new Vector3(topCoordinate, MaximumHeight, crossMinimum)
                    : new Vector3(crossMinimum, MaximumHeight, topCoordinate);
                Vector3 topRight = runsAlongX
                    ? new Vector3(topCoordinate, MaximumHeight, crossMaximum)
                    : new Vector3(crossMaximum, MaximumHeight, topCoordinate);

                AppendLine(destination, bottomLeft, bottomRight, color);
                AppendLine(destination, topLeft, topRight, color);
                AppendLine(destination, bottomLeft, topLeft, color);
                AppendLine(destination, bottomRight, topRight, color);
                AppendLine(destination, bottomLeft, topRight, color);
                AppendLine(destination, bottomRight, topLeft, color);
            }
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

        public void Draw(
            Matrix view,
            Matrix projection,
            SceneLighting lighting)
        {
            model?.Draw(Matrix.Identity, view, projection, lighting);
        }

        public void DrawColliders(
            Matrix view,
            Matrix projection,
            BoundingBox playerBounds,
            BoundingBox skeletonBounds)
        {
            if (collisionDebugEffect is null)
                return;

            var lines = new List<VertexPositionColor>(
                platforms.Count * 24 + stairRamps.Count * 12 + 48);

            foreach (Platform platform in platforms)
            {
                Color color = IsFloorPlatform(platform.Name)
                    ? Color.LimeGreen
                    : platform.Name.StartsWith(
                        "Wall",
                        StringComparison.OrdinalIgnoreCase)
                        ? Color.CornflowerBlue
                        : Color.Magenta;

                AppendBoundingBoxLines(lines, platform.Bounds, color);
            }

            foreach (StairRamp ramp in stairRamps)
                ramp.AppendDebugLines(lines, Color.Yellow);

            AppendBoundingBoxLines(lines, playerBounds, Color.Red);
            AppendBoundingBoxLines(lines, skeletonBounds, Color.Orange);

            if (lines.Count == 0)
                return;

            collisionDebugEffect.World = Matrix.Identity;
            collisionDebugEffect.View = view;
            collisionDebugEffect.Projection = projection;

            VertexPositionColor[] vertices = lines.ToArray();

            foreach (EffectPass pass in collisionDebugEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                collisionDebugEffect.GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.LineList,
                    vertices,
                    0,
                    vertices.Length / 2);
            }
        }

        private void EnsureCollisionDebugEffect(GraphicsDevice graphicsDevice)
        {
            collisionDebugEffect ??= new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        private static void AppendBoundingBoxLines(
            List<VertexPositionColor> destination,
            BoundingBox bounds,
            Color color)
        {
            Vector3[] corners = bounds.GetCorners();

            AppendLine(destination, corners[0], corners[1], color);
            AppendLine(destination, corners[1], corners[2], color);
            AppendLine(destination, corners[2], corners[3], color);
            AppendLine(destination, corners[3], corners[0], color);
            AppendLine(destination, corners[4], corners[5], color);
            AppendLine(destination, corners[5], corners[6], color);
            AppendLine(destination, corners[6], corners[7], color);
            AppendLine(destination, corners[7], corners[4], color);
            AppendLine(destination, corners[0], corners[4], color);
            AppendLine(destination, corners[1], corners[5], color);
            AppendLine(destination, corners[2], corners[6], color);
            AppendLine(destination, corners[3], corners[7], color);
        }

        private static void AppendLine(
            List<VertexPositionColor> destination,
            Vector3 start,
            Vector3 end,
            Color color)
        {
            destination.Add(new VertexPositionColor(start, color));
            destination.Add(new VertexPositionColor(end, color));
        }

    }
}
