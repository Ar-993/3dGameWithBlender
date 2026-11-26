using _3DLight.Collision;
using _3DLight.Interaction;
using _3DLight.Items;
using _3DLight.Npcs;
using Ars3D.Rendering;
using Camera;
using Gameplay;
using Items;
using Levels.Doors;
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

    private readonly AudioSystem? audio;

    private CapsuleShape[] doorOccupants = [];
    public NpcSystem Npcs { get; private set; } = null!;
    public DialogueSession Dialogue { get; } = new();
    public PlayerWeaponSystem Weapons { get; private set; } = null!;
    public RoundTimer RoundTimer { get; } = new(60f);
    public FootstepSystem Footsteps { get; } = new();

    public GameplayWorld(GraphicsDevice graphicsDevice, ContentManager content, AudioSystem? audio = null)
    {
        this.graphicsDevice = graphicsDevice;
        this.content = content;
        this.audio = audio;
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
        RoundTimer.Elapsed += ResetCharacters;
    }

    public void LoadContent()
    {
        levelSession.LoadContent();
        Skeleton.LoadContent(graphicsDevice, content);

        Texture2D fallbackTexture = content.Load<Texture2D>("Assets/Gun/T_PRP_Pistol_Colt_A_BaseColor");
        Effect toonEffect = content.Load<Effect>("ToonShader");
        Npcs = NpcSystem.Load(Level, LevelSession.ModelName, graphicsDevice, fallbackTexture, toonEffect, Dialogue);
        doorOccupants = new CapsuleShape[2 + Npcs.Capsules.Length];
        foreach (string id in itemCatalog.StartingItems)
            Items.StoreStartingItem(itemCatalog.Create(id, Player.Position));
        Weapons = new PlayerWeaponSystem(graphicsDevice, audio);
        itemCatalog = ItemCatalog.Load(graphicsDevice, fallbackTexture, toonEffect);
        ItemSpawner.SpawnConfigured(Level, itemCatalog, Items, Player.Position);
        Camera.UpdateMatrices(Player.Position, graphicsDevice.Viewport.AspectRatio);
        Footsteps.Reset(Player.Position);
        RoundTimer.Start();
    }

    public void SynchronizeInteraction(bool interactDown) => Items.SuppressInput(interactDown);

    public void Update(float seconds, GameplayInputFrame input)
    {
        if (Dialogue.IsOpen)
        {
            UpdateDialogue(seconds, input);
            return;
        }

        levelSession.Update();
        if (!input.SuppressInteraction && !Player.IsDead && input.SelectedSlot >= 0)
            Items.SelectSlot(input.SelectedSlot);
        bool weaponEquipped = Items.HeldItem?.Weapon is not null;
        UpdatePlayer(seconds, input, allowMelee: !weaponEquipped);
        UpdateWorldInteraction(input);
        if (Dialogue.IsOpen)
            return;

        UpdateSimulation(seconds, input, weaponEquipped);
    }

    private void UpdateDialogue(float seconds, GameplayInputFrame input)
    {
        Items.SuppressInput(input.InteractDown);
        Dialogue.Update(seconds, input.InteractPressed, input.CloseDialoguePressed);
        if (input.InteractPressed && Dialogue.IsOpen)
            audio?.Play("dialogue");
    }

    private void UpdateWorldInteraction(GameplayInputFrame input)
    {
        Interactions.Update(Level.Interactables.Concat(Npcs.TalkTargets), Camera.Position, Camera.Forward,
            input.InteractPressed, !input.SuppressInteraction && !Player.IsDead);
        if (input.InteractPressed && Interactions.Target is LevelDoor)
            audio?.Play("door");
        if (Dialogue.IsOpen)
        {
            audio?.Play("dialogue");
            Items.SuppressInput(input.InteractDown);
        }
    }

    private void UpdateSimulation(float seconds, GameplayInputFrame input, bool weaponEquipped)
    {
        UpdateDoors(seconds);
        Skeleton.Update(Player, Level, seconds, Npcs.Capsules);
        Npcs.Update(Level, seconds, CharacterCapsule(Player.Position), CharacterCapsule(Skeleton.Position));
        Lighting.PointPosition = Player.Position + new Vector3(0f, 1.25f, 0f);
        Projectiles.Update(seconds, Player);
        UpdateItems(seconds, input);
        Weapons.Update(seconds, Items, Camera, Level, Player, Skeleton,
            weaponEquipped && input.AttackPressed && !input.SuppressInteraction,
            input.ReloadPressed && !input.SuppressInteraction);
        UpdateFlashlight(input.ToggleFlashlight);
        ApplyDebugCommands(input);
        RoundTimer.Update(seconds);
    }
    private void ResetCharacters()
    {
        audio?.Play("roundend");
        levelSession.RespawnCharacters();
        Footsteps.Reset(Player.Position);
        Npcs.ResetRoutes(Level);
        RoundTimer.Start();
    }


    private static CapsuleShape CharacterCapsule(Vector3 feet) => new(feet,
    GameplayCharacters.CharacterCollisionRadius, GameplayCharacters.CharacterCollisionHeight);

    private void UpdatePlayer(float seconds, GameplayInputFrame input, bool allowMelee)
    {
        Camera.ApplyMouseDelta(input.LookDelta.X, input.LookDelta.Y);
        Player.Update(
            seconds,
            movement: input.Move,
            jumpPressed: input.JumpPressed,
            attackPressed: allowMelee && input.AttackPressed,
            cameraYaw: Camera.Yaw,
            level: Level,
            obstacles: Npcs.Capsules);
        Footsteps.Update(Player);
        ApplyPlayerAttack();
        Camera.UpdateMatrices(Player.Position, graphicsDevice.Viewport.AspectRatio);
    }

    private void UpdateItems(float seconds, GameplayInputFrame input)
    {
        // Маршрутизация одной кнопки: мировое действие не должно одновременно подбирать предмет.
        if (input.SuppressInteraction || Interactions.Target is not null)
            Items.SuppressInput(input.InteractDown);

        WorldItem? previousItem = Items.HeldItem;

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

        if (input.InteractPressed && Items.HeldItem is { } pickedUp && pickedUp != previousItem)
            audio?.Play("pickup");
    }

    private void UpdateDoors(float seconds)
    {
        doorOccupants[0] = CharacterCapsule(Player.Position);
        doorOccupants[1] = CharacterCapsule(Skeleton.Position);
        Npcs.Capsules.CopyTo(doorOccupants.AsSpan(2));
        Level.UpdateDoors(seconds, doorOccupants);
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
        Dialogue.Close();
        Weapons?.Dispose();
        Npcs?.Dispose();
        itemCatalog?.Dispose();
        Skeleton.Dispose();
        levelSession.Dispose();
    }
}
