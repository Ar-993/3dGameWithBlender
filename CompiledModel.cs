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
        Effect toonEffect)
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

        runtimeMeshes = modelData.Meshes
            .Select(mesh => CreateRuntimeMesh(mesh, texture, toonEffect))
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
            toonEffect);
    }

    public static CompiledModel LoadFromBytes(
        GraphicsDevice graphicsDevice,
        byte[] modelBytes,
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
            toonEffect);
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

        foreach (MeshData mesh in modelData.Meshes)
        {
            string nodeName = modelData.Nodes[mesh.Node].Name;

            if (!IsCollidableLevelNode(nodeName) || mesh.Vertices.Length == 0)
                continue;

            BoundingBox meshBounds = CalculateMeshBounds(mesh);
            float width = meshBounds.Max.X - meshBounds.Min.X;
            float depth = meshBounds.Max.Z - meshBounds.Min.Z;

            // Пол должен быть достаточно большим хотя бы по одной горизонтальной
            // оси: тонкие стены тоже участвуют в столкновениях.
            if (width < 0.5f && depth < 0.5f)
                continue;

            platforms.Add(new Level.Platform(
                platforms.Count,
                nodeName,
                meshBounds));
        }

        if (platforms.Count == 0)
            throw new InvalidOperationException(
                "В скомпилированной модели уровня нет платформ.");

        return MergeAdjacentFloorTiles(platforms);
    }

    private static List<Level.Platform> MergeAdjacentFloorTiles(
        List<Level.Platform> sourcePlatforms)
    {
        const float maximumFloorHeightDifference = 0.08f;
        const float maximumTileGap = 0.12f;

        var merged = new List<Level.Platform>();

        foreach (Level.Platform source in sourcePlatforms)
        {
            if (!IsMergeableFloorNode(source.Name))
            {
                merged.Add(source);
                continue;
            }

            BoundingBox combinedBounds = source.Bounds;
            bool absorbedAnotherTile;

            // Повторяем проход, чтобы собрать всю связанную область плиток,
            // а не только непосредственных соседей первой плитки.
            do
            {
                absorbedAnotherTile = false;

                for (int index = merged.Count - 1; index >= 0; index--)
                {
                    Level.Platform candidate = merged[index];

                    if (!IsMergeableFloorNode(candidate.Name) ||
                        MathF.Abs(candidate.SurfaceY - combinedBounds.Max.Y) >
                            maximumFloorHeightDifference ||
                        !AreHorizontallyConnected(
                            candidate.Bounds,
                            combinedBounds,
                            maximumTileGap))
                    {
                        continue;
                    }

                    combinedBounds = BoundingBox.CreateMerged(
                        combinedBounds,
                        candidate.Bounds);
                    merged.RemoveAt(index);
                    absorbedAnotherTile = true;
                }
            }
            while (absorbedAnotherTile);

            merged.Add(new Level.Platform(0, source.Name, combinedBounds));
        }

        for (int index = 0; index < merged.Count; index++)
            merged[index] = merged[index] with { Id = index };

        return merged;
    }

    private static bool AreHorizontallyConnected(
        BoundingBox first,
        BoundingBox second,
        float maximumGap)
    {
        float gapX = MathF.Max(
            0f,
            MathF.Max(first.Min.X - second.Max.X, second.Min.X - first.Max.X));
        float gapZ = MathF.Max(
            0f,
            MathF.Max(first.Min.Z - second.Max.Z, second.Min.Z - first.Max.Z));

        return gapX <= maximumGap && gapZ <= maximumGap;
    }

    private static bool IsMergeableFloorNode(string nodeName) =>
        nodeName.StartsWith("Ground", StringComparison.OrdinalIgnoreCase) ||
        nodeName.StartsWith("Floor", StringComparison.OrdinalIgnoreCase) ||
        nodeName.StartsWith("Bridge", StringComparison.OrdinalIgnoreCase) ||
        nodeName.StartsWith("Trail", StringComparison.OrdinalIgnoreCase) ||
        nodeName.Contains("Platform", StringComparison.OrdinalIgnoreCase);

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

    private static bool IsCollidableLevelNode(string nodeName) =>
        nodeName.StartsWith("Ground", StringComparison.OrdinalIgnoreCase) ||
        nodeName.StartsWith("Floor", StringComparison.OrdinalIgnoreCase) ||
        nodeName.StartsWith("Wall", StringComparison.OrdinalIgnoreCase) ||
        nodeName.StartsWith("Bridge", StringComparison.OrdinalIgnoreCase) ||
        nodeName.StartsWith("Trail", StringComparison.OrdinalIgnoreCase) ||
        nodeName.StartsWith("Cylinder", StringComparison.OrdinalIgnoreCase) ||
        nodeName.Contains("Platform", StringComparison.OrdinalIgnoreCase);

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
