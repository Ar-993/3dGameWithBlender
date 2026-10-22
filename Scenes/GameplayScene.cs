using _3DLight.Gameplay;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;

namespace _3DLight.Scenes;

internal sealed class GameplayScene : IGameScene
{
    private readonly Game game;
    private readonly GameplayInput input;
    private readonly GameplayHud hud;
    private ContentManager? content;
    private GameplayWorld world = null!;
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
    }

    public void Enter()
    {
        hud.Enter();
        input.Enter();
        world.SynchronizeInput(Keyboard.GetState());
    }

    public void Exit() => input.Exit();

    public void Update(GameTime gameTime)
    {
        GameplayInputFrame frame = input.Update();
        hud.Update(gameTime, input.IsPaused);
        if (!frame.CanUpdateWorld)
        {
            world.SynchronizeInput(frame.Keyboard);
            return;
        }

        world.Update(gameTime, frame);
        DebugConsole.Update(gameTime, world.Player, world.Skeleton,
            hud.Fps, hud.WorkingSetMegabytes);
    }

    public void Draw(GameTime gameTime)
    {
        hud.PrepareInventory(world.Items);
        world.Draw();
        hud.Draw(world.Player, world.Skeleton, world.Items, world.Session.Version,
            world.Lighting.Enabled, world.ShowCollisionDebug,
            game.IsActive && input.MouseCaptured && !input.IsPaused && !world.Player.IsDead);
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
            try { world?.Dispose(); }
            finally { content?.Dispose(); }
        }
    }
}
