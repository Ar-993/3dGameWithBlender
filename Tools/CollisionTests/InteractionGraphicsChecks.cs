using System.Reflection;
using _3DLight;
using _3DLight.Models;
using _3DLight.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal static class InteractionGraphicsChecks
{
    public static void Run(Action<string, Action> run)
    {
        try
        {
            using var game = new InteractionTestGame(run);
            game.Run();
        }
        catch (Exception error)
        {
            run("interaction graphics initialization", () => throw error);
        }
    }

    private sealed class InteractionTestGame : Game
    {
        private const int ImageWidth = 1280;
        private const int ImageHeight = 720;
        private readonly Action<string, Action> run;
        private readonly GraphicsDeviceManager graphics;

        public InteractionTestGame(Action<string, Action> run)
        {
            this.run = run;
            graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = ImageWidth,
                PreferredBackBufferHeight = ImageHeight,
                GraphicsProfile = GraphicsProfile.HiDef,
                SynchronizeWithVerticalRetrace = false
            };
            Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
            IsFixedTimeStep = false;
            Window.Title = "Ars3D interaction graphics checks";
            Window.Position = new Point(-10000, -10000);
        }

        protected override void LoadContent()
        {
            Level level = new();
            GameUi? ui = null;
            try
            {
                level.LoadContent(Content, "gas_station");
                Effect effect = Content.Load<Effect>("ToonShader");
                using var fallback = new Texture2D(GraphicsDevice, 1, 1);
                fallback.SetData(new[] { Color.White });
                using ItemCatalog catalog = ItemCatalog.Load(GraphicsDevice, fallback, effect);
                ui = new GameUi();
                ui.Initialize(this);
                ui.SetHudText("", "", "", skeletonIsDead: false);
                ui.SetPaused(false);

                Player testPlayer = new(
                    new CharacterPhysicsComponent(Vector3.Zero, 0.35f, 1.8f, -28f),
                    new StatsComponent(maxHealth: 100),
                    new AttackComponent(25, 1.6f, 0.45f),
                    PlayerDefinition.Load());
                ItemInteraction interaction = new(testPlayer.Definition.Interaction);
                SceneLighting lighting = new();
                lighting.Fog.Enabled = true;
                lighting.Fog.Color = new Color(148, 155, 151);
                lighting.Fog.StartDistance = 7f;
                lighting.Fog.EndDistance = 28f;
                Vector3 playerFeet = Vector3.Zero;
                FirstPersonCamera camera = new();
                string directory = Path.Combine(FindRepositoryRoot(), "artifacts", "first-person");
                Directory.CreateDirectory(directory);
                using var target = new RenderTarget2D(GraphicsDevice, ImageWidth, ImageHeight,
                    mipMap: false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);

                WorldItem? item = null;
                bool itemSettled = false;
                run("real ItemSpawn creates rigid Santa that tumbles and rests on gas station geometry", () =>
                {
                    Require(level.TryGetMarkerPosition("ItemSpawn", out Vector3 marker),
                        "The gas station model is missing its ItemSpawn marker");
                    Near(marker.Y, 5f);
                    Require(File.Exists(Path.Combine(AppContext.BaseDirectory,
                        "Content", "Assets", "Christmas", "SantaClaus.png")),
                        "The real Santa material texture was not copied to the test output");
                    item = ItemSpawner.Spawn(level, catalog, "santa", "ItemSpawn", Vector3.Zero);
                    Require(item.Renderer!.Model.Data.Meshes.Any(mesh => mesh.Bones.Length > 0),
                        "Santa did not contain a skinned mesh for the GPU draw");
                    Near(item.Position, marker);
                    Require(item.HasRigidBody, "Santa did not receive its rigid body");
                    interaction.Add(item);
                    playerFeet = new(marker.X, 0f, marker.Z + 1.3f);
                    Quaternion initialOrientation = item.Orientation;
                    for (int frame = 0; frame < 30; frame++)
                    {
                        item.UpdatePhysics(level, 1f / 60f);
                        CheckFiniteBody(item);
                    }
                    Require(item.Velocity.Y < 0f && item.Center.Y < marker.Y,
                        "The rigid Santa did not fall under gravity");
                    Require(MathF.Abs(Quaternion.Dot(initialOrientation, item.Orientation)) < 0.999f,
                        "Santa remained upright instead of rotating during its fall");
                    AimAt(item.Center);
                    Capture("falling-item.png");

                    for (int frame = 0; frame < 720 && !item.IsResting; frame++)
                    {
                        item.UpdatePhysics(level, 1f / 60f);
                        CheckFiniteBody(item);
                    }
                    Require(item.IsResting, $"Santa did not come to rest: center={item.Center}");
                    Require(item.Bounds.Min.Y >= -0.005f && item.Bounds.Min.Y <= 0.035f,
                        $"The rotated Santa penetrated or hovered above the floor: {item.Bounds}");
                    Near(item.Velocity, Vector3.Zero);
                    Near(item.AngularVelocity, Vector3.Zero);
                    Require(MathF.Abs(Vector3.Transform(Vector3.Up, item.Orientation).Y) < 0.97f,
                        "The rigid Santa was forced back into an upright rest pose");

                    Vector3 restingCenter = item.Center;
                    Quaternion restingOrientation = item.Orientation;
                    for (int frame = 0; frame < 60; frame++)
                        item.UpdatePhysics(level, 1f / 60f);
                    Near(item.Center, restingCenter);
                    Near(MathF.Abs(Quaternion.Dot(restingOrientation, item.Orientation)), 1f);
                    Require(item.IsResting, "The resting Santa started moving again without an impulse");

                    playerFeet = new(item.Center.X,
                        MathF.Max(0f, item.Bounds.Min.Y - CapsuleCollision.SkinWidth), item.Center.Z + 1.15f);
                    AimAt(item.Center);
                    interaction.Update(level, 0f, playerFeet, camera.Position, camera.Forward, eDown: false);
                    Capture("fallen-side.png");
                    itemSettled = true;
                });

                if (!itemSettled)
                    return;

                WorldItem santa = item!;
                run("the item catalog shares one model and definition across many independent instances", () =>
                {
                    WorldItem[] copies = Enumerable.Range(0, 100)
                        .Select(index => catalog.Create("santa", new Vector3(index, 5f, 15f))).ToArray();
                    Require(copies.Select(copy => copy.State.InstanceId).Distinct().Count() == 100,
                        "Spawned items shared mutable instance identity");
                    Require(copies.All(copy => ReferenceEquals(copy.Renderer!.Model, santa.Renderer!.Model) &&
                        ReferenceEquals(copy.State.Definition, santa.State.Definition)),
                        "The catalog reloaded an asset or definition for each instance");
                    Require(catalog.LoadedModelCount == 1, "Unused item models were loaded eagerly");
                    WorldItem gun = catalog.Create("colt", Vector3.Zero);
                    Require(gun.State.Definition.Id == "colt" && catalog.LoadedModelCount == 2,
                        "Another item definition could not load its own model");
                });
                playerFeet = new(santa.Center.X,
                    MathF.Max(0f, santa.Bounds.Min.Y - CapsuleCollision.SkinWidth), santa.Center.Z + 1.3f);
                AimAt(santa.Center);
                run("nearby pickup renders the real Santa and Cyrillic E prompt", () =>
                {
                    interaction.Update(level, 0f, playerFeet, camera.Position, camera.Forward, eDown: false);
                    Require(interaction.NearbyItem == santa && interaction.HeldItem is null,
                        "Approaching Santa did not expose a pickup candidate");
                    Capture("nearby-pickup.png");
                });

                run("the E rising edge renders Santa in the invisible inventory slot", () =>
                {
                    interaction.Update(level, 0f, playerFeet, camera.Position, camera.Forward, eDown: true);
                    Require(interaction.HeldItem == santa && santa.IsHeld,
                        "The E press did not move Santa into the single held slot");
                    camera = new FirstPersonCamera();
                    camera.UpdateMatrices(playerFeet, ImageWidth / (float)ImageHeight);
                    Capture("held-item.png");
                });

                run("the next E press throws Santa and renders its physical flight", () =>
                {
                    Require(interaction.HeldItem == santa, "Santa was not held before the throw");
                    interaction.Update(level, 0f, playerFeet, camera.Position, camera.Forward, eDown: false);
                    interaction.Update(level, 0f, playerFeet, camera.Position, camera.Forward, eDown: true);
                    Require(interaction.HeldItem is null && !santa.IsHeld && santa.Velocity.Z < 0f,
                        "The second E press did not throw Santa forward");
                    Vector3 release = santa.Center;
                    Quaternion releaseOrientation = santa.Orientation;
                    float releaseVerticalVelocity = santa.Velocity.Y;
                    for (int frame = 0; frame < 12; frame++)
                    {
                        interaction.Update(level, 1f / 60f, playerFeet,
                            camera.Position, camera.Forward, eDown: false);
                        CheckFiniteBody(santa);
                    }
                    Require(santa.Center.Z < release.Z && santa.Velocity.Y < releaseVerticalVelocity,
                        "The thrown item did not travel forward under gravity");
                    Require(MathF.Abs(Quaternion.Dot(releaseOrientation, santa.Orientation)) < 0.99f,
                        "The thrown Santa did not visibly tumble");
                    Capture("thrown-item.png");
                });

                void AimAt(Vector3 center)
                {
                    camera = new FirstPersonCamera();
                    camera.UpdateMatrices(playerFeet, ImageWidth / (float)ImageHeight);
                    Vector3 offset = center - camera.Position;
                    float pitch = MathF.Atan2(offset.Y, MathF.Sqrt(offset.X * offset.X + offset.Z * offset.Z));
                    camera.ApplyMouseDelta(0f, -pitch / 0.003f);
                    camera.UpdateMatrices(playerFeet, ImageWidth / (float)ImageHeight);
                    lighting.FollowPlayer(playerFeet);
                }

                void Capture(string fileName)
                {
                    try
                    {
                        GraphicsDevice.SetRenderTarget(target);
                        GraphicsDevice.Clear(lighting.Fog.Color);
                        GraphicsDevice.BlendState = BlendState.Opaque;
                        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
                        level.Draw(camera.View, camera.Projection, lighting);
                        interaction.Draw(camera.View, camera.Projection, lighting);
                        WorldItem? promptItem = interaction.HeldItem ?? interaction.NearbyItem;
                        ui.SetInteractionText(promptItem?.Name, interaction.HeldItem is not null, isVisible: true);
                        ui.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1f / 60f)));
                        ui.Draw();
                    }
                    finally
                    {
                        GraphicsDevice.SetRenderTarget(null);
                    }

                    string path = Path.Combine(directory, fileName);
                    using (FileStream stream = File.Create(path))
                        target.SaveAsPng(stream, ImageWidth, ImageHeight);
                    Require(new FileInfo(path).Length > 0, "The graphics capture was empty");
                    Console.WriteLine($"  CAPTURE {path}");
                }
            }
            finally
            {
                GraphicsDevice.SetRenderTarget(null);
                ui?.Dispose();
                DisposeLevelResources(level);
                Exit();
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (DirectoryInfo? directory = new(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "3DLight.csproj")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("The screenshot fixture could not find the Ars3D repository.");
    }

    private static void DisposeLevelResources(Level level)
    {
        // Level currently has no disposal method. Release the resources created
        // by this fixture while the owning DesktopGL device is still alive.
        foreach (string fieldName in new[] { "model", "collisionDebugEffect" })
        {
            var resource = typeof(Level)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(level) as IDisposable;
            resource?.Dispose();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void CheckFiniteBody(WorldItem item)
    {
        Require(Finite(item.Center) && Finite(item.Velocity) && Finite(item.AngularVelocity) &&
            Finite(item.Bounds.Min) && Finite(item.Bounds.Max) &&
            float.IsFinite(item.Orientation.X) && float.IsFinite(item.Orientation.Y) &&
            float.IsFinite(item.Orientation.Z) && float.IsFinite(item.Orientation.W),
            "The rigid Santa produced a non-finite position, rotation or velocity");
        Near(item.Orientation.LengthSquared(), 1f);

        static bool Finite(Vector3 value) =>
            float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static void Near(float actual, float expected) =>
        Require(MathF.Abs(actual - expected) <= 0.002f, $"Expected {expected}, got {actual}");

    private static void Near(Vector3 actual, Vector3 expected) =>
        Require(Vector3.Distance(actual, expected) <= 0.002f, $"Expected {expected}, got {actual}");
}
