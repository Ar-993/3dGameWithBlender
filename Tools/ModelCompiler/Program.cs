using System.Text.Json;
using Ars3D.ModelFormat;
using System.Numerics;

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

// Один FBX может содержать несколько предметов. Извлекаем только нужный узел и его потомков.
static ModelData ExtractStaticItem(ModelData source, string rootName, float[] rotationDegrees)
{
    int root = source.Nodes.FindIndex(node => node.Name.Equals(rootName, StringComparison.OrdinalIgnoreCase));
    if (root < 0)
        throw new InvalidDataException($"Item root '{rootName}' was not found.");

    Matrix4x4 rotation = ItemRotation(rotationDegrees);
    (Matrix4x4[] global, bool[] included) = FindSubtree(source, root);
    MeshData[] meshes = source.Meshes.Where(mesh => included[mesh.Node]).ToArray();
    if (meshes.Length == 0 || meshes.Any(mesh => mesh.Bones.Length != 0))
        throw new InvalidDataException($"'{rootName}' must contain static item meshes.");

    VertexData[][] vertices = meshes.Select(mesh =>
        BakeVertices(mesh, global[mesh.Node] * rotation)).ToArray();

    ModelData result = AssembleStaticItem(rootName, meshes, vertices);
    Console.WriteLine($"  Extracted '{rootName}': {result.Meshes.Count} meshes, no bones required.");
    return result;
}

static Matrix4x4 ItemRotation(float[] degrees)
{
    if (degrees.Length != 3 || degrees.Any(value => !float.IsFinite(value)))
        throw new InvalidDataException("rotationDegrees needs three finite numbers.");

    const float toRadians = MathF.PI / 180f;
    return Matrix4x4.CreateRotationX(degrees[0] * toRadians) *
        Matrix4x4.CreateRotationY(degrees[1] * toRadians) *
        Matrix4x4.CreateRotationZ(degrees[2] * toRadians);
}

static ModelData AssembleStaticItem(string rootName, MeshData[] meshes, VertexData[][] vertices)
{
    // Нижний центр модели становится точкой (0, 0, 0) в системе координат предмета.
    Vector3 pivot = BottomCenter(vertices.SelectMany(mesh => mesh));
    var result = new ModelData();
    result.Nodes.Add(new(rootName, -1, Matrix4x4.Identity));
    for (int index = 0; index < meshes.Length; index++)
    {
        MeshData mesh = meshes[index];
        result.Meshes.Add(new MeshData
        {
            Name = mesh.Name,
            Node = 0,
            TextureName = mesh.TextureName,
            MaterialName = mesh.MaterialName,
            MaterialOpacity = mesh.MaterialOpacity,
            Vertices = vertices[index].Select(vertex => vertex with { Position = vertex.Position - pivot }).ToArray(),
            Indices = mesh.Indices
        });
    }
    return result;
}

static (Matrix4x4[] Global, bool[] Included) FindSubtree(ModelData source, int root)
{
    var global = new Matrix4x4[source.Nodes.Count];
    var included = new bool[source.Nodes.Count];
    for (int index = 0; index < source.Nodes.Count; index++)
    {
        NodeData node = source.Nodes[index];
        global[index] = node.Parent < 0 ? node.Bind : node.Bind * global[node.Parent];
        included[index] = index == root || (node.Parent >= 0 && included[node.Parent]);
    }
    return (global, included);
}

static VertexData[] BakeVertices(MeshData mesh, Matrix4x4 transform)
{
    if (!Matrix4x4.Invert(transform, out Matrix4x4 inverse))
        throw new InvalidDataException($"Item mesh '{mesh.Name}' contains a non-invertible transform.");

    // При неравномерном масштабе нормалям нужна обратная транспонированная матрица.
    Matrix4x4 normalTransform = Matrix4x4.Transpose(inverse);
    return mesh.Vertices.Select(vertex => vertex with
    {
        Position = Vector3.Transform(vertex.Position, transform),
        Normal = Vector3.Normalize(Vector3.TransformNormal(vertex.Normal, normalTransform))
    }).ToArray();
}

static Vector3 BottomCenter(IEnumerable<VertexData> vertices)
{
    Vector3 minimum = new(float.PositiveInfinity);
    Vector3 maximum = new(float.NegativeInfinity);
    foreach (VertexData vertex in vertices)
    {
        minimum = Vector3.Min(minimum, vertex.Position);
        maximum = Vector3.Max(maximum, vertex.Position);
    }
    return new((minimum.X + maximum.X) * 0.5f, minimum.Y, (minimum.Z + maximum.Z) * 0.5f);
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

    if (modelEntry.RootNode is { } root)
        model = ExtractStaticItem(model, root, modelEntry.RotationDegrees);

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
    public string? RootNode { get; set; }
    public float[] RotationDegrees { get; set; } = [0f, 0f, 0f];
}
