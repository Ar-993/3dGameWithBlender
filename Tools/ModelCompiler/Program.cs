using System.Numerics;
using System.Text.Json;
using Assimp;
using _3DLight.Assets;
using AssetQuaternionKey = _3DLight.Assets.QuaternionKey;
using AssetVectorKey = _3DLight.Assets.VectorKey;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ModelCompiler <models.json>");
    return 2;
}

string manifestPath = Path.GetFullPath(args[0]);
string manifestDirectory = Path.GetDirectoryName(manifestPath)!;
ModelManifest manifest = ReadManifest(manifestPath);

foreach (ModelEntry modelEntry in manifest.Models)
    CompileManifestEntry(modelEntry, manifestDirectory);

return 0;

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

    ModelData model = CompileModel(
        sourcePath,
        modelEntry.Clips,
        manifestDirectory);

    ModelDataIo.Write(outputPath, model);
    Console.WriteLine($"  -> {outputPath}");
}

static ModelData CompileModel(
    string sourcePath,
    Dictionary<string, string>? clipFiles,
    string manifestDirectory)
{
    using var importer = new AssimpContext();
    PostProcessSteps importFlags = GetImportFlags();

    Scene sourceScene = importer.ImportFile(sourcePath, importFlags)
        ?? throw new InvalidDataException(
            $"Assimp не смог импортировать '{sourcePath}'.");

    bool transposeMatrices = ChooseMatrixLayout(sourceScene);
    var model = new ModelData();
    var nodeIndices = new Dictionary<string, int>(StringComparer.Ordinal);

    AddNodeHierarchy(
        sourceScene.RootNode,
        parentIndex: -1,
        model,
        nodeIndices,
        transposeMatrices);

    int[] meshOwnerNodes = FindMeshOwnerNodes(sourceScene, nodeIndices);
    AddMeshes(
        sourceScene,
        model,
        meshOwnerNodes,
        nodeIndices,
        transposeMatrices);

    AddAnimationClips(
        importer,
        sourceScene,
        sourcePath,
        clipFiles,
        manifestDirectory,
        importFlags,
        nodeIndices,
        model);

    string matrixLayout = transposeMatrices ? "transposed" : "direct";
    Console.WriteLine(
        $"  {model.Meshes.Count} meshes, " +
        $"{model.Nodes.Count} nodes, " +
        $"{model.Clips.Count} clips, " +
        $"matrices {matrixLayout}");

    return model;
}

static PostProcessSteps GetImportFlags() =>
    PostProcessSteps.Triangulate |
    PostProcessSteps.JoinIdenticalVertices |
    PostProcessSteps.GenerateSmoothNormals |
    PostProcessSteps.FlipUVs |
    PostProcessSteps.LimitBoneWeights |
    PostProcessSteps.FlipWindingOrder;

static void AddNodeHierarchy(
    Assimp.Node sourceNode,
    int parentIndex,
    ModelData model,
    Dictionary<string, int> nodeIndices,
    bool transposeMatrices)
{
    if (nodeIndices.ContainsKey(sourceNode.Name))
    {
        throw new InvalidDataException(
            $"Узлы модели должны иметь уникальные имена: '{sourceNode.Name}'.");
    }

    int nodeIndex = model.Nodes.Count;
    nodeIndices.Add(sourceNode.Name, nodeIndex);

    model.Nodes.Add(new NodeData(
        sourceNode.Name,
        parentIndex,
        ConvertMatrix(sourceNode.Transform, transposeMatrices)));

    foreach (Assimp.Node childNode in sourceNode.Children)
    {
        AddNodeHierarchy(
            childNode,
            nodeIndex,
            model,
            nodeIndices,
            transposeMatrices);
    }
}

static int[] FindMeshOwnerNodes(
    Scene scene,
    Dictionary<string, int> nodeIndices)
{
    var ownerNodes = new int[scene.MeshCount];

    void VisitNode(Assimp.Node node)
    {
        int nodeIndex = nodeIndices[node.Name];

        foreach (int meshIndex in node.MeshIndices)
            ownerNodes[meshIndex] = nodeIndex;

        foreach (Assimp.Node childNode in node.Children)
            VisitNode(childNode);
    }

    VisitNode(scene.RootNode);
    return ownerNodes;
}

static void AddMeshes(
    Scene scene,
    ModelData model,
    int[] meshOwnerNodes,
    Dictionary<string, int> nodeIndices,
    bool transposeMatrices)
{
    for (int meshIndex = 0; meshIndex < scene.MeshCount; meshIndex++)
    {
        MeshData mesh = ConvertMesh(
            scene.Meshes[meshIndex],
            meshOwnerNodes[meshIndex],
            nodeIndices,
            transposeMatrices);

        model.Meshes.Add(mesh);
    }
}

