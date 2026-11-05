using Ars3D.ModelFormat;
using Ars3D.Rendering;
using Microsoft.Xna.Framework;

namespace Levels.Doors;

internal sealed class DoorDiscovery(ModelData data, ModelHierarchy hierarchy)
{
    private const string FbxHelperTag = "_$AssimpFbx$_";

    public IEnumerable<int> FindNamedNodes()
    {
        for (int node = 0; node < data.Nodes.Count; node++)
        {
            string name = data.Nodes[node].Name;
            if (name.Contains(FbxHelperTag, StringComparison.Ordinal))
                continue;

            if (name.Contains("door", StringComparison.OrdinalIgnoreCase))
                yield return node;
        }
    }

    public HashSet<int> GetDoorMeshes(int root)
    {
        var belongsToDoor = new bool[data.Nodes.Count];
        belongsToDoor[root] = true;
        for (int node = root + 1; node < data.Nodes.Count; node++)
        {
            int parent = data.Nodes[node].Parent;
            belongsToDoor[node] = parent >= 0 && belongsToDoor[parent];
        }

        return Enumerable.Range(0, data.Meshes.Count)
            .Where(index => belongsToDoor[data.Meshes[index].Node]).ToHashSet();
    }

    public Vector3 FindModelPivot(int node)
    {
        string pivotName = data.Nodes[node].Name + FbxHelperTag + "RotationPivot";
        for (int parent = data.Nodes[node].Parent; parent >= 0; parent = data.Nodes[parent].Parent)
        {
            if (data.Nodes[parent].Name.Equals(pivotName, StringComparison.Ordinal))
            {
                return Vector3.Transform(hierarchy.BindPoseGlobalTransforms[parent].Translation,
                    Matrix.Invert(hierarchy.BindPoseGlobalTransforms[node]));
            }
        }

        // Например, в Blender петля задаётся переносом origin самого объекта.
        return Vector3.Zero;
    }

    public Vector3 FindLeftHinge(int doorNode, IReadOnlySet<int> meshIds)
    {
        Matrix worldToDoor = Matrix.Invert(hierarchy.BindPoseGlobalTransforms[doorNode]);
        var points = new List<Vector3>();
        foreach (int meshId in meshIds)
        {
            MeshData mesh = data.Meshes[meshId];
            Matrix meshToDoor = hierarchy.BindPoseGlobalTransforms[mesh.Node] * worldToDoor;
            foreach (VertexData vertex in mesh.Vertices)
                points.Add(Vector3.Transform(new(vertex.Position.X, vertex.Position.Y, vertex.Position.Z), meshToDoor));
        }

        float left = points.Min(point => point.X);
        // Толщину берём у левого ребра: выступающая ручка не должна смещать ось петель.
        Vector3[] edge = points.Where(point => point.X <= left + 0.001f).ToArray();
        float middleZ = (edge.Min(point => point.Z) + edge.Max(point => point.Z)) * 0.5f;
        return new Vector3(left, 0f, middleZ);
    }

}