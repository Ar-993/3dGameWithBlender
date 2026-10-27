using System.Text.Json;

internal sealed class LevelHotReload : IDisposable
{
    private readonly string versionsDirectory;
    private readonly string pointerPath;
    private readonly FileSystemWatcher watcher;
    private int reloadRequested = 1;
    private string loadedVersion = "";

    private LevelHotReload(string projectDirectory)
    {
        versionsDirectory = Path.GetFullPath(Path.Combine(
            projectDirectory,
            "Content",
            "Models",
            "LevelVersions"));
        pointerPath = Path.Combine(versionsDirectory, "latest.json");
        Directory.CreateDirectory(versionsDirectory);

        watcher = new FileSystemWatcher(versionsDirectory)
        {
            NotifyFilter =
                NotifyFilters.FileName |
                NotifyFilters.LastWrite |
                NotifyFilters.CreationTime |
                NotifyFilters.Size,
            IncludeSubdirectories = false
        };
        watcher.Created += OnPointerChanged;
        watcher.Changed += OnPointerChanged;
        watcher.Renamed += OnPointerChanged;
        watcher.Error += OnWatcherError;
        watcher.EnableRaisingEvents = true;
    }

    public static LevelHotReload? TryCreate()
    {
        string? projectDirectory = FindProjectDirectory();

        if (projectDirectory is null)
        {
            Console.WriteLine(
                "Level hot reload disabled: Assets/models.json was not found.");
            return null;
        }

        Console.WriteLine($"Level hot reload directory: {projectDirectory}");
        return new LevelHotReload(projectDirectory);
    }

    public bool TryTakePendingModel(
        out byte[] modelBytes,
        out string version)
    {
        modelBytes = [];
        version = "";

        if (Interlocked.Exchange(ref reloadRequested, 0) == 0)
            return false;

        if (!File.Exists(pointerPath))
            return false;

        string json = File.ReadAllText(pointerPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        LevelVersionPointer pointer = JsonSerializer.Deserialize<LevelVersionPointer>(
            json,
            options) ?? throw new InvalidDataException("latest.json is empty.");

        if (string.IsNullOrWhiteSpace(pointer.ModelFile))
            throw new InvalidDataException("latest.json does not contain modelFile.");

        if (pointer.Version == loadedVersion)
            return false;

        string candidatePath = Path.GetFullPath(Path.Combine(
            versionsDirectory,
            pointer.ModelFile));
        string allowedPrefix = versionsDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!candidatePath.StartsWith(
                allowedPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "latest.json points outside the level versions directory.");
        }

        if (!File.Exists(candidatePath))
            throw new FileNotFoundException("Hot-reload model was not found.", candidatePath);

        modelBytes = File.ReadAllBytes(candidatePath);
        version = pointer.Version;
        loadedVersion = pointer.Version;
        return true;
    }

    private void OnPointerChanged(object sender, FileSystemEventArgs eventArgs)
    {
        if (Path.GetFileName(eventArgs.FullPath).Equals(
                "latest.json",
                StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Exchange(ref reloadRequested, 1);
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs eventArgs)
    {
        Console.WriteLine(
            $"Level hot reload watcher error: {eventArgs.GetException().Message}");
        Interlocked.Exchange(ref reloadRequested, 1);
    }

    private static string? FindProjectDirectory()
    {
        string? configuredDirectory = Environment.GetEnvironmentVariable(
            "ARS3D_PROJECT_FOLDER");

        if (!string.IsNullOrWhiteSpace(configuredDirectory) &&
            IsProjectDirectory(configuredDirectory))
        {
            return Path.GetFullPath(configuredDirectory);
        }

        string[] startingDirectories =
        [
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        ];

        foreach (string startingDirectory in startingDirectories)
        {
            var directory = new DirectoryInfo(startingDirectory);

            while (directory is not null)
            {
                if (IsProjectDirectory(directory.FullName))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static bool IsProjectDirectory(string directory) =>
        File.Exists(Path.Combine(directory, "Assets", "models.json"));

    public void Dispose()
    {
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
    }

    private sealed class LevelVersionPointer
    {
        public string Version { get; set; } = "unknown";
        public string ModelFile { get; set; } = "";
    }
}
