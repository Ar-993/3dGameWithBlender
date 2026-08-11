using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;

public sealed class LightGame : Game
{
    private readonly GraphicsDeviceManager graphics;

    private readonly Level level = new();
    private readonly Player player = new();
    private readonly ThirdPersonCamera camera = new();

    private readonly Skeleton skeleton = new(new Vector3(840f, 17.5f, 40f));

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
        DebugConsole.Open();
        CaptureMouse();
    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        debugFont = Content.Load<SpriteFont>("DebugFont");
        string animsFolder = Path.Combine(AppContext.BaseDirectory, "Content", "Assets");

        string levelSource = Path.Combine(AppContext.BaseDirectory, "Assets", "level.fbx");
        level.LoadContent(Content, "level", levelSource);
        player.LoadContent(GraphicsDevice, animsFolder);
        skeleton.LoadContent(GraphicsDevice, Content, animsFolder);
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboard = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();

        // 1. Если окно игры потеряло фокус — автоматически отпускаем мышь
        if (!IsActive)
        {
            mouseCaptured = false;
            IsMouseVisible = true;
        }

        // 2. Нажатие ESC переключает режим (освободить / захватить)
        if (keyboard.IsKeyDown(Keys.Escape) && previousKeyboard.IsKeyUp(Keys.Escape))
        {
            mouseCaptured = !mouseCaptured;
            IsMouseVisible = !mouseCaptured;

            if (mouseCaptured)
                CaptureMouse();
        }

        // 3. Клик левой кнопкой мыши по окну игры снова активирует захват
        if (!mouseCaptured && IsActive && mouse.LeftButton == ButtonState.Pressed)
        {
            mouseCaptured = true;
            IsMouseVisible = false;
            CaptureMouse();
        }

        // 4. Вращаем камеру ТОЛЬКО когда мышь захвачена и окно активно
        if (mouseCaptured && IsActive)
        {
            var windowCenter = new Point(Window.ClientBounds.Width / 2, Window.ClientBounds.Height / 2);
            camera.UpdateMouse(windowCenter, mouse);
            Mouse.SetPosition(windowCenter.X, windowCenter.Y);
        }

        // Обновление игрока и скелета
        player.Update(gameTime, keyboard, camera.Yaw, level);
        camera.UpdateMatrices(player.Position, GraphicsDevice.Viewport.AspectRatio);

        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        skeleton.Update(player, level, deltaTime);
        DebugConsole.Update(gameTime, player, skeleton);

        previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(25, 30, 40));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;

        level.Draw(camera.View, camera.Projection);
        player.Draw(camera.View, camera.Projection);
        skeleton.Draw(camera.View, camera.Projection);

        // 🟢 ДЕБАГ: Печатаем координаты игрока И скелета
        spriteBatch.Begin();
        string playerPlatform = player.CurrentPlatform?.Id.ToString() ?? "AIR";
        string skeletonPlatform = skeleton.CurrentPlatform?.Id.ToString() ?? "AIR";
        string playerText = $"PLAYER POS:   X: {player.Position.X:F2} | Y: {player.Position.Y:F2} | Z: {player.Position.Z:F2} | PLATFORM: {playerPlatform}";
        string skelText = $"SKELETON POS: X: {skeleton.Position.X:F2} | Y: {skeleton.Position.Y:F2} | Z: {skeleton.Position.Z:F2} | PLATFORM: {skeletonPlatform}";

        spriteBatch.DrawString(debugFont, playerText, new Vector2(15, 15), Color.Yellow);
        spriteBatch.DrawString(debugFont, skelText, new Vector2(15, 35), Color.LawnGreen);
        spriteBatch.End();

        base.Draw(gameTime);
    }

    private void CaptureMouse()
    {
        var center = new Point(Window.ClientBounds.Width / 2, Window.ClientBounds.Height / 2);
        Mouse.SetPosition(center.X, center.Y);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        DebugConsole.Close();
        base.OnExiting(sender, args);
    }
}
