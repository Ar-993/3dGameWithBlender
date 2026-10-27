using System.Reflection;
using _3DLight;
using _3DLight.Gameplay;
using _3DLight.Scenes;
using Ars3D.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal static class SceneGraphicsChecks
{
    public static void Run(Action<string, Action> run)
    {
        try
        {
            using var game = new SceneTestGame(run);
            game.Run();
        }
        catch (Exception error)
        {
            run("scene graphics initialization", () => throw error);
        }
    }

    private sealed class SceneTestGame : Game
    {
        private readonly Action<string, Action> run;
        private readonly GraphicsDeviceManager graphics;

        public SceneTestGame(Action<string, Action> run)
        {
            this.run = run;
            graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 320,
                PreferredBackBufferHeight = 180,
                SynchronizeWithVerticalRetrace = false
            };
            Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
            IsFixedTimeStep = false;
            Window.Title = "Ars3D scene graphics checks";
            Window.Position = new Point(-10000, -10000);
        }

        protected override void LoadContent()
        {
            try
            {
                Effect hostEffect = Content.Load<Effect>("ToonShader");
                // BasicEffect создаёт общий шаблон в GraphicsDevice.EffectCache.
                // Его срок жизни равен устройству, поэтому создаём его до захвата сцены.
                using (var warmup = new BasicEffect(GraphicsDevice)) { }
                run("projectile renderer draws live bullets without changing simulation and releases owned resources", () =>
                {
                    Texture2D hostTexture = Content.Load<Texture2D>("Assets/Gun/T_PRP_Pistol_Colt_A_BaseColor");
                    Player player = new(
                        new CharacterPhysicsComponent(Vector3.Right * 10f, 0.35f, 1.8f, -28f),
                        new StatsComponent(100), new AttackComponent(25, 1.6f, 0.45f), new PlayerDefinition());
                    var projectiles = new ProjectileSystem(
                        new ShootingCube(Vector3.Zero, Vector3.One, fireRateSeconds: 1f));
                    projectiles.Update(0.5f, player);
                    Require(projectiles.Bullets.Count == 1, "No live bullet was available for rendering");
                    Bullet bullet = projectiles.Bullets[0];
                    Vector3 position = bullet.Position;
                    float lifetime = bullet.LifeTime;

                    HashSet<GraphicsResource> before = Snapshot(GraphicsDevice);
                    HashSet<GraphicsResource> ownedResources;
                    using (var renderer = new ProjectileRenderer(GraphicsDevice, Content))
                    {
                        ownedResources = Snapshot(GraphicsDevice).Except(before).ToHashSet();
                        renderer.Draw(projectiles,
                            Matrix.CreateLookAt(new Vector3(6f, 3f, 8f), Vector3.Right * 5f, Vector3.Up),
                            Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4,
                                GraphicsDevice.Viewport.AspectRatio, 0.04f, 1000f),
                            new SceneLighting());
                        Require(bullet.Position == position && bullet.LifeTime == lifetime &&
                            projectiles.Bullets.Count == 1 && player.Health == 100,
                            "Rendering changed projectile simulation");
                    }

                    Require(ownedResources.Count > 0 && ownedResources.All(resource => resource.IsDisposed),
                        "The projectile renderer leaked model resources");
                    Require(!hostTexture.IsDisposed && !hostEffect.IsDisposed,
                        "The projectile renderer disposed borrowed content");
                });
                run("gameplay scenes enter, update, draw and release their GPU resources across two transitions", () =>
                {
                    using var scenes = new SceneManager();
                    TimeSpan frameDuration = TimeSpan.FromSeconds(1.0 / 60.0);
                    var time = new GameTime(frameDuration, frameDuration);

                    for (int cycle = 1; cycle <= 2; cycle++)
                    {
                        var gameplay = new TrackingGameplayScene(this);
                        scenes.ChangeScene(() => gameplay);
                        scenes.Update(time);
                        scenes.Draw(time);
                        scenes.Update(time);
                        scenes.Draw(time);

                        Require(ReferenceEquals(scenes.CurrentScene, gameplay) && gameplay.Entered,
                            $"Gameplay scene {cycle} did not become the entered current scene");
                        Require(gameplay.UpdateCount == 2 && gameplay.DrawCount == 2,
                            $"Gameplay scene {cycle} did not run its real update and draw methods");
                        Require(gameplay.Resources.Count > 0,
                            $"Gameplay scene {cycle} did not load any tracked GPU resources");
                        Require(gameplay.Resources.OfType<Texture2D>().Any() &&
                            gameplay.Resources.OfType<Effect>().Any() &&
                            gameplay.Resources.OfType<VertexBuffer>().Any() &&
                            gameplay.Resources.OfType<IndexBuffer>().Any(),
                            $"Gameplay scene {cycle} did not capture its textures, effects and mesh buffers");

                        var empty = new EmptyScene();
                        scenes.ChangeScene(() => empty);
                        scenes.Update(time);
                        scenes.Draw(time);

                        Require(ReferenceEquals(scenes.CurrentScene, empty) &&
                            gameplay.Exited && gameplay.Disposed,
                            $"Gameplay scene {cycle} was not exited and disposed during the switch");
                        Require(gameplay.Resources.Count > 0 &&
                            gameplay.Resources.All(resource => resource.IsDisposed),
                            $"Gameplay scene {cycle} leaked GPU resources: " +
                            string.Join(", ", gameplay.Resources
                                .Where(resource => !resource.IsDisposed)
                                .Select(resource => resource.GetType().Name)));
                        Require(!hostEffect.IsDisposed,
                            $"Gameplay scene {cycle} disposed the host content effect");
                    }
                });
            }
            finally
            {
                Exit();
            }
        }
    }

    private sealed class TrackingGameplayScene : IGameScene
    {
        private readonly GraphicsDevice graphics;
        private readonly GameplayScene scene;

        public List<GraphicsResource> Resources { get; } = new();
        public bool Entered { get; private set; }
        public bool Exited { get; private set; }
        public bool Disposed { get; private set; }
        public int UpdateCount { get; private set; }
        public int DrawCount { get; private set; }

        public TrackingGameplayScene(Game game)
        {
            graphics = game.GraphicsDevice;
            scene = new GameplayScene(game);
        }

        public void LoadContent()
        {
            HashSet<GraphicsResource> before = Snapshot(graphics);
            try { scene.LoadContent(); }
            finally { Resources.AddRange(Snapshot(graphics).Except(before)); }
        }

        public void Enter()
        {
            // Gum resources are global and initialized here, outside the world capture.
            scene.Enter();
            Entered = true;
        }

        public void Exit()
        {
            scene.Exit();
            Exited = true;
        }

        public void Update(GameTime time)
        {
            scene.Update(time);
            UpdateCount++;
        }

        public void Draw(GameTime time)
        {
            scene.Draw(time);
            DrawCount++;
        }

        public void Dispose()
        {
            scene.Dispose();
            Disposed = true;
        }
    }

    private sealed class EmptyScene : IGameScene
    {
        public void LoadContent() { }
        public void Enter() { }
        public void Exit() { }
        public void Update(GameTime time) { }
        public void Draw(GameTime time) { }
        public void Dispose() { }
    }

    private static HashSet<GraphicsResource> Snapshot(GraphicsDevice graphics)
    {
        // DesktopGL does not raise ResourceCreated for these constructors.
        // Inspect its weak resource registry only in this GPU test fixture.
        var references = (List<WeakReference>)typeof(GraphicsDevice)
            .GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(graphics)!;
        return references.Select(reference => reference.Target)
            .OfType<GraphicsResource>()
            .Where(resource => resource is Texture2D or VertexBuffer or IndexBuffer or Effect)
            .ToHashSet();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
