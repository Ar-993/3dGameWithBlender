using Ars3D.ModelFormat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsVector4 = System.Numerics.Vector4;

namespace Ars3D.Rendering;

public sealed class CompiledModel : IDisposable
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
        SceneLighting lighting,
        SceneRenderQueue? renderQueue = null)
    {
        Array.Copy(
            Hierarchy.BindPoseGlobalTransforms,
            globalTransforms,
            globalTransforms.Length);

        if (renderQueue is null)
            DrawMeshes(world, view, projection, lighting);
        else
            SubmitMeshes(renderQueue, world);
    }

    public void DrawPose(
        BonePose[] pose,
        Matrix world,
        Matrix view,
        Matrix projection,
        SceneLighting lighting,
        SceneRenderQueue? renderQueue = null)
    {
        ValidatePoseLength(pose);

        for (int nodeIndex = 0; nodeIndex < pose.Length; nodeIndex++)
            localTransforms[nodeIndex] = pose[nodeIndex].ToMatrix();

        Hierarchy.CalculateGlobalTransforms(localTransforms, globalTransforms);
        if (renderQueue is null)
            DrawMeshes(world, view, projection, lighting);
        else
            SubmitMeshes(renderQueue, world);
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

    // Для жёстких узлов (дверей, лифтов): сохраняет исходные матрицы без разложения в TRS.
    public void DrawTransforms(
        Matrix[] nodeLocalTransforms,
        Matrix world,
        Matrix view,
        Matrix projection,
        SceneLighting lighting,
        SceneRenderQueue? renderQueue = null)
    {
        ArgumentNullException.ThrowIfNull(nodeLocalTransforms);
        if (nodeLocalTransforms.Length != NodeCount)
            throw new ArgumentException("Для каждого узла нужна локальная матрица.", nameof(nodeLocalTransforms));

        Hierarchy.CalculateGlobalTransforms(nodeLocalTransforms, globalTransforms);
        if (renderQueue is null)
            DrawMeshes(world, view, projection, lighting);
        else
            SubmitMeshes(renderQueue, world);
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
            if (sourceMesh.Bones.Length == 0)
                meshEffect.CurrentTechnique = meshEffect.Techniques["StaticTechnique"]
                    ?? meshEffect.Techniques["ToonTechnique"];
            return new RuntimeMesh(
                sourceMesh,
                vertexBuffer,
                indexBuffer,
                meshEffect,
                CreateIdentityBoneTransforms(),
                texture,
                BoundingBox.CreateFromPoints(sourceMesh.Vertices.Select(v => ToXnaVector3(v.Position))));
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

    public void Submit(SceneRenderQueue renderQueue, Matrix world)
    {
        ArgumentNullException.ThrowIfNull(renderQueue);
        Array.Copy(Hierarchy.BindPoseGlobalTransforms, globalTransforms, globalTransforms.Length);
        SubmitMeshes(renderQueue, world);
    }

    public void SubmitPose(SceneRenderQueue renderQueue, BonePose[] pose, Matrix world)
    {
        ArgumentNullException.ThrowIfNull(renderQueue);
        ValidatePoseLength(pose);
        for (int nodeIndex = 0; nodeIndex < pose.Length; nodeIndex++)
            localTransforms[nodeIndex] = pose[nodeIndex].ToMatrix();
        Hierarchy.CalculateGlobalTransforms(localTransforms, globalTransforms);
        SubmitMeshes(renderQueue, world);
    }

    public void SubmitTransforms(SceneRenderQueue renderQueue, Matrix[] nodeLocalTransforms, Matrix world)
    {
        ArgumentNullException.ThrowIfNull(renderQueue);
        ArgumentNullException.ThrowIfNull(nodeLocalTransforms);
        if (nodeLocalTransforms.Length != NodeCount)
            throw new ArgumentException("Для каждого узла нужна локальная матрица.", nameof(nodeLocalTransforms));
        Hierarchy.CalculateGlobalTransforms(nodeLocalTransforms, globalTransforms);
        SubmitMeshes(renderQueue, world);
    }

    private void SubmitMeshes(SceneRenderQueue renderQueue, Matrix world)
    {
        foreach (RuntimeMesh mesh in runtimeMeshes)
        {
            Matrix meshWorld = PrepareMeshTransforms(mesh, world);
            bool skinned = mesh.Source.Bones.Length > 0;
            bool transparent = IsTransparent(mesh.Source);
            // Sorting must follow the animated glass rather than its bind pose.
            // Opaque meshes do not need CPU skinning; their positions stay on the GPU.
            BoundingBox bounds = transparent && skinned ? CalculateSkinnedBounds(mesh) : mesh.LocalBounds;
            renderQueue.Add(mesh.VertexBuffer, mesh.IndexBuffer, mesh.Effect, mesh.Texture,
                meshWorld, mesh.BoneTransforms, bounds, skinned,
                transparent, mesh.Source.MaterialOpacity,
                VegetationMaterials.UseAlphaCutOut(mesh.Source) ? 0.5f : 0f);
        }
    }

    private static BoundingBox CalculateSkinnedBounds(RuntimeMesh mesh)
    {
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        foreach (VertexData vertex in mesh.Source.Vertices)
        {
            Vector3 position = ToXnaVector3(vertex.Position);
            // Equivalent to mul(position, sum(Bones[index] * weight)) in ModelVertex.fxh.
            Vector3 skinnedPosition =
                Vector3.Transform(position, mesh.BoneTransforms[vertex.B0]) * vertex.Weights.X +
                Vector3.Transform(position, mesh.BoneTransforms[vertex.B1]) * vertex.Weights.Y +
                Vector3.Transform(position, mesh.BoneTransforms[vertex.B2]) * vertex.Weights.Z +
                Vector3.Transform(position, mesh.BoneTransforms[vertex.B3]) * vertex.Weights.W;
            minimum = Vector3.Min(minimum, skinnedPosition);
            maximum = Vector3.Max(maximum, skinnedPosition);
        }
        return new BoundingBox(minimum, maximum);
    }

    private void DrawMeshes(
    Matrix world,
    Matrix view,
    Matrix projection,
    SceneLighting lighting)
    {
        RasterizerState previousRasterizerState = graphicsDevice.RasterizerState;
        graphicsDevice.RasterizerState = RasterizerState.CullNone; 

        BlendState previousBlendState = graphicsDevice.BlendState;
        DepthStencilState previousDepthState = graphicsDevice.DepthStencilState;

        try
        {
            Vector3 cameraPosition = Matrix.Invert(view).Translation;

            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;

            foreach (RuntimeMesh runtimeMesh in runtimeMeshes)
            {
                if (!IsTransparent(runtimeMesh.Source))
                {
                    runtimeMesh.Effect.Parameters["Alpha"]?.SetValue(1.0f);
                    runtimeMesh.Effect.Parameters["UseTexture"]?.SetValue(1.0f);

                    DrawMesh(runtimeMesh, world, view, projection, cameraPosition, lighting);
                }
            }

            graphicsDevice.BlendState = BlendState.NonPremultiplied;
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            foreach (RuntimeMesh runtimeMesh in runtimeMeshes)
            {
                if (IsTransparent(runtimeMesh.Source))
                {
                    runtimeMesh.Effect.Parameters["Alpha"]?.SetValue(runtimeMesh.Source.MaterialOpacity);
                    runtimeMesh.Effect.Parameters["UseTexture"]?.SetValue(1.0f);

                    DrawMesh(runtimeMesh, world, view, projection, cameraPosition, lighting);
                }
            }
        }
        finally
        {
            graphicsDevice.RasterizerState = previousRasterizerState;
            graphicsDevice.BlendState = previousBlendState;
            graphicsDevice.DepthStencilState = previousDepthState;
        }
    }

    // Classify by the material, not the object's name: a window can also own an opaque frame.
    private static bool IsTransparent(MeshData mesh)
    {
        // Растительность с отсечением альфы рисуем
        // в обычном непрозрачном проходе.
        if (VegetationMaterials.UseAlphaCutOut(mesh))
        {
            return false;
        }

        // У материала задана неполная непрозрачность.
        if (mesh.MaterialOpacity < 1f)
        {
            return true;
        }

        // В имени материала есть слово glass.
        if (mesh.MaterialName.Contains(
            "glass",
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Запасная проверка для модели без имени материала.
        if (string.IsNullOrWhiteSpace(mesh.MaterialName))
        {
            if (mesh.Name.Contains(
                "glass",
                StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(mesh.TextureName))
                {
                    return true;
                }
            }
        }

        // Все остальные меши считаем непрозрачными.
        return false;
    }

    private void DrawMesh(
        RuntimeMesh runtimeMesh,
        Matrix modelWorld,
        Matrix view,
        Matrix projection,
        Vector3 cameraPosition,
        SceneLighting lighting)
    {
        Matrix meshWorld = PrepareMeshTransforms(runtimeMesh, modelWorld);
        // Animated bounds change with the pose; do not cull them using static vertex bounds.
        if (runtimeMesh.Source.Bones.Length == 0 &&
            lighting.Fog.IsFullyFogged(runtimeMesh.LocalBounds, meshWorld, cameraPosition))
            return;

        graphicsDevice.SetVertexBuffer(runtimeMesh.VertexBuffer);
        graphicsDevice.Indices = runtimeMesh.IndexBuffer;
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
        parameters["RimStrength"]?.SetValue(runtimeMesh.Source.Bones.Length > 0 ? 0.25f : 0f);
        parameters["AlphaCutoff"]?.SetValue(
            VegetationMaterials.UseAlphaCutOut(runtimeMesh.Source)
                ? 0.5f
                : 0f);
        parameters["Bones"]?.SetValue(runtimeMesh.BoneTransforms);
        lighting.Apply(parameters);
        lighting.Fog.Apply(parameters);
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
        Texture2D Texture,
        BoundingBox LocalBounds) : IDisposable
    {
        public void Dispose()
        {
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
            Effect.Dispose();
        }
    }

}
