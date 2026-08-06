using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

public sealed class LightGame : Game
{
    private readonly GraphicsDeviceManager graphics;

    private readonly Level level = new();
    private readonly Player player = new();
    private readonly ThirdPersonCamera camera = new();

    // Инструменты для рисования 2D-текста (Отладчик)
    private SpriteBatch spriteBatch = null!;
    private SpriteFont debugFont = null!;

    private KeyboardState previousKeyboard;
    private bool mouseCaptured = true;

    public LightGame()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true
        };

        // 🎯 Включаем фиксацию на 60 FPS:
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);

        Window.Title = "3D Game — 60 FPS Animations";
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
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
        CaptureMouse();
    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        debugFont = Content.Load<SpriteFont>("DebugFont");

        level.LoadContent(Content, "level");

        // Загружаем анимации персонажа
        player.LoadContent(Content);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        if (keyboard.IsKeyDown(Keys.Escape) && previousKeyboard.IsKeyUp(Keys.Escape))
        {
            mouseCaptured = !mouseCaptured;
            IsMouseVisible = !mouseCaptured;
            if (mouseCaptured) CaptureMouse();
        }

        var windowCenter = new Point(Window.ClientBounds.Width / 2, Window.ClientBounds.Height / 2);

        if (mouseCaptured && IsActive)
        {
            camera.UpdateMouse(windowCenter, Mouse.GetState());
            Mouse.SetPosition(windowCenter.X, windowCenter.Y);
        }

        player.Update(gameTime, keyboard, camera.Yaw);
        camera.UpdateMatrices(player.Position, GraphicsDevice.Viewport.AspectRatio);

        previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(25, 30, 40));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;

        level.Draw(camera.View, camera.Projection);

        // 🎯 Передаем gameTime в игрока!
        player.Draw(camera.View, camera.Projection, gameTime);

        spriteBatch.Begin();
        string debugText = $"PLAYER POS:  X: {player.Position.X:F2}  |  Y: {player.Position.Y:F2}  |  Z: {player.Position.Z:F2}";
        spriteBatch.DrawString(debugFont, debugText, new Vector2(17, 17), Color.Black);
        spriteBatch.DrawString(debugFont, debugText, new Vector2(15, 15), Color.Yellow);
        spriteBatch.End();

        base.Draw(gameTime);
    }

    private void CaptureMouse()
    {
        var center = new Point(Window.ClientBounds.Width / 2, Window.ClientBounds.Height / 2);
        Mouse.SetPosition(center.X, center.Y);
    }
}
