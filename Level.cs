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

    }
}
