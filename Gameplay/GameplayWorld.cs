using _3DLight.Items;
using _3DLight.Interaction;
using Ars3D.Rendering;
using Camera;
using Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Gameplay;

// Владеет объектами игрового мира и задаёт порядок симуляции.
// Получает готовые действия из GameplayInput; отрисовкой занимается GameplayRenderer.
internal sealed class GameplayWorld : IDisposable
{
    private readonly GraphicsDevice graphicsDevice;
    private readonly ContentManager content;
    private readonly LevelSession levelSession;
    private ItemCatalog? itemCatalog;
    private bool disposed;
    private bool flashlightOn = true;

    public Player Player { get; } = GameplayCharacters.CreatePlayer();
    public Skeleton Skeleton { get; } = GameplayCharacters.CreateSkeleton();
    public FirstPersonCamera Camera { get; } = new();
    public SceneLighting Lighting { get; } = new();
    public Level Level => levelSession.Level;
    public string LevelVersion => levelSession.Version;
    public ItemInteraction Items { get; }
    public InteractionSystem Interactions { get; }
    public ProjectileSystem Projectiles { get; }
    public bool ShowCollisionDebug { get; private set; }

    public GameplayWorld(GraphicsDevice graphicsDevice, ContentManager content)
    {
        this.graphicsDevice = graphicsDevice;
        this.content = content;
        levelSession = new LevelSession(content, Player, Skeleton);
        Items = new ItemInteraction(Player.Definition.Interaction);
        Interactions = new InteractionSystem(Level.IsInteractionVisible);
        Projectiles = new ProjectileSystem(new ShootingCube(
            position: new Vector3(70f, 2f, 5f),
            size: Vector3.One,
            fireRateSeconds: 1.5f));

        Lighting.Fog.Enabled = LevelSession.ModelName == "gas_station";
        Lighting.Fog.Color = new Color(148, 155, 151);
        Lighting.Fog.StartDistance = 7f;
        Lighting.Fog.EndDistance = 28f;
    }

    public void LoadContent()
    {
        levelSession.LoadContent();
        Skeleton.LoadContent(graphicsDevice, content);

        Texture2D fallbackTexture = content.Load<Texture2D>("Assets/Gun/T_PRP_Pistol_Colt_A_BaseColor");
        Effect toonEffect = content.Load<Effect>("ToonShader");
        itemCatalog = ItemCatalog.Load(graphicsDevice, fallbackTexture, toonEffect);
        ItemSpawner.SpawnConfigured(Level, itemCatalog, Items, Player.Position);
        Camera.UpdateMatrices(Player.Position, graphicsDevice.Viewport.AspectRatio);
    }

    public void SynchronizeInteraction(bool interactDown) => Items.SuppressInput(interactDown);

    public void Update(float seconds, GameplayInputFrame input)
    {
        levelSession.Update();
        UpdatePlayer(seconds, input);
        Interactions.Update(Level.Interactables, Camera.Position, Camera.Forward,
            input.InteractPressed, !input.SuppressInteraction && !Player.IsDead);
        UpdateDoors(seconds);
        Skeleton.Update(Player, Level, seconds);
        Lighting.PointPosition = Player.Position + new Vector3(0f, 1.25f, 0f);
        Projectiles.Update(seconds, Player);
        UpdateItems(seconds, input);
        UpdateFlashlight(input.ToggleFlashlight);
        ApplyDebugCommands(input);
    }

    private void UpdatePlayer(float seconds, GameplayInputFrame input)
    {
        Camera.ApplyMouseDelta(input.LookDelta.X, input.LookDelta.Y);
        Player.Update(
            seconds,
            movement: input.Move,
            jumpPressed: input.JumpPressed,
            attackPressed: input.AttackPressed,
            cameraYaw: Camera.Yaw,
            level: Level);
        ApplyPlayerAttack();
        Camera.UpdateMatrices(Player.Position, graphicsDevice.Viewport.AspectRatio);
    }

    private void UpdateItems(float seconds, GameplayInputFrame input)
    {
        // Маршрутизация одной кнопки: мировое действие не должно одновременно подбирать предмет.
        if (input.SuppressInteraction || Interactions.Target is not null)
            Items.SuppressInput(input.InteractDown);

        Items.Update(
            Level,
            seconds,
            Player.Position,
            Camera.Position,
            Camera.Forward,
            eDown: input.InteractDown,
            canInteract: !Player.IsDead,
            selectedSlot: input.SelectedSlot,
            dropPressed: input.DropPressed);
    }

    private void UpdateDoors(float seconds)
    {
        ReadOnlySpan<CapsuleShape> occupants =
        [
            new(Player.Position, GameplayCharacters.CharacterCollisionRadius, GameplayCharacters.CharacterCollisionHeight),
            new(Skeleton.Position, GameplayCharacters.CharacterCollisionRadius, GameplayCharacters.CharacterCollisionHeight)
        ];
        Level.UpdateDoors(seconds, occupants);
    }

    private void UpdateFlashlight(bool togglePressed)
    {
        if (togglePressed && Items.HeldItem?.State.Definition.Id == "flashlight")
            flashlightOn = !flashlightOn;

        bool holdingFlashlight = Items.TryGetHeldLight(Camera.Position, Camera.Forward,
            out Vector3 lightOrigin, out Vector3 lightDirection);
        Lighting.FlashlightEnabled = flashlightOn && holdingFlashlight && !Player.IsDead;
        Lighting.FlashlightPosition = lightOrigin;
        Lighting.FlashlightDirection = lightDirection;
    }

    private void ApplyDebugCommands(GameplayInputFrame input)
    {
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

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        itemCatalog?.Dispose();
        Skeleton.Dispose();
        levelSession.Dispose();
    }
}