static MeshData ConvertMesh(
    Assimp.Mesh sourceMesh,
    int ownerNodeIndex,
    Dictionary<string, int> nodeIndices,
    bool transposeMatrices)
{
    const int maximumBoneCount = 72;

    if (sourceMesh.BoneCount > maximumBoneCount)
    {
        throw new InvalidDataException(
            $"Меш '{sourceMesh.Name}' содержит {sourceMesh.BoneCount} костей; " +
            $"максимум — {maximumBoneCount}.");
    }

    List<VertexInfluence>[] vertexInfluences =
        CreateVertexInfluenceLists(sourceMesh.VertexCount);

    BoneData[] bones = ConvertBones(
        sourceMesh,
        nodeIndices,
        transposeMatrices,
        vertexInfluences);

    VertexData[] vertices = ConvertVertices(sourceMesh, vertexInfluences);

    return new MeshData
    {
        Name = sourceMesh.Name,
        Node = ownerNodeIndex,
        Vertices = vertices,
        Indices = sourceMesh.GetIndices().ToArray(),
        Bones = bones
    };
}

static List<VertexInfluence>[] CreateVertexInfluenceLists(int vertexCount)
{
    var influences = new List<VertexInfluence>[vertexCount];

    for (int vertexIndex = 0; vertexIndex < influences.Length; vertexIndex++)
        influences[vertexIndex] = [];

    return influences;
}

static BoneData[] ConvertBones(
    Assimp.Mesh sourceMesh,
    Dictionary<string, int> nodeIndices,
    bool transposeMatrices,
    List<VertexInfluence>[] vertexInfluences)
{
    var bones = new BoneData[sourceMesh.BoneCount];

    for (int boneIndex = 0; boneIndex < sourceMesh.BoneCount; boneIndex++)
    {
        Bone sourceBone = sourceMesh.Bones[boneIndex];

        if (!nodeIndices.TryGetValue(sourceBone.Name, out int nodeIndex))
        {
            throw new InvalidDataException(
                $"Кость '{sourceBone.Name}' отсутствует в иерархии узлов.");
        }

        bones[boneIndex] = new BoneData(
            nodeIndex,
            ConvertMatrix(sourceBone.OffsetMatrix, transposeMatrices));

        foreach (VertexWeight vertexWeight in sourceBone.VertexWeights)
        {
            vertexInfluences[vertexWeight.VertexID].Add(new VertexInfluence(
                boneIndex,
                vertexWeight.Weight));
        }
    }

    return bones;
}

static VertexData[] ConvertVertices(
    Assimp.Mesh sourceMesh,
    List<VertexInfluence>[] vertexInfluences)
{
    var vertices = new VertexData[sourceMesh.VertexCount];

    for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
    {
        List<VertexInfluence> influences = vertexInfluences[vertexIndex];
        influences.Sort((first, second) => second.Weight.CompareTo(first.Weight));

        float totalWeight = influences
            .Take(4)
            .Sum(influence => influence.Weight);

        if (totalWeight <= 0f)
            totalWeight = 1f;

        Vector3 position = sourceMesh.Vertices[vertexIndex];
        Vector3 normal = sourceMesh.HasNormals
            ? sourceMesh.Normals[vertexIndex]
            : Vector3.UnitY;
        Vector3 textureCoordinate = sourceMesh.HasTextureCoords(0)
            ? sourceMesh.TextureCoordinateChannels[0][vertexIndex]
            : Vector3.Zero;

        vertices[vertexIndex] = new VertexData(
            position,
            normal,
            new Vector2(textureCoordinate.X, textureCoordinate.Y),
            GetBoneIndex(influences, 0),
            GetBoneIndex(influences, 1),
            GetBoneIndex(influences, 2),
            GetBoneIndex(influences, 3),
            new Vector4(
                GetBoneWeight(influences, 0, totalWeight),
                GetBoneWeight(influences, 1, totalWeight),
                GetBoneWeight(influences, 2, totalWeight),
                GetBoneWeight(influences, 3, totalWeight)));
    }

    return vertices;
}

static byte GetBoneIndex(List<VertexInfluence> influences, int influenceIndex)
{
    if (influenceIndex >= influences.Count)
        return 0;

    return checked((byte)influences[influenceIndex].BoneIndex);
}

static float GetBoneWeight(
    List<VertexInfluence> influences,
    int influenceIndex,
    float totalWeight)
{
    if (influenceIndex < influences.Count)
        return influences[influenceIndex].Weight / totalWeight;

    return influenceIndex == 0 ? 1f : 0f;
}

static void AddAnimationClips(
    AssimpContext importer,
    Scene sourceScene,
    string sourcePath,
    Dictionary<string, string>? clipFiles,
    string manifestDirectory,
    PostProcessSteps importFlags,
    Dictionary<string, int> nodeIndices,
    ModelData model)
{
    if (clipFiles is null)
        return;

    foreach ((string clipName, string relativeClipPath) in clipFiles)
    {
        string clipPath = ResolvePath(manifestDirectory, relativeClipPath);
        Scene clipScene = PathsAreEqual(clipPath, sourcePath)
            ? sourceScene
            : importer.ImportFile(clipPath, importFlags);

        model.Clips.Add(ConvertClip(clipName, clipScene, nodeIndices));
    }
}

