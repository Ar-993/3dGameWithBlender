using _3DLight.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace _3DLight.Gameplay;

// Порядок игрового шага задаётся здесь; ввод, загрузка и пули имеют своих владельцев.
internal sealed class GameplayWorld : IDisposable
{
    private readonly GraphicsDevice graphics;
    private readonly ContentManager content;
    private ItemCatalog? itemCatalog;
    private ProjectileSystem? projectiles;
    private bool disposed;
    private bool flashlightOn = true;

    public Player Player { get; } = GameplayCharacters.CreatePlayer();
    public Skeleton Skeleton { get; } = GameplayCharacters.CreateSkeleton();
    public FirstPersonCamera Camera { get; } = new();
    public SceneLighting Lighting { get; } = new();
    public LevelSession Session { get; }
    public ItemInteraction Items { get; }
    public bool ShowCollisionDebug { get; private set; }

    public GameplayWorld(GraphicsDevice graphics, ContentManager content)
    {
        this.graphics = graphics;
        this.content = content;
        Session = new LevelSession(content, Player, Skeleton);
        Items = new ItemInteraction(Player.Definition.Interaction);

        Lighting.Fog.Enabled = LevelSession.ModelName == "gas_station";
        Lighting.Fog.Color = new Color(148, 155, 151);
        Lighting.Fog.StartDistance = 7f;
        Lighting.Fog.EndDistance = 28f;
    }

    public void LoadContent()
    {
        Session.LoadContent();
        string animationsFolder = Path.Combine(AppContext.BaseDirectory, "Content", "Assets");
        Skeleton.LoadContent(graphics, content, animationsFolder);

        Texture2D fallback = content.Load<Texture2D>("Assets/Gun/T_PRP_Pistol_Colt_A_BaseColor");
        Effect effect = content.Load<Effect>("ToonShader");
        itemCatalog = ItemCatalog.Load(graphics, fallback, effect);
        ItemSpawner.SpawnConfigured(Session.Level, itemCatalog, Items, Player.Position);
        projectiles = new ProjectileSystem(graphics, content, Lighting);
        Camera.UpdateMatrices(Player.Position, graphics.Viewport.AspectRatio);
    }

    public void SynchronizeInput(KeyboardState keyboard)
    {
        Player.SynchronizeInput(keyboard);
        Items.SuppressInput(keyboard.IsKeyDown(Keys.E));
    }

    public void Update(GameTime gameTime, GameplayInputFrame input)
    {
        Session.Update();
        Camera.ApplyMouseDelta(input.LookDelta.X, input.LookDelta.Y);
        Player.Update(gameTime, input.Keyboard, Camera.Yaw, Session.Level,
            input.AttackPressed);
        ApplyPlayerAttack();
        Camera.UpdateMatrices(Player.Position, graphics.Viewport.AspectRatio);

        float seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Skeleton.Update(Player, Session.Level, seconds);
        Lighting.FollowPlayer(Player.Position);
        projectiles!.Update(seconds, Player);

        bool interactDown = input.Keyboard.IsKeyDown(Keys.E);
        if (input.SuppressInteraction)
            Items.SuppressInput(interactDown);
        Items.Update(Session.Level, seconds, Player.Position, Camera.Position,
            Camera.Forward, interactDown, canInteract: !Player.IsDead);

        if (input.ToggleFlashlight && Items.HeldItem?.State.Definition.Id == "flashlight")
            flashlightOn = !flashlightOn;
        bool holdingFlashlight = Items.TryGetHeldLight(Camera.Position, Camera.Forward,
            out Vector3 lightOrigin, out Vector3 lightDirection);
        Lighting.FlashlightEnabled = flashlightOn && holdingFlashlight && !Player.IsDead;
        Lighting.FlashlightPosition = lightOrigin;
        Lighting.FlashlightDirection = lightDirection;

        if (input.ToggleLighting)
            Lighting.Enabled = !Lighting.Enabled;
        if (input.ToggleColliders)
            ShowCollisionDebug = !ShowCollisionDebug;
    }

    private void ApplyPlayerAttack()
    {
        if (Player.AttackShouldDealDamage && !Skeleton.IsDead &&
            Vector3.Distance(Skeleton.Position, Player.Position) <= Player.AttackRange)
        {
            Skeleton.TakeDamage(Player.AttackDamage);
        }
    }

    public void Draw()
    {
        graphics.Clear(Lighting.Fog.Enabled ? Lighting.Fog.Color : new Color(25, 30, 40));
        graphics.DepthStencilState = DepthStencilState.Default;
        graphics.RasterizerState = RasterizerState.CullNone;

        if (ShowCollisionDebug)
        {
            Session.Level.DrawColliders(Camera.View, Camera.Projection,
                CreateCharacterDebugBounds(Player.Position),
                CreateCharacterDebugBounds(Skeleton.Position));
            return;
        }

        Session.Level.Draw(Camera.View, Camera.Projection, Lighting);
        Skeleton.Draw(Camera.View, Camera.Projection, Lighting);
        Items.Draw(Camera.View, Camera.Projection, Lighting);
        projectiles!.Draw(Camera.View, Camera.Projection);
    }

    private static BoundingBox CreateCharacterDebugBounds(Vector3 position) => new(
        position - new Vector3(GameplayCharacters.CharacterCollisionRadius, 0f,
            GameplayCharacters.CharacterCollisionRadius),
        position + new Vector3(GameplayCharacters.CharacterCollisionRadius,
            GameplayCharacters.CharacterCollisionHeight, GameplayCharacters.CharacterCollisionRadius));

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        projectiles?.Dispose();
        itemCatalog?.Dispose();
        Skeleton.Dispose();
        Session.Dispose();
    }
}
