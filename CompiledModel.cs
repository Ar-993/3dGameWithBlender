using _3DLight.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector3 = System.Numerics.Vector3;
using NumericsVector4 = System.Numerics.Vector4;

namespace _3DLight;

internal sealed class CompiledModel : IDisposable
{
    private const int MaximumBoneCount = 72;

    private readonly GraphicsDevice graphicsDevice;
    private readonly ModelData modelData;
    private readonly RuntimeMesh[] runtimeMeshes;
    private readonly Dictionary<string, ClipData> clipsByName;
    private readonly Texture2D[] ownedMaterialTextures;

    private readonly BonePose[] bindLocalPoses;
    private readonly BonePose[] sampledPose;
    private readonly Matrix[] localTransforms;
    private readonly Matrix[] globalTransforms;
    private readonly Matrix[] bindPoseGlobalTransforms;
    private readonly Matrix inverseRootTransform;

    public IEnumerable<string> ClipNames => clipsByName.Keys;
    public int NodeCount => modelData.Nodes.Count;

    private CompiledModel(
        GraphicsDevice graphicsDevice,
        ModelData modelData,
        Texture2D texture,
        Effect toonEffect,
        string textureFolderName)
    {
        this.graphicsDevice = graphicsDevice;
        this.modelData = modelData;

        clipsByName = modelData.Clips.ToDictionary(
            clip => clip.Name,
            StringComparer.OrdinalIgnoreCase);

        bindLocalPoses = clipsByName.Count == 0
            ? []
            : modelData.Nodes
                .Select(node => BonePose.FromMatrix(
                    ToXnaMatrix(node.Bind),
                    node.Name))
                .ToArray();

        sampledPose = new BonePose[bindLocalPoses.Length];
        localTransforms = modelData.Nodes
            .Select(node => ToXnaMatrix(node.Bind))
            .ToArray();

        globalTransforms = new Matrix[localTransforms.Length];
        bindPoseGlobalTransforms = new Matrix[localTransforms.Length];
        CalculateGlobalTransforms(localTransforms, bindPoseGlobalTransforms);

        inverseRootTransform = localTransforms.Length == 0
            ? Matrix.Identity
            : Matrix.Invert(bindPoseGlobalTransforms[0]);

        Dictionary<string, Texture2D> materialTextures =
            LoadMaterialTextures(graphicsDevice, textureFolderName);
        ownedMaterialTextures = materialTextures.Values.ToArray();

        runtimeMeshes = modelData.Meshes
            .Select(mesh => CreateRuntimeMesh(
                mesh,
                ResolveMeshTexture(mesh, materialTextures, texture),
                toonEffect))
            .ToArray();
    }

    public static CompiledModel Load(
        GraphicsDevice graphicsDevice,
        string modelName,
        Texture2D texture,
        Effect toonEffect)
    {
        string modelPath = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Models",
            modelName + ".3dmodel");

        using FileStream modelStream = File.OpenRead(modelPath);
        ModelData modelData = ModelDataIo.Read(modelStream);

