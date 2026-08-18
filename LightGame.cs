using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;

public sealed class LightGame : Game
{
    private const float Gravity = -28f;
    private const float CharacterCollisionRadius = 0.35f;
    private const float CharacterCollisionHeight = 1.8f;

    private readonly GraphicsDeviceManager graphics;

    private readonly Level level = new();
    private readonly Player player = new(
    new CharacterFacade(
        CharacterFactory.Create(
            new CharacterPhysicsComponent(
                new Vector3(850f, 17.5f, 45f),
                CharacterCollisionRadius,
                CharacterCollisionHeight,
                Gravity),
            new CharacterAnimationComponent(modelScale: 1f),
            new StatsComponent(maxHealth: 100),
            new AttackComponent(
                damage: 25,
                range: 1.6f,
                hitTimeNormalized: 0.45f))));
    private readonly ThirdPersonCamera camera = new();

    private readonly Skeleton skeleton = new(new CharacterFacade(
        CharacterFactory.Create(
            new CharacterPhysicsComponent(
                new Vector3(840f, 17.5f, 40f),
                CharacterCollisionRadius,
                CharacterCollisionHeight,
                Gravity),

            new CharacterAnimationComponent(modelScale: 1f),

            new StatsComponent(maxHealth: 100),

            new SkeletonAIComponent(),
            new AttackComponent(
        damage: 10,
        range: 1.4f,
        hitTimeNormalized: 0.45f))));

    private SpriteBatch spriteBatch = null!;
    private SpriteFont debugFont = null!;

    private RenderTarget2D renderTarget = null!;
    private Effect postProcessEffect = null!;

    private KeyboardState previousKeyboard;
    private MouseState previousMouse;
    private bool mouseCaptured;
    private bool wasActive;

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
        wasActive = IsActive;
        CaptureMouse();
    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        debugFont = Content.Load<SpriteFont>("DebugFont");
        string animsFolder = Path.Combine(AppContext.BaseDirectory, "Content", "Assets");

        renderTarget = new RenderTarget2D(
            GraphicsDevice,
            graphics.PreferredBackBufferWidth,
            graphics.PreferredBackBufferHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24);

        string levelSource = Path.Combine(AppContext.BaseDirectory, "Assets", "level.fbx");
        level.LoadContent(Content, "level", levelSource);
        player.LoadContent(
    GraphicsDevice,
    Content,
    animsFolder);
        skeleton.LoadContent(GraphicsDevice, Content, animsFolder);

        // ⭐ Загружаем Shader
        try
        {
            postProcessEffect = Content.Load<Effect>("Shader");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Не удалось загрузить шейдер: {ex.Message}");
            postProcessEffect = null!;
        }
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboard = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();
        bool capturedThisFrame = false;
        bool justActivated = IsActive && !wasActive;

        // Потеря фокуса всегда освобождает курсор и приостанавливает управление.
        if (!IsActive)
        {
            ReleaseMouse();
            previousKeyboard = keyboard;
            previousMouse = mouse;
            wasActive = false;
            base.Update(gameTime);
            return;
        }

        // Escape только освобождает мышь. Повторное нажатие ничего не закрывает.
        if (keyboard.IsKeyDown(Keys.Escape) && previousKeyboard.IsKeyUp(Keys.Escape))
            ReleaseMouse();

        // Захватываем только по новому клику, а не по кнопке, зажатой во время Alt+Tab.
        if (!mouseCaptured &&
            !justActivated &&
            IsInsideClient(mouse) &&
            mouse.LeftButton == ButtonState.Pressed &&
            previousMouse.LeftButton == ButtonState.Released)
        {
            CaptureMouse();
            capturedThisFrame = true;
        }

        // В кадр захвата пропускаем смещение, потому что MouseState ещё хранит старую позицию.
        if (mouseCaptured)
        {
            var windowCenter = new Point(Window.ClientBounds.Width / 2, Window.ClientBounds.Height / 2);
            if (!capturedThisFrame)
                camera.UpdateMouse(windowCenter, mouse);
            Mouse.SetPosition(windowCenter.X, windowCenter.Y);
        }

