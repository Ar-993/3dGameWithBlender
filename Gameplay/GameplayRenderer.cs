using Ars3D.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Gameplay;

// Рисует мир и владеет визуальными ресурсами снарядов.
internal sealed class GameplayRenderer : IDisposable
{
    private readonly GraphicsDevice graphicsDevice;
    private readonly ProjectileRenderer projectileRenderer;

    public GameplayRenderer(GraphicsDevice graphicsDevice, ContentManager content)
    {
        this.graphicsDevice = graphicsDevice;
        projectileRenderer = new ProjectileRenderer(graphicsDevice, content);
    }

    public void Draw(GameplayWorld world)
    {
        SceneLighting lighting = world.Lighting;
        FirstPersonCamera camera = world.Camera;

        graphicsDevice.Clear(lighting.Fog.Enabled ? lighting.Fog.Color : new Color(25, 30, 40));
        graphicsDevice.DepthStencilState = DepthStencilState.Default;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;

        if (world.ShowCollisionDebug)
        {
            world.Level.DrawColliders(camera.View, camera.Projection,
                CreateCharacterBounds(world.Player.Position),
                CreateCharacterBounds(world.Skeleton.Position));
            return;
        }

        world.Level.Draw(camera.View, camera.Projection, lighting);
        world.Skeleton.Draw(camera.View, camera.Projection, lighting);
        world.Items.Draw(camera.View, camera.Projection, lighting);
        projectileRenderer.Draw(world.Projectiles, camera.View, camera.Projection, lighting);
    }

    private static BoundingBox CreateCharacterBounds(Vector3 position) => new(
        position - new Vector3(GameplayCharacters.CharacterCollisionRadius, 0f,
            GameplayCharacters.CharacterCollisionRadius),
        position + new Vector3(GameplayCharacters.CharacterCollisionRadius,
            GameplayCharacters.CharacterCollisionHeight, GameplayCharacters.CharacterCollisionRadius));

    public void Dispose() => projectileRenderer.Dispose();
}
