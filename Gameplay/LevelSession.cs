using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace _3DLight.Gameplay;

internal sealed class LevelSession : IDisposable
{
    private enum SpawnMode
    {
        Coordinates,
        BlenderEmpty
    }

    private static readonly SpawnMode ActivePlayerSpawnMode = SpawnMode.BlenderEmpty;
    private static readonly SpawnMode ActiveSkeletonSpawnMode = SpawnMode.BlenderEmpty;
    private const string PlayerSpawnObjectName = "PlayerSpawn";
    private const string SkeletonSpawnObjectName = "SkeletonSpawn";

    public const string ModelName = "gas_station";

    private readonly ContentManager content;
    private readonly Player player;
    private readonly Skeleton skeleton;
    private readonly bool enableHotReload;
    private LevelHotReload? hotReload;
    private bool disposed;

    public Level Level { get; } = new();
    public string Version { get; private set; } = "base";

    public LevelSession(
        ContentManager content,
        Player player,
        Skeleton skeleton,
        bool enableHotReload = false)
    {
        this.content = content;
        this.player = player;
        this.skeleton = skeleton;
        this.enableHotReload = enableHotReload;
    }

    public void LoadContent()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        try
        {
            Level.LoadContent(content, ModelName);
            hotReload = enableHotReload ? LevelHotReload.TryCreate() : null;
            TryLoadLatestVersion();
            SpawnCharacters();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Update()
    {
        if (hotReload is null)
            return;

        try
        {
            if (hotReload.TryTakePendingModel(out byte[] modelBytes, out string version))
                ApplyVersion(modelBytes, version, respawnCharacters: true);
        }
        catch (Exception error)
        {
            Console.WriteLine($"Level hot reload failed: {error.Message}");
        }
    }

    private void TryLoadLatestVersion()
    {
        if (hotReload is null)
            return;

        try
        {
            if (hotReload.TryTakePendingModel(out byte[] modelBytes, out string version))
                ApplyVersion(modelBytes, version, respawnCharacters: false);
        }
        catch (Exception error)
        {
            Console.WriteLine($"Initial level hot reload failed: {error.Message}");
        }
    }

    private void ApplyVersion(byte[] modelBytes, string version, bool respawnCharacters)
    {
        Level.ReloadContent(content, modelBytes, ModelName);
        if (respawnCharacters)
            SpawnCharacters();

        Version = version;
        Console.WriteLine($"Level hot reload applied: {version}");
    }

    private void SpawnCharacters()
    {
        player.SpawnAt(ResolveSpawnPosition(
            ActivePlayerSpawnMode,
            PlayerSpawnObjectName,
            GameplayCharacters.PlayerSpawnCoordinates,
            "Player"));
        skeleton.SpawnAt(ResolveSpawnPosition(
            ActiveSkeletonSpawnMode,
            SkeletonSpawnObjectName,
            GameplayCharacters.SkeletonSpawnCoordinates,
            "Skeleton"));
    }

    private Vector3 ResolveSpawnPosition(
        SpawnMode mode,
        string objectName,
        Vector3 fallbackCoordinates,
        string characterName)
    {
        if (mode == SpawnMode.Coordinates)
        {
            Console.WriteLine($"{characterName} spawn from coordinates: {fallbackCoordinates}");
            return fallbackCoordinates;
        }

        if (Level.TryGetMarkerPosition(objectName, out Vector3 markerPosition))
        {
            Console.WriteLine(
                $"{characterName} spawn from Blender Empty '{objectName}': " + markerPosition);
            return markerPosition;
        }

        Console.WriteLine(
            $"Blender Empty '{objectName}' was not found for {characterName}. " +
            $"Using fallback coordinates: {fallbackCoordinates}");
        return fallbackCoordinates;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        hotReload?.Dispose();
        hotReload = null;
        Level.Dispose();
    }
}
