using _3DLight;
using _3DLight.Assets;
using _3DLight.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;

internal static class ModelLoaderGraphicsChecks
{
    public static void Run(Action<string, Action> run)
    {
        try
        {
            using var game = new LoaderTestGame(run);
            game.Run();
        }
        catch (Exception error)
        {
            run("loader graphics initialization", () => throw error);
        }
    }

    private sealed class LoaderTestGame : Game
    {
        private readonly Action<string, Action> run;
        private readonly GraphicsDeviceManager graphics;

        public LoaderTestGame(Action<string, Action> run)
        {
            this.run = run;
            graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 64,
                PreferredBackBufferHeight = 64,
                SynchronizeWithVerticalRetrace = false
            };
            Content.RootDirectory = "Content";
            IsFixedTimeStep = false;
            Window.Position = new Point(-10000, -10000);
        }

        protected override void LoadContent()
        {
            Effect effect = Content.Load<Effect>("ToonShader");
            using var fallback = new Texture2D(GraphicsDevice, 1, 1);
            fallback.SetData(new[] { Color.White });

            run("loader reads a model file, draws it and disposes only owned resources", () =>
            {
                var resources = new List<GraphicsResource>();
                CompiledModel? model = null;
                try
                {
                    Capture(resources, () => model = ModelAssetLoader.Load(
                        GraphicsDevice, "gun", fallback, effect, "Gun"));
                    Require(resources.OfType<Texture2D>().Any(), "No material texture was loaded");
                    model!.Draw(Matrix.Identity, Matrix.Identity, Matrix.Identity, new SceneLighting());
                }
                finally { model?.Dispose(); }
                Require(resources.Count > 0 && resources.All(resource => resource.IsDisposed), "Owned model resources survived disposal");
                Require(!fallback.IsDisposed && !effect.IsDisposed, "Borrowed texture or source effect was disposed");
            });

            run("loader reads model bytes and draws an animated pose", () =>
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "Models", "player.3dmodel"));
                var resources = new List<GraphicsResource>();
                CompiledModel? model = null;
                try
                {
                    Capture(resources, () => model = ModelAssetLoader.LoadFromBytes(
                        GraphicsDevice, bytes, "player", fallback, effect));
                    var sampler = new AnimationSampler(model!.Data.Nodes, model.Data.Clips);
                    var pose = new BonePose[sampler.NodeCount];
                    sampler.SamplePose("Idle", sampler.GetClipDuration("Idle") * 0.5f, false, pose);
                    model.DrawPose(pose, Matrix.Identity, Matrix.Identity, Matrix.Identity, new SceneLighting());
                }
                finally { model?.Dispose(); }
                Require(resources.Count > 0 && resources.All(resource => resource.IsDisposed), "Animated model resources survived disposal");
                Require(!fallback.IsDisposed && !effect.IsDisposed, "Borrowed resources were disposed");
            });

            run("loader releases material textures and partial mesh buffers after construction failure", () =>
            {
                var resources = new List<GraphicsResource>();
                Throws<NullReferenceException>(() => Capture(resources, () => ModelAssetLoader.Load(
                    GraphicsDevice, "gun", fallback, null!, "Gun")));
                Require(resources.Count == 0, "Partial construction left resources registered with the graphics device");
                Require(!fallback.IsDisposed && !effect.IsDisposed, "Failure disposed borrowed resources");
            });

            run("model construction releases completed meshes if a later mesh fails", () =>
            {
                using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "Models", "gun.3dmodel"));
                ModelData data = ModelDataIo.Read(stream);
                data.Meshes.Add(new MeshData { Vertices = null! });
                var resources = new List<GraphicsResource>();
                Throws<NullReferenceException>(() => Capture(resources, () => new CompiledModel(
                    GraphicsDevice, data, new[] { fallback, fallback }, Array.Empty<Texture2D>(), effect)));
                Require(resources.Count == 0, "Completed mesh resources remained registered after failure");
                Require(!fallback.IsDisposed && !effect.IsDisposed, "Failure disposed borrowed resources");
            });

            run("loader releases earlier textures when a later PNG is invalid", () =>
            {
                string folder = "loader-check-" + Guid.NewGuid().ToString("N");
                string directory = Path.Combine(AppContext.BaseDirectory, "Content", "Assets", folder);
                Directory.CreateDirectory(directory);
                string validPath = Path.Combine(directory, "a-valid.png");
                string invalidPath = Path.Combine(directory, "b-invalid.png");
                try
                {
                    using (FileStream stream = File.Create(validPath)) fallback.SaveAsPng(stream, 1, 1);
                    File.WriteAllBytes(invalidPath, new byte[] { 1, 2, 3 });
                    var resources = new List<GraphicsResource>();
                    Throws<InvalidOperationException>(() => Capture(resources, () => ModelAssetLoader.Load(
                        GraphicsDevice, "gun", fallback, effect, folder)));
                    Require(resources.Count == 0, "Failed PNG loading left textures registered with the graphics device");
                    Require(!fallback.IsDisposed && !effect.IsDisposed, "PNG failure disposed borrowed resources");
                }
                finally
                {
                    File.Delete(validPath);
                    File.Delete(invalidPath);
                    Directory.Delete(directory);
                }
            });

            Exit();
        }

        private void Capture(List<GraphicsResource> resources, Action action)
        {
            HashSet<GraphicsResource> before = Snapshot();
            try { action(); }
            finally { resources.AddRange(Snapshot().Except(before)); }
        }

        private HashSet<GraphicsResource> Snapshot()
        {
            // DesktopGL does not raise ResourceCreated for these constructors.
            // Inspect its weak resource registry only in this GPU test fixture.
            var references = (List<WeakReference>)typeof(GraphicsDevice)
                .GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(GraphicsDevice)!;
            return references.Select(reference => reference.Target)
                .OfType<GraphicsResource>()
                .Where(resource => resource is Texture2D or VertexBuffer or IndexBuffer or Effect)
                .ToHashSet();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}");
    }
}
