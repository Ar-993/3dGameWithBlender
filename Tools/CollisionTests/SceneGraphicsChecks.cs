using System.Reflection;
using _3DLight.Scenes;
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