static ClipData ConvertClip(
    string clipName,
    Scene scene,
    Dictionary<string, int> nodeIndices)
{
    if (scene.AnimationCount == 0)
    {
        throw new InvalidDataException(
            $"Файл клипа '{clipName}' не содержит анимацию.");
    }

    Animation sourceAnimation = scene.Animations[0];
    var clip = new ClipData
    {
        Name = clipName,
        Duration = sourceAnimation.DurationInTicks,
        TicksPerSecond = sourceAnimation.TicksPerSecond > 0
            ? sourceAnimation.TicksPerSecond
            : 25
    };

    foreach (NodeAnimationChannel sourceChannel in sourceAnimation.NodeAnimationChannels)
    {
        if (!nodeIndices.TryGetValue(sourceChannel.NodeName, out int nodeIndex))
            continue;

        clip.Channels.Add(new ChannelData
        {
            Node = nodeIndex,
            Positions = sourceChannel.PositionKeys
                .Select(key => new AssetVectorKey(key.Time, key.Value))
                .ToArray(),
            Rotations = sourceChannel.RotationKeys
                .Select(key => new AssetQuaternionKey(key.Time, key.Value))
                .ToArray(),
            Scales = sourceChannel.ScalingKeys
                .Select(key => new AssetVectorKey(key.Time, key.Value))
                .ToArray()
        });
    }

    return clip;
}

static bool ChooseMatrixLayout(Scene scene)
{
    if (!scene.Meshes.Any(mesh => mesh.HasBones))
        return true;

    float directError = CalculateBindPoseError(scene, transposeMatrices: false);
    float transposedError = CalculateBindPoseError(scene, transposeMatrices: true);

    return transposedError < directError;
}

static float CalculateBindPoseError(Scene scene, bool transposeMatrices)
{
    var globalTransforms = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);

    void VisitNode(Assimp.Node node, Matrix4x4 parentTransform)
    {
        Matrix4x4 localTransform = ConvertMatrix(node.Transform, transposeMatrices);
        Matrix4x4 globalTransform = localTransform * parentTransform;
        globalTransforms[node.Name] = globalTransform;

        foreach (Assimp.Node childNode in node.Children)
            VisitNode(childNode, globalTransform);
    }

    VisitNode(scene.RootNode, Matrix4x4.Identity);

    Matrix4x4 rootTransform = ConvertMatrix(
        scene.RootNode.Transform,
        transposeMatrices);
    Matrix4x4.Invert(rootTransform, out Matrix4x4 inverseRootTransform);

    float totalError = 0f;
    int measuredBoneCount = 0;

    foreach (Assimp.Mesh mesh in scene.Meshes)
    {
        foreach (Bone bone in mesh.Bones)
        {
            if (!globalTransforms.TryGetValue(bone.Name, out Matrix4x4 globalTransform))
                continue;

            Matrix4x4 offset = ConvertMatrix(bone.OffsetMatrix, transposeMatrices);
            Matrix4x4 skinTransform =
                offset * globalTransform * inverseRootTransform;

            totalError += CalculateIdentityError(skinTransform);
            measuredBoneCount++;
        }
    }

    return measuredBoneCount == 0
        ? float.MaxValue
        : totalError / measuredBoneCount;
}

static float CalculateIdentityError(Matrix4x4 matrix) =>
    MathF.Abs(matrix.M11 - 1) +
    MathF.Abs(matrix.M22 - 1) +
    MathF.Abs(matrix.M33 - 1) +
    MathF.Abs(matrix.M44 - 1) +
    MathF.Abs(matrix.M12) +
    MathF.Abs(matrix.M13) +
    MathF.Abs(matrix.M14) +
    MathF.Abs(matrix.M21) +
    MathF.Abs(matrix.M23) +
    MathF.Abs(matrix.M24) +
    MathF.Abs(matrix.M31) +
    MathF.Abs(matrix.M32) +
    MathF.Abs(matrix.M34) +
    MathF.Abs(matrix.M41) +
    MathF.Abs(matrix.M42) +
    MathF.Abs(matrix.M43);

static Matrix4x4 ConvertMatrix(Matrix4x4 matrix, bool transposeMatrices) =>
    transposeMatrices
        ? Matrix4x4.Transpose(matrix)
        : matrix;

static bool PathsAreEqual(string firstPath, string secondPath) =>
    Path.GetFullPath(firstPath).Equals(
        Path.GetFullPath(secondPath),
        StringComparison.OrdinalIgnoreCase);

static string ResolvePath(string directory, string path) =>
    Path.GetFullPath(Path.Combine(directory, path));

internal readonly record struct VertexInfluence(
    int BoneIndex,
    float Weight);

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
