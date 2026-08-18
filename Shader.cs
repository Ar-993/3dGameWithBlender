using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

public sealed class Shader
{
    public Effect Effect { get; private set; } = null!;
    public void LoadContent(ContentManager content, string assetName) => Effect = content.Load<Effect>(assetName);
    public void ApplyParameters(Matrix world, Matrix view, Matrix projection, Vector3 cameraPosition, Vector3 lightDirection, Texture2D texture)
    {
        Effect.Parameters["World"]?.SetValue(world);
        Effect.Parameters["View"]?.SetValue(view);
        Effect.Parameters["Projection"]?.SetValue(projection);
        Effect.Parameters["CameraPosition"]?.SetValue(cameraPosition);
        Effect.Parameters["LightDirection"]?.SetValue(lightDirection);
        Effect.Parameters["ModelTexture"]?.SetValue(texture);
    }
}
