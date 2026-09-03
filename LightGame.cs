using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;
using System.IO;

public sealed class LightGame : Game
{
    private enum SpawnMode
    {
        Coordinates,
        BlenderEmpty
    }

    private const string ActiveLevelModel = "level_one";
    private const string ActiveLevelTexture = "Assets/level_one.fbm/palette_0";
    private static readonly SpawnMode ActivePlayerSpawnMode =
        SpawnMode.BlenderEmpty;
    private const string PlayerSpawnObjectName = "PlayerSpawn";
    private static readonly Vector3 PlayerSpawnCoordinates = new(-1f, 0.03f, -0.06f);
    private static readonly SpawnMode ActiveSkeletonSpawnMode =
        SpawnMode.BlenderEmpty;
    private const string SkeletonSpawnObjectName = "SkeletonSpawn";
    private static readonly Vector3 SkeletonSpawnCoordinates = new(3f, 0.03f, -0.06f);
    private const float Gravity = -28f;
    private const float CharacterCollisionRadius = 0.35f;
    private const float CharacterCollisionHeight = 1.8f;

    private readonly GraphicsDeviceManager graphics;
    private readonly SceneLighting lighting = new();
    private readonly PauseController pause = new();
    private readonly GameUi gameUi = new();

    private readonly Level level = new();
    private readonly Player player = new(
    new CharacterFacade(
        CharacterFactory.Create(
            new CharacterPhysicsComponent(
                PlayerSpawnCoordinates,
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
    private LevelHotReload? levelHotReload;

    private readonly Skeleton skeleton = new(new CharacterFacade(
        CharacterFactory.Create(
            new CharacterPhysicsComponent(
                SkeletonSpawnCoordinates,
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

    private KeyboardState previousKeyboard;
    private MouseState previousMouse;
    private bool mouseCaptured;
    private bool wasActive;
    private long fpsSampleStarted = Stopwatch.GetTimestamp();
    private int framesInSample;
    private double realFps;
    private double workingSetMegabytes;
    private string activeLevelVersion = "base";
    private bool showCollisionDebug;

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
        gameUi.Initialize(this);
        DebugConsole.Open();
        wasActive = IsActive;
        CaptureMouse();
    }

    protected override void LoadContent()
    {
        string animsFolder = Path.Combine(AppContext.BaseDirectory, "Content", "Assets");

        level.LoadContent(
            Content,
            ActiveLevelModel,
            ActiveLevelTexture);
        levelHotReload = LevelHotReload.TryCreate();
        TryLoadLatestLevelVersion();
        SpawnCharacters();
        player.LoadContent(
    GraphicsDevice,
    Content,
    animsFolder);
        skeleton.LoadContent(GraphicsDevice, Content, animsFolder);

    }

    private void SpawnCharacters()
    {
        player.SpawnAt(ResolveSpawnPosition(
            ActivePlayerSpawnMode,
            PlayerSpawnObjectName,
            PlayerSpawnCoordinates,
            "Player"));
        skeleton.SpawnAt(ResolveSpawnPosition(
            ActiveSkeletonSpawnMode,
            SkeletonSpawnObjectName,
            SkeletonSpawnCoordinates,
            "Skeleton"));
    }

    private void TryLoadLatestLevelVersion()
    {
        if (levelHotReload is null)
            return;

        try
        {
            if (levelHotReload.TryTakePendingModel(
                    out byte[] modelBytes,
                    out string version))
            {
                ApplyLevelVersion(modelBytes, version, respawnCharacters: false);
            }
        }
        catch (Exception error)
        {
            Console.WriteLine($"Initial level hot reload failed: {error.Message}");
        }
    }

    private void UpdateLevelHotReload()
    {
        if (levelHotReload is null)
            return;

        try
        {
            if (levelHotReload.TryTakePendingModel(
                    out byte[] modelBytes,
                    out string version))
            {
                ApplyLevelVersion(modelBytes, version, respawnCharacters: true);
            }
        }
        catch (Exception error)
        {
            Console.WriteLine($"Level hot reload failed: {error.Message}");
        }
    }

    private void ApplyLevelVersion(
        byte[] modelBytes,
        string version,
        bool respawnCharacters)
    {
        level.ReloadContent(
            Content,
            modelBytes,
            ActiveLevelModel,
            ActiveLevelTexture);

        if (respawnCharacters)
            SpawnCharacters();

        activeLevelVersion = version;
        Console.WriteLine($"Level hot reload applied: {version}");
    }

    private Vector3 ResolveSpawnPosition(
        SpawnMode mode,
        string objectName,
        Vector3 fallbackCoordinates,
        string characterName)
    {
        if (mode == SpawnMode.Coordinates)
        {
            Console.WriteLine(
                $"{characterName} spawn from coordinates: {fallbackCoordinates}");
            return fallbackCoordinates;
        }

        if (level.TryGetMarkerPosition(objectName, out Vector3 markerPosition))
        {
            Console.WriteLine(
                $"{characterName} spawn from Blender Empty '{objectName}': " +
                markerPosition);
            return markerPosition;
        }

        Console.WriteLine(
            $"Blender Empty '{objectName}' was not found for {characterName}. " +
            $"Using fallback coordinates: {fallbackCoordinates}");
        return fallbackCoordinates;
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboard = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();
        bool capturedThisFrame = false;
        bool justActivated = IsActive && !wasActive;

        gameUi.Update(gameTime);

        // Потеря фокуса всегда освобождает курсор и приостанавливает управление.
        if (!IsActive)
        {
            ReleaseMouse();
            pause.SynchronizeInput(keyboard);
            previousKeyboard = keyboard;
            previousMouse = mouse;
            wasActive = false;
            base.Update(gameTime);
            return;
        }

        PauseTransition pauseTransition = pause.Update(keyboard);

        if (pauseTransition != PauseTransition.None)
        {
            gameUi.SetPaused(pause.IsPaused);

            if (pauseTransition == PauseTransition.Paused)
            {
                ReleaseMouse();
            }
            else
            {
                CaptureMouse();
                capturedThisFrame = true;
            }
        }

        // Пока включена пауза, игровой мир и анимации не обновляются.
        if (pause.IsPaused)
        {
            previousKeyboard = keyboard;
            previousMouse = mouse;
            wasActive = true;
            base.Update(gameTime);
            return;
        }

        UpdateLevelHotReload();

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

            if (difference.Length() <= player.AttackRange)
                skeleton.TakeDamage(player.AttackDamage);
        }

        camera.UpdateMatrices(
            player.Position,
            GraphicsDevice.Viewport.AspectRatio,
            level);

        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        skeleton.Update(player, level, deltaTime);
        lighting.FollowPlayer(player.Position);

        if (keyboard.IsKeyDown(Keys.L) && previousKeyboard.IsKeyUp(Keys.L))
            lighting.Enabled = !lighting.Enabled;

        if (keyboard.IsKeyDown(Keys.O) && previousKeyboard.IsKeyUp(Keys.O))
            showCollisionDebug = !showCollisionDebug;

        DebugConsole.Update(
            gameTime,
            player,
            skeleton,
            realFps,
            workingSetMegabytes);

        previousKeyboard = keyboard;
        previousMouse = mouse;
        wasActive = true;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        UpdatePerformanceMetrics();

        GraphicsDevice.Clear(new Color(25, 30, 40));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;

        if (showCollisionDebug)
        {
            level.DrawColliders(
                camera.View,
                camera.Projection,
                CreateCharacterDebugBounds(player.Position),
                CreateCharacterDebugBounds(skeleton.Position));
        }
        else
        {
            level.Draw(camera.View, camera.Projection, lighting);
            player.Draw(camera.View, camera.Projection, lighting);
            skeleton.Draw(camera.View, camera.Projection, lighting);
        }

        string playerPlatform = player.CurrentPlatform?.Id.ToString() ?? "AIR";
        string skeletonPlatform = skeleton.CurrentPlatform?.Id.ToString() ?? "AIR";
        string playerText =
            $"PLAYER HP: {player.Health}/{player.MaxHealth} | " +
            $"X: {player.Position.X:F2} | " +
            $"Y: {player.Position.Y:F2} | " +
            $"Z: {player.Position.Z:F2} | " +
            $"PLATFORM: {playerPlatform}";

        string skelText =
            $"SKELETON HP: {skeleton.Health}/{skeleton.MaxHealth} | " +
            $"X: {skeleton.Position.X:F2} | " +
            $"Y: {skeleton.Position.Y:F2} | " +
            $"Z: {skeleton.Position.Z:F2} | " +
            $"PLATFORM: {skeletonPlatform}";
        string performanceText =
            $"FPS: {realFps:F1} | RAM: {workingSetMegabytes:F1} MB | " +
            $"LEVEL: {activeLevelVersion} | " +
            $"LIGHT: {(lighting.Enabled ? "ON" : "OFF")} (L) | " +
            $"COLLIDERS: {(showCollisionDebug ? "ON" : "OFF")} (O)";
        gameUi.SetHudText(
            playerText,
            skelText,
            performanceText,
            skeleton.IsDead);
        gameUi.Draw();

        base.Draw(gameTime);
    }

    private static BoundingBox CreateCharacterDebugBounds(Vector3 position) =>
        new(
            new Vector3(
                position.X - CharacterCollisionRadius,
                position.Y,
                position.Z - CharacterCollisionRadius),
            new Vector3(
                position.X + CharacterCollisionRadius,
                position.Y + CharacterCollisionHeight,
                position.Z + CharacterCollisionRadius));

    private void UpdatePerformanceMetrics()
    {
        const double sampleDurationSeconds = 0.5;

        framesInSample++;
        long now = Stopwatch.GetTimestamp();
        double elapsedSeconds = Stopwatch.GetElapsedTime(
            fpsSampleStarted,
            now).TotalSeconds;

        if (elapsedSeconds >= sampleDurationSeconds)
        {
            realFps = framesInSample / elapsedSeconds;
            framesInSample = 0;
            fpsSampleStarted = now;
        }

        workingSetMegabytes = Environment.WorkingSet / (1024d * 1024d);
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
        levelHotReload?.Dispose();
        gameUi.Dispose();
        DebugConsole.Close();
        base.OnExiting(sender, args);
    }
}
