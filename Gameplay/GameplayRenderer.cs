using Ars3D.Rendering;
using Camera;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Gameplay;

// Рисует мир и владеет визуальными ресурсами снарядов.
internal sealed class GameplayRenderer : IDisposable
{
    private readonly GraphicsDevice graphicsDevice;
    private readonly ProjectileRenderer projectileRenderer;
    private readonly SceneRenderer sceneRenderer;

    public PostProcessSettings PostProcessing => sceneRenderer.PostProcessing;

    public GameplayRenderer(GraphicsDevice graphicsDevice, ContentManager content)
    {
        this.graphicsDevice = graphicsDevice;
        sceneRenderer = new SceneRenderer(graphicsDevice, content);
        try
        {
            projectileRenderer = new ProjectileRenderer(graphicsDevice, content);
        }
        catch
        {
            sceneRenderer.Dispose();
            throw;
        }
    }

    public void Draw(GameplayWorld world)
    {
        SceneLighting lighting = world.Lighting;
        FirstPersonCamera camera = world.Camera;

        SceneRenderQueue queue = sceneRenderer.Queue;
        queue.Clear();

        if (world.ShowCollisionDebug)
        {
            graphicsDevice.Clear(new Color(25, 30, 40));
            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            world.Level.DrawColliders(camera.View, camera.Projection,
                CreateCharacterBounds(world.Player.Position),
                CreateCharacterBounds(world.Skeleton.Position),
                world.Npcs.Capsules);
            return;
        }

        world.Level.Draw(camera.View, camera.Projection, lighting, queue);
        world.Skeleton.Draw(camera.View, camera.Projection, lighting, queue);
        world.Npcs.Draw(camera.View, camera.Projection, lighting, queue);
        world.Items.Draw(camera.View, camera.Projection, lighting, queue);
        projectileRenderer.Draw(world.Projectiles, camera.View, camera.Projection, lighting, queue);
        sceneRenderer.Draw(camera.View, camera.Projection, lighting);
    }

    private static BoundingBox CreateCharacterBounds(Vector3 position) => new(
        position - new Vector3(GameplayCharacters.CharacterCollisionRadius, 0f,
            GameplayCharacters.CharacterCollisionRadius),
        position + new Vector3(GameplayCharacters.CharacterCollisionRadius,
            GameplayCharacters.CharacterCollisionHeight, GameplayCharacters.CharacterCollisionRadius));

    public void Dispose()
    {
        try { projectileRenderer.Dispose(); }
        finally { sceneRenderer.Dispose(); }
    }
}