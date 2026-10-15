using _3DLight.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public sealed class LightGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private readonly SceneManager scenes = new();

    public LightGame()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true
        };

        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        Window.Title = "Ars3D — First Person";
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        graphics.PreferMultiSampling = true;
        graphics.ApplyChanges();
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        Window.Position = new Point(
            Math.Max(0, (display.Width - Window.ClientBounds.Width) / 2),
            Math.Max(0, (display.Height - Window.ClientBounds.Height) / 2));

        base.Initialize();
        DebugConsole.Open();
    }

    protected override void LoadContent() =>
        scenes.ChangeScene(() => new GameplayScene(this));

    protected override void Update(GameTime gameTime)
    {
        scenes.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        scenes.Draw(gameTime);
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        scenes.Dispose();
        DebugConsole.Close();
        base.UnloadContent();
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        scenes.Dispose();
        DebugConsole.Close();
        base.OnExiting(sender, args);
    }
}
