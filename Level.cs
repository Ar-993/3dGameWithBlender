using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace _3DLight
{
    public partial class Level
    {
        // В модели level_one высота одной ступени равна 0.5.
        // Небольшой запас покрывает погрешность координат после импорта.
        private const float MaximumStepHeight = 0.55f;
        private const float MinimumGroundNormalY = 0.7f;

        private CompiledModel? model;
        private readonly List<Platform> platforms = [];
        private readonly List<TriangleCollider> meshColliders = [];
        private BasicEffect? collisionDebugEffect;

        public sealed record Platform(int Id, string Name, BoundingBox Bounds)
        {
            public float SurfaceY => Bounds.Max.Y;

            public bool ContainsHorizontal(Vector3 position, float margin = 0f) =>
                position.X >= Bounds.Min.X + margin && position.X <= Bounds.Max.X - margin &&
                position.Z >= Bounds.Min.Z + margin && position.Z <= Bounds.Max.Z - margin;
        }

        public readonly record struct TriangleCollider(
            string Name,
            Vector3 A,
            Vector3 B,
            Vector3 C,
            Vector3 Normal,
            BoundingBox Bounds)
        {
            public Platform? SupportPlatform { get; init; }
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

            ReplaceModel(CompiledModel.LoadFromBytes(
                graphicsDevice,
                modelBytes,
                modelPath,
                texture,
                toonEffect));
        }

        private void ReplaceModel(CompiledModel replacement)
        {
            List<Platform> replacementPlatforms;
            List<TriangleCollider> replacementMeshColliders;

            try
            {
                replacementPlatforms = replacement.BuildPlatforms();
                replacementMeshColliders = replacement.BuildTriangleColliders();
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
            meshColliders.Clear();
            meshColliders.AddRange(replacementMeshColliders);
            previousModel?.Dispose();
        }

        public bool TryGetMarkerPosition(string markerName, out Vector3 position)
        {
            if (model is not null)
                return model.TryGetNodePosition(markerName, out position);

            position = default;
            return false;
        }



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
                meshColliders.Count * 6 + 600);

            foreach (TriangleCollider triangle in meshColliders)
            {
                Color color = MathF.Abs(triangle.Normal.Y) >= MinimumGroundNormalY
                    ? Color.LimeGreen
                    : Color.CornflowerBlue;
                AppendLine(lines, triangle.A, triangle.B, color);
                AppendLine(lines, triangle.B, triangle.C, color);
                AppendLine(lines, triangle.C, triangle.A, color);
            }

            AppendCapsuleLines(lines, playerBounds, Color.Red);
            AppendCapsuleLines(lines, skeletonBounds, Color.Orange);

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

        private static void AppendCapsuleLines(
            List<VertexPositionColor> destination,
            BoundingBox bounds,
            Color color)
        {
            float radius = (bounds.Max.X - bounds.Min.X) * 0.5f;
            Vector3 feet = new((bounds.Min.X + bounds.Max.X) * 0.5f,
                bounds.Min.Y, (bounds.Min.Z + bounds.Max.Z) * 0.5f);
            CapsuleShape capsule = new(feet, radius, bounds.Max.Y - bounds.Min.Y);
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float first = MathHelper.TwoPi * i / segments;
                float second = MathHelper.TwoPi * (i + 1) / segments;
                Vector3 a = new(MathF.Cos(first) * radius, 0f, MathF.Sin(first) * radius);
                Vector3 b = new(MathF.Cos(second) * radius, 0f, MathF.Sin(second) * radius);
                AppendLine(destination, capsule.AxisStart + a, capsule.AxisStart + b, color);
                AppendLine(destination, capsule.AxisEnd + a, capsule.AxisEnd + b, color);
            }
            for (int meridian = 0; meridian < 4; meridian++)
            {
                float angle = meridian * MathHelper.PiOver2;
                Vector3 radial = new(MathF.Cos(angle), 0f, MathF.Sin(angle));
                AppendLine(destination, capsule.AxisStart + radial * radius,
                    capsule.AxisEnd + radial * radius, color);
                for (int i = 0; i < 12; i++)
                {
                    float a = MathHelper.PiOver2 * i / 12f;
                    float b = MathHelper.PiOver2 * (i + 1) / 12f;
                    Vector3 first = radius * (radial * MathF.Cos(a) + Vector3.Up * MathF.Sin(a));
                    Vector3 second = radius * (radial * MathF.Cos(b) + Vector3.Up * MathF.Sin(b));
                    AppendLine(destination, capsule.AxisEnd + first, capsule.AxisEnd + second, color);
                    first.Y = -first.Y;
                    second.Y = -second.Y;
                    AppendLine(destination, capsule.AxisStart + first, capsule.AxisStart + second, color);
                }
            }
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