        // Клик, которым мышь только захватывается, не считается атакой.
        bool attackPressed =
            mouseCaptured &&
            !capturedThisFrame &&
            mouse.LeftButton == ButtonState.Pressed &&
            previousMouse.LeftButton == ButtonState.Released;

        bool flyKickPressed =
            mouseCaptured &&
            !capturedThisFrame &&
            mouse.RightButton == ButtonState.Pressed &&
            previousMouse.RightButton == ButtonState.Released;

        // Обновление игрока и скелета
        player.Update(
            gameTime,
            keyboard,
            camera.Yaw,
            level,
            attackPressed,
            flyKickPressed);

        if (player.AttackShouldDealDamage && !skeleton.IsDead)
        {
            Vector3 difference = skeleton.Position - player.Position;
            difference.Y = 0f;

            if (difference.Length() <= player.AttackRange)
                skeleton.TakeDamage(player.AttackDamage);
        }

        camera.UpdateMatrices(player.Position, GraphicsDevice.Viewport.AspectRatio);

        // ⭐ Устанавливаем параметры Toon Shader
        if (postProcessEffect != null)
        {
            postProcessEffect.Parameters["CameraPosition"]?.SetValue(camera.Position);
            postProcessEffect.Parameters["LightDirection"]?.SetValue(
                Vector3.Normalize(new Vector3(-0.7f, -1f, -0.4f)));
        }

        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        skeleton.Update(player, level, deltaTime);
        DebugConsole.Update(gameTime, player, skeleton);

        previousKeyboard = keyboard;
        previousMouse = mouse;
        wasActive = true;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(renderTarget);
        GraphicsDevice.Clear(new Color(25, 30, 40));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;

        level.Draw(camera.View, camera.Projection, postProcessEffect);
        player.Draw(camera.View, camera.Projection, postProcessEffect);
        skeleton.Draw(camera.View, camera.Projection, postProcessEffect);

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);

        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, postProcessEffect);
        spriteBatch.Draw(renderTarget, Vector2.Zero, Color.White);
        spriteBatch.End();

        spriteBatch.Begin();
        string playerPlatform = player.CurrentPlatform?.Id.ToString() ?? "AIR";
        string skeletonPlatform = skeleton.CurrentPlatform?.Id.ToString() ?? "AIR";
        string playerText = $"PLAYER HP: {player.Health}/{player.MaxHealth} | X: {player.Position.X:F2} | Y: {player.Position.Y:F2} | Z: {player.Position.Z:F2} | PLATFORM: {playerPlatform}";
        string skelText = $"SKELETON HP: {skeleton.Health}/{skeleton.MaxHealth} | X: {skeleton.Position.X:F2} | Y: {skeleton.Position.Y:F2} | Z: {skeleton.Position.Z:F2} | PLATFORM: {skeletonPlatform}";
        Color skeletonDebugColor = skeleton.IsDead ? Color.Gray : Color.LawnGreen;

        spriteBatch.DrawString(debugFont, playerText, new Vector2(15, 15), Color.Yellow);
        spriteBatch.DrawString(debugFont, skelText, new Vector2(15, 35), skeletonDebugColor);
        spriteBatch.End();

        base.Draw(gameTime);
    }

    private void CaptureMouse()
    {
        mouseCaptured = true;
        IsMouseVisible = false;
        var center = new Point(Window.ClientBounds.Width / 2, Window.ClientBounds.Height / 2);
        Mouse.SetPosition(center.X, center.Y);
    }

    private void ReleaseMouse()
    {
        mouseCaptured = false;
        IsMouseVisible = true;
    }

    private bool IsInsideClient(MouseState mouse) =>
        mouse.X >= 0 && mouse.X < Window.ClientBounds.Width &&
        mouse.Y >= 0 && mouse.Y < Window.ClientBounds.Height;

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        DebugConsole.Close();
        base.OnExiting(sender, args);
    }
}