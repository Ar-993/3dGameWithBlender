using _3DLight;
using Ars3D.ModelFormat;
using Ars3D.Rendering;
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

            run("foliage cutoff parameter reaches the shader and holes do not write depth", () =>
            {
                Require(effect.Parameters["AlphaCutoff"] is not null,
                    "The shader parameter name must exactly match CompiledModel's AlphaCutoff binding");
                using var texture = new Texture2D(GraphicsDevice, 2, 1);
                texture.SetData(new[] { Color.Transparent, new Color(8, 35, 10, 255) });
                using var target = new RenderTarget2D(GraphicsDevice, 64, 64, false,
                    SurfaceFormat.Color, DepthFormat.Depth24);
                var lighting = new SceneLighting { Enabled = false };
                foreach (string foliage in new[] { "Bush", "Tree", "Tree_01", "Background", "Background_01" })
                {
                    using CompiledModel leaves = QuadModel(foliage, foliage + ".png", texture, 0f);
                    using CompiledModel background = QuadModel("Concrete", "concrete.png", fallback, 0.5f);
                    GraphicsDevice.SetRenderTarget(target);
                    try
                    {
                        GraphicsDevice.Clear(Color.CornflowerBlue);
                        leaves.Draw(Matrix.Identity, Matrix.Identity, Matrix.Identity, lighting);
                        background.Draw(Matrix.Identity, Matrix.Identity, Matrix.Identity, lighting);
                    }
                    finally { GraphicsDevice.SetRenderTarget(null); }
                    var pixels = new Color[64 * 64];
                    target.GetData(pixels);
                    Color hole = pixels[32 * 64 + 16], leaf = pixels[32 * 64 + 48];
                    Require(hole.R > 240 && hole.G > 240 && hole.B > 240,
                        $"{foliage}: transparent pixels still occlude the background: {hole}");
                    Require(leaf.R < 20 && leaf.G > 25 && leaf.G < 45 && leaf.B < 20,
                        $"{foliage}: dark opaque leaves were discarded or overwritten: {leaf}");
                }

                run("fog colors distant pixels and skips fully hidden mesh draw calls", () =>
                {
                    using CompiledModel distant = QuadModel("Concrete", "concrete.png", fallback, 0.5f);
                    lighting.Fog.Enabled = true;
                    lighting.Fog.StartDistance = 0.1f;
                    lighting.Fog.EndDistance = 0.2f;
                    lighting.Fog.Color = Color.Lime;
                    GraphicsDevice.SetRenderTarget(target);
                    try
                    {
                        GraphicsDevice.Clear(Color.Red);
                        distant.Draw(Matrix.Identity, Matrix.Identity, Matrix.Identity, lighting);
                    }
                    finally { GraphicsDevice.SetRenderTarget(null); }
                    var pixels = new Color[64 * 64];
                    target.GetData(pixels);
                    Color fogPixel = pixels[32 * 64 + 32];
                    Require(fogPixel.G > 240 && fogPixel.R < 10 && fogPixel.B < 10,
                        $"Distant pixel does not match fog: {fogPixel}");
                    lighting.Fog.CullFullyFoggedMeshes = true;
                    GraphicsDevice.SetRenderTarget(target);
                    try
                    {
                        GraphicsDevice.Clear(Color.Red);
                        var before = GraphicsDevice.Metrics.DrawCount;
                        distant.Draw(Matrix.Identity, Matrix.Identity, Matrix.Identity, lighting);
                        Require(GraphicsDevice.Metrics.DrawCount == before,
                            "Fully fogged static mesh still issued a draw call");
                    }
                    finally { GraphicsDevice.SetRenderTarget(null); }
                    target.GetData(pixels);
                    Require(pixels[32 * 64 + 32] == Color.Red, "Culled mesh changed the render target");
                    var box = new BoundingBox(new(-1f), new(1f));
                    lighting.Fog.EndDistance = 28f;
                    Require(!lighting.Fog.IsFullyFogged(box, Matrix.CreateScale(10f) *
                        Matrix.CreateRotationY(0.5f) * Matrix.CreateTranslation(30f, 0f, 0f), Vector3.Zero),
                        "Large rotated mesh intersecting visible range was culled");
                    Require(lighting.Fog.IsFullyFogged(box, Matrix.CreateTranslation(40f, 0f, 0f), Vector3.Zero),
                        "Distant mesh was retained");
                    lighting.Fog.Enabled = false;
                    Require(!lighting.Fog.IsFullyFogged(box, Matrix.CreateTranslation(40f, 0f, 0f), Vector3.Zero),
                        "Disabling fog did not disable distance culling");
                });

                CompiledModel QuadModel(string material, string textureName, Texture2D map, float z)
                {
                    var data = new ModelData();
                    data.Nodes.Add(new("Quad", -1, System.Numerics.Matrix4x4.Identity));
                    data.Meshes.Add(new MeshData
                    {
                        Node = 0, MaterialName = material, TextureName = textureName,
                        Vertices = [Vertex(-1f, -1f, 0f), Vertex(1f, -1f, 1f),
                            Vertex(1f, 1f, 1f), Vertex(-1f, 1f, 0f)],
                        Indices = [0, 1, 2, 0, 2, 3]
                    });
                    return new CompiledModel(GraphicsDevice, data, [map], [], effect);

                    VertexData Vertex(float x, float y, float u) => new(new(x, y, z),
                        System.Numerics.Vector3.UnitZ, new(u, 0.5f), 0, 0, 0, 0, new(1f, 0f, 0f, 0f));
                }
            });

            run("loader reads a model file, draws it and disposes only owned resources", () =>
            {
                var resources = new List<GraphicsResource>();
                CompiledModel? model = null;
                try
                {
                    Capture(resources, () => model = ModelAssetLoader.Load(
                        GraphicsDevice, ModelPaths.ModelFile("gun"), fallback, effect,
                        ModelPaths.TextureDirectory("Gun")));
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
                        GraphicsDevice, bytes, fallback, effect, ModelPaths.TextureDirectory("player")));
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
                    GraphicsDevice, ModelPaths.ModelFile("gun"), fallback, null!,
                    ModelPaths.TextureDirectory("Gun"))));
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
                        GraphicsDevice, ModelPaths.ModelFile("gun"), fallback, effect, directory)));
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
