using System.Text.Json;
using Ars3D.ModelFormat;

if (args is ["--single", string singleSource, string singleOutput])
{
    CompileSingleModel(singleSource, singleOutput);
    return 0;
}

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage:\n" +
        "  ModelCompiler <models.json>\n" +
        "  ModelCompiler --single <source.fbx> <output.3dmodel>");
    return 2;
}

string manifestPath = Path.GetFullPath(args[0]);
string manifestDirectory = Path.GetDirectoryName(manifestPath)!;
ModelManifest manifest = ReadManifest(manifestPath);

foreach (ModelEntry modelEntry in manifest.Models)
    CompileManifestEntry(modelEntry, manifestDirectory);

return 0;

static void CompileSingleModel(string sourceArgument, string outputArgument)
{
    string sourcePath = Path.GetFullPath(sourceArgument);
    string outputPath = Path.GetFullPath(outputArgument);
    string sourceDirectory = Path.GetDirectoryName(sourcePath)!;

    Console.WriteLine($"Compiling hot-reload level '{sourcePath}'...");

    ModelData model = AssimpModelImporter.Import(
        sourcePath,
        clipFiles: null,
        manifestDirectory: sourceDirectory);

    ModelDataIo.Write(outputPath, model);
    Console.WriteLine($"  -> {outputPath}");
}

static ModelManifest ReadManifest(string manifestPath)
{
    string manifestJson = File.ReadAllText(manifestPath);
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    return JsonSerializer.Deserialize<ModelManifest>(manifestJson, jsonOptions)
        ?? throw new InvalidDataException("Пустой manifest моделей.");
}

static void CompileManifestEntry(
    ModelEntry modelEntry,
    string manifestDirectory)
{
    string sourcePath = ResolvePath(manifestDirectory, modelEntry.Source);
    string outputPath = ResolvePath(manifestDirectory, modelEntry.Output);

    Console.WriteLine($"Compiling {modelEntry.Name}...");

    ModelData model = AssimpModelImporter.Import(
        sourcePath,
        modelEntry.Clips,
        manifestDirectory);

    ModelDataIo.Write(outputPath, model);
    Console.WriteLine($"  -> {outputPath}");
}

static string ResolvePath(string directory, string path) =>
    Path.GetFullPath(Path.Combine(directory, path));

internal sealed class ModelManifest
{
    public List<ModelEntry> Models { get; set; } = [];
}

internal sealed class ModelEntry
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string Output { get; set; } = "";
    public Dictionary<string, string>? Clips { get; set; }
}