        return new CompiledModel(
            graphicsDevice,
            modelData,
            texture,
            toonEffect,
            modelName);
    }

    public static CompiledModel LoadFromBytes(
        GraphicsDevice graphicsDevice,
        byte[] modelBytes,
        string modelName,
        Texture2D texture,
        Effect toonEffect)
    {
        using var modelStream = new MemoryStream(
            modelBytes,
            writable: false);
        ModelData modelData = ModelDataIo.Read(modelStream);

        return new CompiledModel(
            graphicsDevice,
            modelData,
            texture,
            toonEffect,
            modelName);
    }

    private static Dictionary<string, Texture2D> LoadMaterialTextures(
        GraphicsDevice graphicsDevice,
        string textureFolderName)
    {
        var textures = new Dictionary<string, Texture2D>(
            StringComparer.OrdinalIgnoreCase);
        string directory = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Assets",
            textureFolderName);

        if (!Directory.Exists(directory))
            return textures;

        foreach (string path in Directory.EnumerateFiles(directory, "*.png"))
        {
            using FileStream stream = File.OpenRead(path);
            textures[Path.GetFileName(path)] = Texture2D.FromStream(
                graphicsDevice,
                stream);
        }

        Console.WriteLine(
            $"Loaded {textures.Count} material textures from '{directory}'.");
        return textures;
    }

    private static Texture2D ResolveMeshTexture(
        MeshData mesh,
        IReadOnlyDictionary<string, Texture2D> materialTextures,
        Texture2D fallbackTexture)
    {
        if (!string.IsNullOrWhiteSpace(mesh.TextureName) &&
            materialTextures.TryGetValue(mesh.TextureName, out Texture2D? texture))
        {
            return texture;
        }

        if (!string.IsNullOrWhiteSpace(mesh.TextureName))
        {
            Console.WriteLine(
                $"Texture '{mesh.TextureName}' required by mesh '{mesh.Name}' " +
                "was not found; using the fallback texture.");
        }

        return fallbackTexture;
    }

    public float GetClipDuration(string clipName)
    {
        if (!clipsByName.TryGetValue(clipName, out ClipData? clip))
            throw new InvalidOperationException($"Анимация '{clipName}' отсутствует.");

        return (float)(clip.Duration / clip.TicksPerSecond);
    }

    public bool TryGetNodePosition(string nodeName, out Vector3 position)
    {
        for (int nodeIndex = 0; nodeIndex < modelData.Nodes.Count; nodeIndex++)
        {
            if (!string.Equals(
                    modelData.Nodes[nodeIndex].Name,
                    nodeName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            position = bindPoseGlobalTransforms[nodeIndex].Translation;
            return true;
        }

        position = default;
        return false;
    }

    public bool[] CreateNodeHierarchyMask(string rootNodeName)
    {
        int rootNodeIndex = modelData.Nodes.FindIndex(node =>
            string.Equals(
                node.Name,
                rootNodeName,
                StringComparison.OrdinalIgnoreCase));

        if (rootNodeIndex < 0)
        {
            throw new InvalidOperationException(
                $"Кость '{rootNodeName}' отсутствует в модели.");
        }

        var mask = new bool[modelData.Nodes.Count];
        mask[rootNodeIndex] = true;

        for (int nodeIndex = rootNodeIndex + 1;
             nodeIndex < modelData.Nodes.Count;
             nodeIndex++)
        {
            int parentIndex = modelData.Nodes[nodeIndex].Parent;
            mask[nodeIndex] = parentIndex >= 0 && mask[parentIndex];
        }

        return mask;
    }

    public void Draw(
        Matrix world,
        Matrix view,
        Matrix projection,
        SceneLighting lighting)
    {
        Array.Copy(
            bindPoseGlobalTransforms,
            globalTransforms,
            bindPoseGlobalTransforms.Length);

        DrawMeshes(world, view, projection, lighting);
    }

    public void Draw(
        string clipName,
        float elapsedSeconds,
        bool loop,
        Matrix world,
        Matrix view,
        Matrix projection,
        SceneLighting lighting)
    {
        SamplePose(clipName, elapsedSeconds, loop, sampledPose);
        DrawPose(sampledPose, world, view, projection, lighting);
    }

    public void SamplePose(
        string clipName,
        float elapsedSeconds,
        bool loop,
        BonePose[] destination)
    {
        if (!clipsByName.TryGetValue(clipName, out ClipData? clip))
            throw new InvalidOperationException($"Анимация '{clipName}' отсутствует.");

        ValidatePoseLength(destination);
        Array.Copy(bindLocalPoses, destination, bindLocalPoses.Length);

        double animationTick = CalculateAnimationTick(
            clip,
            elapsedSeconds,
            loop);

        foreach (ChannelData channel in clip.Channels)
        {
            BonePose bindPose = bindLocalPoses[channel.Node];

            Vector3 position = InterpolateVectorKeys(
                channel.Positions,
                animationTick,
                bindPose.Position);

            Quaternion rotation = InterpolateQuaternionKeys(
                channel.Rotations,
                animationTick,
                bindPose.Rotation);

            Vector3 scale = InterpolateVectorKeys(
                channel.Scales,
                animationTick,
                bindPose.Scale);

            destination[channel.Node] = new BonePose(position, rotation, scale);
        }
    }

    public void DrawPose(
        BonePose[] pose,
        Matrix world,
        Matrix view,
        Matrix projection,
        SceneLighting lighting)
    {
        ValidatePoseLength(pose);

        for (int nodeIndex = 0; nodeIndex < pose.Length; nodeIndex++)
            localTransforms[nodeIndex] = pose[nodeIndex].ToMatrix();

        CalculateGlobalTransforms(localTransforms, globalTransforms);
        DrawMeshes(world, view, projection, lighting);
    }

    public List<Level.Platform> BuildPlatforms()
    {
        var platforms = new List<Level.Platform>();

        for (int meshIndex = 0; meshIndex < modelData.Meshes.Count; meshIndex++)
        {
            MeshData mesh = modelData.Meshes[meshIndex];
            if (mesh.Vertices.Length == 0 || mesh.Indices.Length < 3)
                continue;

            // Имена узлов — только подписи. Любой меш уровня участвует
            // в коллизиях; Empty/маркеры без геометрии сюда не попадают.
            platforms.Add(new Level.Platform(
                meshIndex,
                modelData.Nodes[mesh.Node].Name,
                CalculateMeshBounds(mesh)));
        }

        if (platforms.Count == 0)
            throw new InvalidOperationException(
                "В скомпилированной модели уровня нет геометрии для коллизий.");

        return platforms;
    }

    public List<Level.TriangleCollider> BuildTriangleColliders()
    {
        const float minimumTriangleAreaSquared = 0.00000001f;
        var colliders = new List<Level.TriangleCollider>();

        for (int meshIndex = 0; meshIndex < modelData.Meshes.Count; meshIndex++)
        {
            MeshData mesh = modelData.Meshes[meshIndex];
            string nodeName = modelData.Nodes[mesh.Node].Name;
            if (mesh.Vertices.Length == 0 || mesh.Indices.Length < 3)
                continue;

            Vector3[] vertices = GetWorldVertices(mesh);
            // Одна опора на весь меш сохраняет идентичность платформы для ИИ.
            // Высота контакта вычисляется по грани, а не по этим общим границам.
            var support = new Level.Platform(
                meshIndex, nodeName, BoundingBox.CreateFromPoints(vertices));
            for (int index = 0; index + 2 < mesh.Indices.Length; index += 3)
            {
                Vector3 a = vertices[mesh.Indices[index]];
                Vector3 b = vertices[mesh.Indices[index + 1]];
                Vector3 c = vertices[mesh.Indices[index + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);

                if (normal.LengthSquared() <= minimumTriangleAreaSquared)
                    continue;

                normal.Normalize();
                colliders.Add(new Level.TriangleCollider(
                    nodeName,
                    a,
                    b,
                    c,
                    normal,
                    BoundingBox.CreateFromPoints(new[] { a, b, c }))
                {
                    SupportPlatform = support
                });
            }
        }

        return colliders;
    }


    private Vector3[] GetWorldVertices(MeshData mesh)
    {
        Matrix nodeTransform = bindPoseGlobalTransforms[mesh.Node];
        return mesh.Vertices
            .Select(vertex => Vector3.Transform(
                ToXnaVector3(vertex.Position),
                nodeTransform))
            .ToArray();
    }


    private void ValidatePoseLength(BonePose[] pose)
    {
        if (pose.Length != NodeCount)
        {
            throw new ArgumentException(
                $"Поза содержит {pose.Length} узлов вместо {NodeCount}.",
                nameof(pose));
        }
    }

    private static double CalculateAnimationTick(
        ClipData clip,
        float elapsedSeconds,
        bool loop)
    {
        double animationTick = elapsedSeconds * clip.TicksPerSecond;

        if (loop && clip.Duration > 0)
            return animationTick % clip.Duration;

        return Math.Min(animationTick, clip.Duration);
    }

    private BoundingBox CalculateMeshBounds(MeshData mesh)
    {
        var minimum = new Vector3(float.MaxValue);
        var maximum = new Vector3(float.MinValue);
        Matrix nodeTransform = bindPoseGlobalTransforms[mesh.Node];

        foreach (VertexData vertex in mesh.Vertices)
        {
            Vector3 localPosition = ToXnaVector3(vertex.Position);
            Vector3 worldPosition = Vector3.Transform(localPosition, nodeTransform);

            minimum = Vector3.Min(minimum, worldPosition);
            maximum = Vector3.Max(maximum, worldPosition);
        }

        return new BoundingBox(minimum, maximum);
    }


    private RuntimeMesh CreateRuntimeMesh(
        MeshData sourceMesh,
        Texture2D texture,
        Effect toonEffect)
    {
        RuntimeVertex[] vertices = ConvertVertices(sourceMesh.Vertices);

        var vertexBuffer = new VertexBuffer(
            graphicsDevice,
            RuntimeVertex.Declaration,
            vertices.Length,
            BufferUsage.WriteOnly);
        vertexBuffer.SetData(vertices);

        var indexBuffer = new IndexBuffer(
            graphicsDevice,
            IndexElementSize.ThirtyTwoBits,
            sourceMesh.Indices.Length,
            BufferUsage.WriteOnly);
        indexBuffer.SetData(sourceMesh.Indices);

        Effect meshEffect = toonEffect.Clone();
        Matrix[] boneTransforms = CreateIdentityBoneTransforms();

        return new RuntimeMesh(
            sourceMesh,
            vertexBuffer,
            indexBuffer,
            meshEffect,
            boneTransforms,
            texture);
    }

    private static RuntimeVertex[] ConvertVertices(VertexData[] sourceVertices)
    {
        var runtimeVertices = new RuntimeVertex[sourceVertices.Length];

        for (int vertexIndex = 0; vertexIndex < sourceVertices.Length; vertexIndex++)
        {
            VertexData sourceVertex = sourceVertices[vertexIndex];

            runtimeVertices[vertexIndex] = new RuntimeVertex(
                ToXnaVector3(sourceVertex.Position),
                ToXnaVector3(sourceVertex.Normal),
                new Vector2(sourceVertex.Uv.X, sourceVertex.Uv.Y),
                new Byte4(
                    sourceVertex.B0,
                    sourceVertex.B1,
                    sourceVertex.B2,
                    sourceVertex.B3),
                ToXnaVector4(sourceVertex.Weights));
        }

        return runtimeVertices;
    }

    private static Matrix[] CreateIdentityBoneTransforms()
    {
        var boneTransforms = new Matrix[MaximumBoneCount];

        for (int boneIndex = 0; boneIndex < boneTransforms.Length; boneIndex++)
            boneTransforms[boneIndex] = Matrix.Identity;

        return boneTransforms;
    }

    private void DrawMeshes(
        Matrix world,
        Matrix view,
        Matrix projection,
        SceneLighting lighting)
    {
        RasterizerState previousRasterizerState = graphicsDevice.RasterizerState;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;

        try
        {
            Vector3 cameraPosition = Matrix.Invert(view).Translation;

            foreach (RuntimeMesh runtimeMesh in runtimeMeshes)
            {
                DrawMesh(
                    runtimeMesh,
                    world,
                    view,
                    projection,
                    cameraPosition,
                    lighting);
            }
        }
        finally
        {
            graphicsDevice.RasterizerState = previousRasterizerState;
        }
    }

    private void DrawMesh(
        RuntimeMesh runtimeMesh,
        Matrix modelWorld,
        Matrix view,
        Matrix projection,
        Vector3 cameraPosition,
        SceneLighting lighting)
    {
        graphicsDevice.SetVertexBuffer(runtimeMesh.VertexBuffer);
        graphicsDevice.Indices = runtimeMesh.IndexBuffer;

        Matrix meshWorld = PrepareMeshTransforms(runtimeMesh, modelWorld);
        ApplyEffectParameters(
            runtimeMesh,
            meshWorld,
            view,
            projection,
            cameraPosition,
            lighting);

        int primitiveCount = runtimeMesh.Source.Indices.Length / 3;

        foreach (EffectPass effectPass in runtimeMesh.Effect.CurrentTechnique.Passes)
        {
            effectPass.Apply();
            graphicsDevice.DrawIndexedPrimitives(
                PrimitiveType.TriangleList,
                baseVertex: 0,
                startIndex: 0,
                primitiveCount);
        }
    }

    private Matrix PrepareMeshTransforms(RuntimeMesh runtimeMesh, Matrix modelWorld)
    {
        MeshData sourceMesh = runtimeMesh.Source;

        if (sourceMesh.Bones.Length == 0)
        {
            runtimeMesh.BoneTransforms[0] = Matrix.Identity;
            return globalTransforms[sourceMesh.Node] * modelWorld;
        }

        for (int boneIndex = 0; boneIndex < sourceMesh.Bones.Length; boneIndex++)
        {
            BoneData bone = sourceMesh.Bones[boneIndex];

            runtimeMesh.BoneTransforms[boneIndex] =
                ToXnaMatrix(bone.Offset) *
                globalTransforms[bone.Node] *
                inverseRootTransform;
        }

        return modelWorld;
    }

    private static void ApplyEffectParameters(
        RuntimeMesh runtimeMesh,
        Matrix world,
        Matrix view,
        Matrix projection,
        Vector3 cameraPosition,
        SceneLighting lighting)
    {
        EffectParameterCollection parameters = runtimeMesh.Effect.Parameters;

        parameters["World"]?.SetValue(world);
        parameters["View"]?.SetValue(view);
        parameters["Projection"]?.SetValue(projection);
        parameters["CameraPosition"]?.SetValue(cameraPosition);
        parameters["ModelTexture"]?.SetValue(runtimeMesh.Texture);
        parameters["Bones"]?.SetValue(runtimeMesh.BoneTransforms);
        lighting.Apply(parameters);
    }

    private void CalculateGlobalTransforms(
        Matrix[] sourceLocalTransforms,
        Matrix[] destinationGlobalTransforms)
    {
        for (int nodeIndex = 0; nodeIndex < sourceLocalTransforms.Length; nodeIndex++)
        {
            int parentIndex = modelData.Nodes[nodeIndex].Parent;
            Matrix localTransform = sourceLocalTransforms[nodeIndex];

            destinationGlobalTransforms[nodeIndex] = parentIndex < 0
                ? localTransform
                : localTransform * destinationGlobalTransforms[parentIndex];
        }
    }

    private static Vector3 InterpolateVectorKeys(
        VectorKey[] keys,
        double animationTick,
        Vector3 fallbackValue)
    {
        if (keys.Length == 0)
            return fallbackValue;

        int firstKeyIndex = FindKeyIndex(keys, animationTick, key => key.Time);

        if (firstKeyIndex == keys.Length - 1)
            return ToXnaVector3(keys[firstKeyIndex].Value);

        VectorKey firstKey = keys[firstKeyIndex];
        VectorKey secondKey = keys[firstKeyIndex + 1];
        float interpolationAmount = CalculateInterpolationAmount(
            animationTick,
            firstKey.Time,
            secondKey.Time);

        return Vector3.Lerp(
            ToXnaVector3(firstKey.Value),
            ToXnaVector3(secondKey.Value),
            interpolationAmount);
    }

    private static Quaternion InterpolateQuaternionKeys(
        QuaternionKey[] keys,
        double animationTick,
        Quaternion fallbackValue)
    {
        if (keys.Length == 0)
            return fallbackValue;

        int firstKeyIndex = FindKeyIndex(keys, animationTick, key => key.Time);

        if (firstKeyIndex == keys.Length - 1)
            return ToXnaQuaternion(keys[firstKeyIndex].Value);

        QuaternionKey firstKey = keys[firstKeyIndex];
        QuaternionKey secondKey = keys[firstKeyIndex + 1];
        float interpolationAmount = CalculateInterpolationAmount(
            animationTick,
            firstKey.Time,
            secondKey.Time);

        Quaternion interpolatedRotation = Quaternion.Slerp(
            ToXnaQuaternion(firstKey.Value),
            ToXnaQuaternion(secondKey.Value),
            interpolationAmount);

        return Quaternion.Normalize(interpolatedRotation);
    }

    private static float CalculateInterpolationAmount(
        double currentTime,
        double firstKeyTime,
        double secondKeyTime)
    {
        double interval = Math.Max(secondKeyTime - firstKeyTime, double.Epsilon);
        double amount = (currentTime - firstKeyTime) / interval;
        return MathHelper.Clamp((float)amount, 0f, 1f);
    }

    private static int FindKeyIndex<T>(
        T[] keys,
        double animationTick,
        Func<T, double> getKeyTime)
    {
        int lowerIndex = 0;
        int upperIndex = keys.Length - 1;

        while (lowerIndex < upperIndex)
        {
            int middleIndex = (lowerIndex + upperIndex + 1) / 2;

            if (getKeyTime(keys[middleIndex]) <= animationTick)
                lowerIndex = middleIndex;
            else
                upperIndex = middleIndex - 1;
        }

        return lowerIndex;
    }

    public void Dispose()
    {
        foreach (RuntimeMesh runtimeMesh in runtimeMeshes)
        {
            runtimeMesh.VertexBuffer.Dispose();
            runtimeMesh.IndexBuffer.Dispose();
            runtimeMesh.Effect.Dispose();
        }

        foreach (Texture2D texture in ownedMaterialTextures)
            texture.Dispose();
    }

    private static Matrix ToXnaMatrix(NumericsMatrix matrix) => new(
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44);

    private static Vector3 ToXnaVector3(NumericsVector3 vector) =>
        new(vector.X, vector.Y, vector.Z);

    private static Vector4 ToXnaVector4(NumericsVector4 vector) =>
        new(vector.X, vector.Y, vector.Z, vector.W);

    private static Quaternion ToXnaQuaternion(NumericsQuaternion quaternion) =>
        new(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);

    private sealed record RuntimeMesh(
        MeshData Source,
        VertexBuffer VertexBuffer,
        IndexBuffer IndexBuffer,
        Effect Effect,
        Matrix[] BoneTransforms,
        Texture2D Texture);

}

internal readonly record struct BonePose(
    Vector3 Position,
    Quaternion Rotation,
    Vector3 Scale)
{
    public static BonePose FromMatrix(Matrix matrix, string nodeName)
    {
        if (!matrix.Decompose(
                out Vector3 scale,
                out Quaternion rotation,
                out Vector3 position))
        {
            throw new InvalidDataException(
                $"Локальную трансформацию узла '{nodeName}' нельзя разложить на TRS.");
        }

        return new BonePose(
            position,
            Quaternion.Normalize(rotation),
            scale);
    }

    public static BonePose Blend(BonePose source, BonePose target, float amount) =>
        new(
            Vector3.Lerp(source.Position, target.Position, amount),
            Quaternion.Normalize(Quaternion.Slerp(
                source.Rotation,
                target.Rotation,
                amount)),
            Vector3.Lerp(source.Scale, target.Scale, amount));

    public Matrix ToMatrix() =>
        Matrix.CreateScale(Scale) *
        Matrix.CreateFromQuaternion(Rotation) *
        Matrix.CreateTranslation(Position);
}

internal readonly struct RuntimeVertex : IVertexType
{
    public static readonly VertexDeclaration Declaration = new(
        new VertexElement(
            offset: 0,
            VertexElementFormat.Vector3,
            VertexElementUsage.Position,
            usageIndex: 0),
        new VertexElement(
            offset: 12,
            VertexElementFormat.Vector3,
            VertexElementUsage.Normal,
            usageIndex: 0),
        new VertexElement(
            offset: 24,
            VertexElementFormat.Vector2,
            VertexElementUsage.TextureCoordinate,
            usageIndex: 0),
        new VertexElement(
            offset: 32,
            VertexElementFormat.Byte4,
            VertexElementUsage.BlendIndices,
            usageIndex: 0),
        new VertexElement(
            offset: 36,
            VertexElementFormat.Vector4,
            VertexElementUsage.BlendWeight,
            usageIndex: 0));

    private readonly Vector3 position;
    private readonly Vector3 normal;
    private readonly Vector2 textureCoordinate;
    private readonly Byte4 boneIndices;
    private readonly Vector4 boneWeights;

    public RuntimeVertex(
        Vector3 position,
        Vector3 normal,
        Vector2 textureCoordinate,
        Byte4 boneIndices,
        Vector4 boneWeights)
    {
        this.position = position;
        this.normal = normal;
        this.textureCoordinate = textureCoordinate;
        this.boneIndices = boneIndices;
        this.boneWeights = boneWeights;
    }

    VertexDeclaration IVertexType.VertexDeclaration => Declaration;
}
