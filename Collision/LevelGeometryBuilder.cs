using _3DLight.Assets;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _3DLight.Collision
{
    internal static class LevelGeometryBuilder
    {
        public static List<Level.Platform> BuildPlatforms(ModelData modelData, Matrix[] bindPoseGlobalTransforms)
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
                    CalculateMeshBounds(mesh, bindPoseGlobalTransforms)));
            }

            if (platforms.Count == 0)
                throw new InvalidOperationException(
                    "В скомпилированной модели уровня нет геометрии для коллизий.");

            return platforms;
        }

        private static BoundingBox CalculateMeshBounds(MeshData mesh, Matrix[] bindPoseGlobalTransforms)
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

        public static List<Level.TriangleCollider> BuildTriangleColliders(ModelData modelData, Matrix[] bindPoseGlobalTransforms)
        {
            const float minimumTriangleAreaSquared = 0.00000001f;
            var colliders = new List<Level.TriangleCollider>();

            for (int meshIndex = 0; meshIndex < modelData.Meshes.Count; meshIndex++)
            {
                MeshData mesh = modelData.Meshes[meshIndex];
                string nodeName = modelData.Nodes[mesh.Node].Name;
                if (mesh.Vertices.Length == 0 || mesh.Indices.Length < 3)
                    continue;

                Vector3[] vertices = GetWorldVertices(mesh, bindPoseGlobalTransforms);
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

        private static Vector3[] GetWorldVertices(MeshData mesh, Matrix[] bindPoseGlobalTransforms)
        {
            Matrix nodeTransform = bindPoseGlobalTransforms[mesh.Node];
            return mesh.Vertices
                .Select(vertex => Vector3.Transform(
                    ToXnaVector3(vertex.Position),
                    nodeTransform))
                .ToArray();
        }

        private static Vector3 ToXnaVector3(System.Numerics.Vector3 vector) =>
        new(vector.X, vector.Y, vector.Z);
    }
}
