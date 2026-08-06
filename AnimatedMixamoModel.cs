using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assimp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector3 = System.Numerics.Vector3;

internal sealed class AnimatedMixamoModel
{
    private const int MaxBones = 72;
    private readonly GraphicsDevice graphicsDevice;
    private readonly List<AnimatedMesh> meshes = [];
    private readonly Dictionary<string, Clip> clips = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Assimp.Node> nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Matrix> bindPose = new(StringComparer.Ordinal);
    private Assimp.Node root = null!;
    private Matrix inverseRoot;
    private bool transposeMatrices;
    private readonly Texture2D dummyTexture;

    public AnimatedMixamoModel(GraphicsDevice graphicsDevice)
    {
        this.graphicsDevice = graphicsDevice;

        // Создаем 1x1 белую текстуру-заглушку
        dummyTexture = new Texture2D(graphicsDevice, 1, 1);
        dummyTexture.SetData(new[] { Color.White });
    }

    public void Load(Dictionary<string, string> animationFiles, Texture2D? texture = null)
    {
        if (animationFiles.Count == 0)
            throw new ArgumentException("Нужно передать хотя бы один файл анимации.");

        // Если текстуру передали из MGCB — берем её, иначе берем белую заглушку
        Texture2D modelTexture = texture ?? dummyTexture;

        using var importer = new AssimpContext();
        var flags = PostProcessSteps.Triangulate | PostProcessSteps.JoinIdenticalVertices |
                    PostProcessSteps.GenerateSmoothNormals | PostProcessSteps.FlipUVs |
                    PostProcessSteps.LimitBoneWeights | PostProcessSteps.FlipWindingOrder;

        bool isFirstMesh = true;

        foreach (var (clipName, filePath) in animationFiles)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Файл анимации не найден: {filePath}");
            Scene scene = importer.ImportFile(filePath, flags);

            if (isFirstMesh)
            {
                transposeMatrices = ChooseMatrixLayout(scene);
                root = scene.RootNode;
                inverseRoot = Matrix.Invert(ToMatrix(root.Transform));
                IndexNodes(root);

                // Передаем модельную текстуру в BuildMeshes
                BuildMeshes(scene, modelTexture);
                isFirstMesh = false;
            }

            clips[clipName] = ReadClip(scene);
        }
    }

    private static bool ChooseMatrixLayout(Scene scene)
    {
        float directError = CalculateBindPoseError(scene, transpose: false);
        float transposedError = CalculateBindPoseError(scene, transpose: true);
        return transposedError < directError;
    }

    private static float CalculateBindPoseError(Scene scene, bool transpose)
    {
        var globals = new Dictionary<string, Matrix>(StringComparer.Ordinal);

        void Visit(Assimp.Node node, Matrix parent)
        {
            Matrix global = ConvertMatrix(node.Transform, transpose) * parent;
            globals[node.Name] = global;
            foreach (var child in node.Children) Visit(child, global);
        }

        Visit(scene.RootNode, Matrix.Identity);
        Matrix inverseSceneRoot = Matrix.Invert(ConvertMatrix(scene.RootNode.Transform, transpose));
        float error = 0f;
        int count = 0;

        foreach (var mesh in scene.Meshes)
            foreach (var bone in mesh.Bones)
            {
                if (!globals.TryGetValue(bone.Name, out Matrix global)) continue;
                Matrix skin = ConvertMatrix(bone.OffsetMatrix, transpose) * global * inverseSceneRoot;
                error += MatrixIdentityError(skin);
                count++;
            }

        return count == 0 ? float.MaxValue : error / count;
    }

    private static float MatrixIdentityError(Matrix m) =>
        MathF.Abs(m.M11 - 1) + MathF.Abs(m.M22 - 1) + MathF.Abs(m.M33 - 1) + MathF.Abs(m.M44 - 1) +
        MathF.Abs(m.M12) + MathF.Abs(m.M13) + MathF.Abs(m.M14) + MathF.Abs(m.M21) +
        MathF.Abs(m.M23) + MathF.Abs(m.M24) + MathF.Abs(m.M31) + MathF.Abs(m.M32) +
        MathF.Abs(m.M34) + MathF.Abs(m.M41) + MathF.Abs(m.M42) + MathF.Abs(m.M43);

    private void IndexNodes(Assimp.Node node)
    {
        nodes[node.Name] = node;
        bindPose[node.Name] = ToMatrix(node.Transform);
        foreach (var child in node.Children)
            IndexNodes(child);
    }

    private void BuildMeshes(Scene scene, Texture2D modelTexture)
    {
        foreach (var source in scene.Meshes)
        {
            if (!source.HasBones) continue;
            if (source.BoneCount > MaxBones)
                throw new InvalidOperationException($"Сетка содержит {source.BoneCount} костей; максимум — {MaxBones}.");

            var weights = new List<(int Bone, float Weight)>[source.VertexCount];
            for (int i = 0; i < weights.Length; i++) weights[i] = [];

            var offsets = new Matrix[source.BoneCount];
            for (int boneIndex = 0; boneIndex < source.BoneCount; boneIndex++)
            {
                var bone = source.Bones[boneIndex];
                offsets[boneIndex] = ToMatrix(bone.OffsetMatrix);
                foreach (var weight in bone.VertexWeights)
                    weights[weight.VertexID].Add((boneIndex, weight.Weight));
            }

            var vertices = new SkinnedVertex[source.VertexCount];
            for (int i = 0; i < vertices.Length; i++)
            {
                weights[i].Sort((a, b) => b.Weight.CompareTo(a.Weight));
                float total = 0f;
                for (int j = 0; j < Math.Min(4, weights[i].Count); j++) total += weights[i][j].Weight;
                if (total <= 0f) total = 1f;

                byte i0 = weights[i].Count > 0 ? (byte)weights[i][0].Bone : (byte)0;
                byte i1 = weights[i].Count > 1 ? (byte)weights[i][1].Bone : (byte)0;
                byte i2 = weights[i].Count > 2 ? (byte)weights[i][2].Bone : (byte)0;
                byte i3 = weights[i].Count > 3 ? (byte)weights[i][3].Bone : (byte)0;
                var indices = new Byte4(i0, i1, i2, i3);

                var blend = new Vector4(
                    weights[i].Count > 0 ? weights[i][0].Weight / total : 1f,
                    weights[i].Count > 1 ? weights[i][1].Weight / total : 0f,
                    weights[i].Count > 2 ? weights[i][2].Weight / total : 0f,
                    weights[i].Count > 3 ? weights[i][3].Weight / total : 0f);

                var p = source.Vertices[i];
                var n = source.HasNormals ? source.Normals[i] : new NumericsVector3(0, 1, 0);
                var uv = source.HasTextureCoords(0) ? source.TextureCoordinateChannels[0][i] : new NumericsVector3();
                vertices[i] = new SkinnedVertex(
                    new Vector3(p.X, p.Y, p.Z), new Vector3(n.X, n.Y, n.Z),
                    new Vector2(uv.X, uv.Y), indices, blend);
            }

            int[] indicesArray = source.GetIndices().ToArray();
            var vertexBuffer = new VertexBuffer(graphicsDevice, SkinnedVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);
            var indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indicesArray.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indicesArray);

            var effect = new SkinnedEffect(graphicsDevice)
            {
                Texture = modelTexture, // Наша MGCB текстура
                DiffuseColor = Vector3.One,
                AmbientLightColor = new Vector3(0.4f),
                SpecularColor = Vector3.Zero,
                Alpha = 1f,
                PreferPerPixelLighting = true
            };
            effect.EnableDefaultLighting();
            effect.AmbientLightColor = new Vector3(0.4f);
            effect.DirectionalLight0.Enabled = true;
            effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.5f, -1f, -0.4f));
            effect.DirectionalLight0.DiffuseColor = Vector3.One;

            meshes.Add(new AnimatedMesh(vertexBuffer, indexBuffer, indicesArray.Length / 3, source.Bones, offsets, effect));
        }

        if (meshes.Count == 0)
            throw new InvalidOperationException("Файл FBX не содержит мешей со скелетом.");
    }

    private static Clip ReadClip(Scene scene)
    {
        if (scene.AnimationCount == 0)
            throw new InvalidOperationException("Файл FBX не содержит анимации.");

        var animation = scene.Animations[0];
        double ticksPerSecond = animation.TicksPerSecond > 0 ? animation.TicksPerSecond : 25.0;
        var channels = new Dictionary<string, NodeAnimationChannel>(StringComparer.Ordinal);
        foreach (var channel in animation.NodeAnimationChannels)
            channels[channel.NodeName] = channel;
        return new Clip(animation.DurationInTicks, ticksPerSecond, channels);
    }

    public void Draw(string clipName, float seconds, bool loop, Matrix world, Matrix view, Matrix projection)
    {
        var oldRasterizerState = graphicsDevice.RasterizerState;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;

        var globals = new Dictionary<string, Matrix>(nodes.Count, StringComparer.Ordinal);
        EvaluateNode(root, Matrix.Identity, clips[clipName], seconds, loop, globals);

        foreach (var mesh in meshes)
        {
            var skin = new Matrix[mesh.Bones.Count];
            for (int i = 0; i < mesh.Bones.Count; i++)
            {
                string boneName = mesh.Bones[i].Name;
                skin[i] = mesh.Offsets[i] * globals[boneName] * inverseRoot;
            }

            mesh.Effect.SetBoneTransforms(skin);
            mesh.Effect.World = world;
            mesh.Effect.View = view;
            mesh.Effect.Projection = projection;
            graphicsDevice.SetVertexBuffer(mesh.VertexBuffer);
            graphicsDevice.Indices = mesh.IndexBuffer;
            foreach (var pass in mesh.Effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawIndexedPrimitives(Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount);
            }
        }

        graphicsDevice.RasterizerState = oldRasterizerState;
    }

    private void EvaluateNode(Assimp.Node node, Matrix parent, Clip clip, float seconds, bool loop, Dictionary<string, Matrix> globals)
    {
        Matrix local = bindPose[node.Name];
        if (clip.Channels.TryGetValue(node.Name, out var channel))
        {
            double tick = seconds * clip.TicksPerSecond;
            tick = loop ? tick % clip.Duration : Math.Min(tick, clip.Duration);
            Vector3 position = InterpolatePosition(channel, tick);
            Quaternion rotation = InterpolateRotation(channel, tick);
            Vector3 scale = InterpolateScale(channel, tick);
            local = Matrix.CreateScale(scale) * Matrix.CreateFromQuaternion(rotation) * Matrix.CreateTranslation(position);
        }

        Matrix global = local * parent;
        globals[node.Name] = global;
        foreach (var child in node.Children)
            EvaluateNode(child, global, clip, seconds, loop, globals);
    }

    private static Vector3 InterpolatePosition(NodeAnimationChannel channel, double time)
    {
        if (channel.PositionKeyCount == 0) return Vector3.Zero;
        int i = FindKey(channel.PositionKeys, time, k => k.Time);
        if (i == channel.PositionKeyCount - 1) return ToVector(channel.PositionKeys[i].Value);
        var a = channel.PositionKeys[i]; var b = channel.PositionKeys[i + 1];
        float t = (float)((time - a.Time) / Math.Max(b.Time - a.Time, double.Epsilon));
        return Vector3.Lerp(ToVector(a.Value), ToVector(b.Value), t);
    }

    private static Quaternion InterpolateRotation(NodeAnimationChannel channel, double time)
    {
        if (channel.RotationKeyCount == 0) return Quaternion.Identity;
        int i = FindKey(channel.RotationKeys, time, k => k.Time);
        if (i == channel.RotationKeyCount - 1) return ToQuaternion(channel.RotationKeys[i].Value);
        var a = channel.RotationKeys[i]; var b = channel.RotationKeys[i + 1];
        float t = (float)((time - a.Time) / Math.Max(b.Time - a.Time, double.Epsilon));
        return Quaternion.Normalize(Quaternion.Slerp(ToQuaternion(a.Value), ToQuaternion(b.Value), t));
    }

    private static Vector3 InterpolateScale(NodeAnimationChannel channel, double time)
    {
        if (channel.ScalingKeyCount == 0) return Vector3.One;
        int i = FindKey(channel.ScalingKeys, time, k => k.Time);
        if (i == channel.ScalingKeyCount - 1) return ToVector(channel.ScalingKeys[i].Value);
        var a = channel.ScalingKeys[i]; var b = channel.ScalingKeys[i + 1];
        float t = (float)((time - a.Time) / Math.Max(b.Time - a.Time, double.Epsilon));
        return Vector3.Lerp(ToVector(a.Value), ToVector(b.Value), t);
    }

    private static int FindKey<T>(IList<T> keys, double time, Func<T, double> getTime)
    {
        for (int i = 0; i < keys.Count - 1; i++)
            if (time < getTime(keys[i + 1])) return i;
        return keys.Count - 1;
    }

    private Matrix ToMatrix(NumericsMatrix m) => ConvertMatrix(m, transposeMatrices);
    private static Matrix ConvertMatrix(NumericsMatrix m, bool transpose) =>
        transpose ? Matrix.Transpose(new Matrix(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44))
                  : new Matrix(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);

    private static Vector3 ToVector(NumericsVector3 v) => new(v.X, v.Y, v.Z);
    private static Quaternion ToQuaternion(NumericsQuaternion q) => new(q.X, q.Y, q.Z, q.W);

    private sealed record Clip(double Duration, double TicksPerSecond, Dictionary<string, NodeAnimationChannel> Channels);
    private sealed record AnimatedMesh(VertexBuffer VertexBuffer, IndexBuffer IndexBuffer, int PrimitiveCount, IList<Bone> Bones, Matrix[] Offsets, SkinnedEffect Effect);
}