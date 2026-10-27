using _3DLight.Gameplay;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace _3DLight.Scenes;

internal sealed class GameplayScene : IGameScene
{
    private readonly Game game;
    private readonly GameplayInput input;
    private readonly GameplayHud hud;
    private ContentManager? content;
    private GameplayWorld world = null!;
    private GameplayRenderer renderer = null!;
    private bool disposed;

    public GameplayScene(Game game)
    {
        this.game = game;
        input = new GameplayInput(game);
        hud = new GameplayHud(game);
    }

    public void LoadContent()
    {
        // Контент сцены живёт отдельно от Game.Content и выгружается при смене сцены.
        content = new ContentManager(game.Services, game.Content.RootDirectory);
        world = new GameplayWorld(game.GraphicsDevice, content);
        world.LoadContent();
        renderer = new GameplayRenderer(game.GraphicsDevice, content);
    }

    public void Enter()
    {
        hud.Enter();
        GameplayInputFrame initialInput = input.Enter();
        world.SynchronizeInteraction(initialInput.InteractDown);
    }

    public void Exit() => input.Exit();

    public void Update(GameTime gameTime)
    {
        GameplayInputFrame frame = input.Update();
        hud.Update(gameTime, input.IsPaused);
        if (!frame.CanUpdateWorld)
        {
            world.SynchronizeInteraction(frame.InteractDown);
            return;
        }

        world.Update((float)gameTime.ElapsedGameTime.TotalSeconds, frame);
        DebugConsole.Update(gameTime, world.Player, world.Skeleton,
            hud.Fps, hud.WorkingSetMegabytes);
    }

    public void Draw(GameTime gameTime)
    {
        hud.PrepareInventory(world.Items);
        renderer.Draw(world);
        hud.Draw(world.Player, world.Skeleton, world.Items, world.LevelVersion,
            world.Lighting.Enabled, world.ShowCollisionDebug,
            game.IsActive && input.MouseCaptured && !input.IsPaused && !world.Player.IsDead,
            world.Interactions.Hint);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        input.Exit();
        try
        {
            hud.Dispose();
        }
        finally
        {
            try { renderer?.Dispose(); }
            finally
            {
                try { world?.Dispose(); }
                finally { content?.Dispose(); }
            }
        }
    }
}
