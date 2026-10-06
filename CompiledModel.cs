using _3DLight.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsVector4 = System.Numerics.Vector4;
using _3DLight.Models;

namespace _3DLight;

internal sealed class CompiledModel : IDisposable
{
    private const int MaximumBoneCount = 72;

    private readonly GraphicsDevice graphicsDevice;
    private readonly RuntimeMesh[] runtimeMeshes;
    private readonly Texture2D[] ownedMaterialTextures;

    private readonly Matrix[] localTransforms;
    private readonly Matrix[] globalTransforms;

    public ModelData Data { get; }
    public ModelHierarchy Hierarchy { get; }
    public int NodeCount => Hierarchy.NodeCount;

    // Loaded texture ownership transfers only when construction succeeds.
    internal CompiledModel(
        GraphicsDevice graphicsDevice,
        ModelData modelData,
        Texture2D[] meshTextures,
        Texture2D[] ownedMaterialTextures,
        Effect toonEffect)
    {
        if (meshTextures.Length != modelData.Meshes.Count)
            throw new ArgumentException("Для каждого меша должна быть задана текстура.", nameof(meshTextures));

        this.graphicsDevice = graphicsDevice;
        this.ownedMaterialTextures = ownedMaterialTextures;
        Data = modelData;
        Hierarchy = new ModelHierarchy(modelData);
        localTransforms = (Matrix[])Hierarchy.LocalTransforms.Clone();
        globalTransforms = new Matrix[Hierarchy.NodeCount];

        var createdMeshes = new List<RuntimeMesh>(modelData.Meshes.Count);
        try
        {
            for (int meshIndex = 0; meshIndex < modelData.Meshes.Count; meshIndex++)
            {
                createdMeshes.Add(CreateRuntimeMesh(
                    modelData.Meshes[meshIndex],
                    meshTextures[meshIndex],
                    toonEffect));
            }

            runtimeMeshes = createdMeshes.ToArray();
        }
        catch
        {
            foreach (RuntimeMesh mesh in createdMeshes)
                mesh.Dispose();
            throw;
        }
    }

    public void Draw(
        Matrix world,
        Matrix view,
        Matrix projection,
        SceneLighting lighting)
    {
        Array.Copy(
            Hierarchy.BindPoseGlobalTransforms,
            globalTransforms,
            globalTransforms.Length);

        DrawMeshes(world, view, projection, lighting);
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

        Hierarchy.CalculateGlobalTransforms(localTransforms, globalTransforms);
        DrawMeshes(world, view, projection, lighting);
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

    private RuntimeMesh CreateRuntimeMesh(
        MeshData sourceMesh,
        Texture2D texture,
        Effect toonEffect)
    {
        RuntimeVertex[] vertices = ConvertVertices(sourceMesh.Vertices);
        VertexBuffer? vertexBuffer = null;
        IndexBuffer? indexBuffer = null;
        Effect? meshEffect = null;

        try
        {
            vertexBuffer = new VertexBuffer(
                graphicsDevice,
                RuntimeVertex.Declaration,
                vertices.Length,
                BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);

            indexBuffer = new IndexBuffer(
                graphicsDevice,
                IndexElementSize.ThirtyTwoBits,
                sourceMesh.Indices.Length,
                BufferUsage.WriteOnly);
            indexBuffer.SetData(sourceMesh.Indices);

            meshEffect = toonEffect.Clone();
            return new RuntimeMesh(
                sourceMesh,
                vertexBuffer,
                indexBuffer,
                meshEffect,
                CreateIdentityBoneTransforms(),
                texture);
        }
        catch
        {
            meshEffect?.Dispose();
            indexBuffer?.Dispose();
            vertexBuffer?.Dispose();
            throw;
        }
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
                Hierarchy.InverseRootTransform;
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

    private static Vector3 ToXnaVector3(System.Numerics.Vector3 vector) =>
        new(vector.X, vector.Y, vector.Z);

    public void Dispose()
    {
        foreach (RuntimeMesh runtimeMesh in runtimeMeshes)
            runtimeMesh.Dispose();

        foreach (Texture2D texture in ownedMaterialTextures)
            texture.Dispose();
    }

    private static Matrix ToXnaMatrix(NumericsMatrix matrix) => new(
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44);

    private static Vector4 ToXnaVector4(NumericsVector4 vector) =>
        new(vector.X, vector.Y, vector.Z, vector.W);

    private sealed record RuntimeMesh(
        MeshData Source,
        VertexBuffer VertexBuffer,
        IndexBuffer IndexBuffer,
        Effect Effect,
        Matrix[] BoneTransforms,
        Texture2D Texture) : IDisposable
    {
        public void Dispose()
        {
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
            Effect.Dispose();
        }
    }

}
