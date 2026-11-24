using _3DLight.Gameplay;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using _3DLight.Npcs;

namespace _3DLight.Scenes;

internal sealed class GameplayScene : IGameScene
{
    private readonly Game game;
    private readonly AudioSystem audio;
    private readonly GameplayInput input;
    private readonly GameplayHud hud;
    private ContentManager? content;
    private GameplayWorld world = null!;
    private GameplayRenderer renderer = null!;
    private bool disposed;

    public GameplayScene(Game game, AudioSystem audio)
    {
        this.game = game;
        this.audio = audio;
        input = new GameplayInput(game);
        hud = new GameplayHud(game);
    }

    public void LoadContent()
    {
        // Контент сцены живёт отдельно от Game.Content и выгружается при смене сцены.
        content = new ContentManager(game.Services, game.Content.RootDirectory);
        world = new GameplayWorld(game.GraphicsDevice, content);
        world.LoadContent();
        world.Footsteps.StepPlayed += soundName => audio.Play(soundName);

        renderer = new GameplayRenderer(game.GraphicsDevice, content);
        renderer.PostProcessing.Exposure = 1f;
        renderer.PostProcessing.Saturation = 1f;
        renderer.PostProcessing.VignetteStrength = 0f;
    }

    public void Enter()
    {
        hud.Enter();
        GameplayInputFrame frame = input.Update(world.Dialogue.IsOpen);
        world.SynchronizeInteraction(frame.InteractDown);
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

        bool canInteract = game.IsActive && input.MouseCaptured && !input.IsPaused &&
            !world.Player.IsDead && !world.Dialogue.IsOpen;
        Vector3? anchor = world.Dialogue.SpeakerAnchor;
        if (canInteract && world.Interactions.Target is NpcTalkTarget target)
            anchor = target.BubbleAnchor;

        hud.SetDialogue(world.Dialogue, anchor is { } point ? ProjectBubble(point) : null);
        hud.Draw(world.Player, world.Skeleton, world.RoundTimer, world.Items, world.LevelVersion,
    world.Lighting.Enabled, world.ShowCollisionDebug, canInteract, world.Interactions.Hint);
    }

    private Vector2? ProjectBubble(Vector3 point)
    {
        var camera = world.Camera;
        if (Vector3.Dot(point - camera.Position, camera.Forward) <= 0f)
            return null;

        var viewport = game.GraphicsDevice.Viewport;
        Vector3 projected = viewport.Project(point, camera.Projection, camera.View, Matrix.Identity);
        float x = projected.X - viewport.X;
        float y = projected.Y - viewport.Y;
        if (projected.Z < 0f || projected.Z > 1f || x < 0f || x >= viewport.Width ||
            y < 0f || y >= viewport.Height)
            return null;

        return new Vector2(x, y);
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
