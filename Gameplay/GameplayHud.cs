using _3DLight.Items;
using Items;
using Microsoft.Xna.Framework;

namespace _3DLight.Gameplay;

/// <summary>
/// Presents gameplay state and counts rendered frames for diagnostics.
/// </summary>
internal sealed class GameplayHud : IDisposable
{
    private readonly Game game;
    private readonly GameUi ui = new();
    private readonly PerformanceMetrics metrics = new();
    private bool initializationStarted;
    private bool disposed;

    public double Fps => metrics.Fps;
    public double WorkingSetMegabytes => metrics.WorkingSetMegabytes;

    public GameplayHud(Game game)
    {
        this.game = game;
    }

    public void Enter()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (initializationStarted)
            throw new InvalidOperationException("The gameplay HUD has already been initialized.");

        // Dispose must also clean up resources if initialization fails midway.
        initializationStarted = true;
        ui.Initialize(game);
    }

    public void Update(GameTime time, bool paused)
    {
        ui.Update(time);
        ui.SetPaused(paused);
    }

    public void Draw(
        Player player,
        Skeleton skeleton,
        ItemInteraction interaction,
        string levelVersion,
        bool lightingEnabled,
        bool showColliders,
        bool canInteract,
        string? worldInteractionText = null)
    {
        metrics.RecordDraw();

        string playerPlatform = player.CurrentPlatform?.Id.ToString() ?? "AIR";
        string skeletonPlatform = skeleton.CurrentPlatform?.Id.ToString() ?? "AIR";
        string playerText =
            $"PLAYER HP: {player.Health}/{player.MaxHealth} | " +
            $"X: {player.Position.X:F2} | " +
            $"Y: {player.Position.Y:F2} | " +
            $"Z: {player.Position.Z:F2} | " +
            $"PLATFORM: {playerPlatform}";
        string skeletonText =
            $"SKELETON HP: {skeleton.Health}/{skeleton.MaxHealth} | " +
            $"X: {skeleton.Position.X:F2} | " +
            $"Y: {skeleton.Position.Y:F2} | " +
            $"Z: {skeleton.Position.Z:F2} | " +
            $"PLATFORM: {skeletonPlatform}";
        string performanceText =
            $"FPS: {metrics.Fps:F1} | RAM: {metrics.WorkingSetMegabytes:F1} MB | " +
            $"LEVEL: {levelVersion} | " +
            $"LIGHT: {(lightingEnabled ? "ON" : "OFF")} (L) | " +
            $"COLLIDERS: {(showColliders ? "ON" : "OFF")} (O)";
        ui.SetHudText(playerText, skeletonText, performanceText, skeleton.IsDead);

        ui.SetWorldInteractionText(worldInteractionText);
        ui.SetInteractionText(interaction.NearbyItem?.Name,
    isHeld: false, isVisible: canInteract);
        ui.Draw();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        if (initializationStarted)
            ui.Dispose();
    }

    public void PrepareInventory(ItemInteraction interaction)
    {
        ui.SetInventory(interaction);
    }

}
