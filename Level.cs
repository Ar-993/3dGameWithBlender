using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace _3DLight
{
    public class Level
    {
        private Model model = null!;
        private Matrix[] boneTransforms = [];
        private readonly List<Platform> platforms = [];

        public sealed record Platform(int Id, string Name, BoundingBox Bounds)
        {
            public float SurfaceY => Bounds.Max.Y;

            public bool ContainsHorizontal(Vector3 position, float margin = 0f) =>
                position.X >= Bounds.Min.X + margin && position.X <= Bounds.Max.X - margin &&
                position.Z >= Bounds.Min.Z + margin && position.Z <= Bounds.Max.Z - margin;
        }

        public void LoadContent(ContentManager content, string modelPath, string sourceModelPath)
        {
            model = content.Load<Model>(modelPath);
            boneTransforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(boneTransforms);
            platforms.Clear();
            platforms.AddRange(LevelColliderLoader.Load(sourceModelPath));
        }

        public bool TryFindLanding(
            Vector3 previousPosition,
            Vector3 nextPosition,
            out Platform? platform,
            out float surfaceY)
        {
            platform = null;
            surfaceY = 0f;

            foreach (Platform candidate in platforms)
            {
                if (!candidate.ContainsHorizontal(nextPosition))
                    continue;

                float candidateY = candidate.SurfaceY;
                if (previousPosition.Y < candidateY - 0.05f || nextPosition.Y > candidateY)
                    continue;

                if (platform is null || candidateY > surfaceY)
                {
                    platform = candidate;
                    surfaceY = candidateY;
                }
            }

            return platform is not null;
        }

        public void Draw(Matrix view, Matrix projection)
        {
            foreach (var mesh in model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.World = boneTransforms[mesh.ParentBone.Index];
                    effect.View = view;
                    effect.Projection = projection;

                    effect.LightingEnabled = true;
                    effect.PreferPerPixelLighting = true;
                    effect.AmbientLightColor = new Vector3(0.35f);
                    effect.DirectionalLight0.Enabled = true;
                    effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.7f, -1f, -0.4f));
                    effect.DirectionalLight0.DiffuseColor = Vector3.One;
                }
                mesh.Draw();
            }
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

            position.X += movement.X;
            foreach (Platform platform in platforms)
            {
                if (!Overlaps(CreateCharacterBounds(position, radius, height), platform.Bounds))
                    continue;

                position.X = movement.X > 0f
                    ? platform.Bounds.Min.X - radius
                    : platform.Bounds.Max.X + radius;
            }

            position.Z += movement.Z;
            foreach (Platform platform in platforms)
            {
                if (!Overlaps(CreateCharacterBounds(position, radius, height), platform.Bounds))
                    continue;

                position.Z = movement.Z > 0f
                    ? platform.Bounds.Min.Z - radius
                    : platform.Bounds.Max.Z + radius;
            }

            position.Y += movement.Y;
            if (movement.Y < 0f)
            {
                float highestSurface = float.MinValue;
                foreach (Platform platform in platforms)
                {
                    if (!Overlaps(CreateCharacterBounds(position, radius, height), platform.Bounds) ||
                        platform.Bounds.Max.Y <= highestSurface)
                    {
                        continue;
                    }

                    highestSurface = platform.Bounds.Max.Y;
                    groundPlatform = platform;
                }

                if (groundPlatform is not null)
                {
                    position.Y = highestSurface;
                    verticalVelocity = 0f;
                }
            }
            else if (movement.Y > 0f)
            {
                float lowestCeiling = float.MaxValue;
                bool hitCeiling = false;
                foreach (Platform platform in platforms)
                {
                    if (!Overlaps(CreateCharacterBounds(position, radius, height), platform.Bounds) ||
                        platform.Bounds.Min.Y >= lowestCeiling)
                    {
                        continue;
                    }

                    lowestCeiling = platform.Bounds.Min.Y;
                    hitCeiling = true;
                }

                if (hitCeiling)
                {
                    position.Y = lowestCeiling - height;
                    verticalVelocity = 0f;
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


    }
}
