using Ars3D.Items;
using Microsoft.Xna.Framework;

namespace _3DLight.Items;

// Borrowed model: the catalog owns its GPU resources and can share it between instances.
internal sealed class ItemModelRenderer(CompiledModel model, Matrix normalization)
{
    public CompiledModel Model => model;

    public void Draw(ItemPose pose, Matrix view, Matrix projection, SceneLighting lighting) =>
        model.Draw(normalization * ItemMath.ToGame(pose.ToMatrix()), view, projection, lighting);
}
