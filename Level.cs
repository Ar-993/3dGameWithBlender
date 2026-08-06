using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight
{
    public class Level
    {
        private Model model = null!;

        private Matrix[] boneTransforms = [];

        public void LoadContent(ContentManager content, string modelPath)
        {
            model = content.Load<Model>(modelPath);
            boneTransforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(boneTransforms);
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
