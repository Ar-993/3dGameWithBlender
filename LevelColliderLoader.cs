using Assimp;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using NumericsMatrix = System.Numerics.Matrix4x4;

namespace _3DLight;

internal static class LevelColliderLoader
{
    public static List<Level.Platform> Load(string sourceModelPath)
    {
        if (!File.Exists(sourceModelPath))
            throw new FileNotFoundException($"Не найден исходный FBX уровня: {sourceModelPath}");

        using var importer = new AssimpContext();
        Scene scene = importer.ImportFile(sourceModelPath, PostProcessSteps.Triangulate);
        var platforms = new List<Level.Platform>();
        int nextId = 0;

        Visit(scene, scene.RootNode, Matrix.Identity, platforms, ref nextId);

        if (platforms.Count == 0)
            throw new InvalidOperationException("В модели уровня не найдены платформы с горизонтальной поверхностью.");

        return platforms;
    }

    private static void Visit(
        Scene scene,
        Node node,
        Matrix parentTransform,
        List<Level.Platform> platforms,
        ref int nextId)
    {
        Matrix transform = ToMatrix(node.Transform) * parentTransform;

        foreach (int meshIndex in node.MeshIndices)
        {
            if (!IsWalkable(node.Name))
                continue;

            Assimp.Mesh mesh = scene.Meshes[meshIndex];
            if (mesh.VertexCount == 0)
                continue;

            Vector3 min = new(float.MaxValue);
            Vector3 max = new(float.MinValue);

            foreach (System.Numerics.Vector3 vertex in mesh.Vertices)
            {
                Vector3 source = new(vertex.X, vertex.Y, vertex.Z);
                Vector3 point = Vector3.Transform(source, transform);
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }

            // Узкие декоративные меши не считаем поверхностями для ходьбы.
            if (max.X - min.X >= 0.5f && max.Z - min.Z >= 0.5f)
                platforms.Add(new Level.Platform(
                    nextId++,
                    node.Name,
                    new Microsoft.Xna.Framework.BoundingBox(min, max)));
        }

        foreach (Node child in node.Children)
            Visit(scene, child, transform, platforms, ref nextId);
    }

    private static bool IsWalkable(string name) =>
        name.StartsWith("Ground", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Bridge", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Trail", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Cylinder", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Platform", StringComparison.OrdinalIgnoreCase);

    private static Matrix ToMatrix(NumericsMatrix value) => Matrix.Transpose(new Matrix(
        value.M11, value.M12, value.M13, value.M14,
        value.M21, value.M22, value.M23, value.M24,
        value.M31, value.M32, value.M33, value.M34,
        value.M41, value.M42, value.M43, value.M44));
}
