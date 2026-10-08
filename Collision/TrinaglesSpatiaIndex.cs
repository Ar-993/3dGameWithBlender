using Microsoft.Xna.Framework;

namespace _3DLight.Collision;

internal sealed class TriangleSpatialIndex
{
    private const int LeafSize = 8;

    private readonly Level.TriangleCollider[] triangles;
    private readonly int[] indices;
    private readonly Node[] nodes;

    public int SourceCount => triangles.Length;

    public Level.TriangleCollider this[int index] =>
        triangles[index];

    private readonly record struct Node(
        BoundingBox Bounds,
        int Start,
        int Count,
        int Left,
        int Right);

    public TriangleSpatialIndex(
        IReadOnlyList<Level.TriangleCollider> source)
    {
        triangles = source.ToArray();
        indices = Enumerable.Range(0, triangles.Length).ToArray();

        if (triangles.Length == 0)
        {
            nodes = [];
            return;
        }

        var builder = new List<Node>();
        Build(builder, 0, triangles.Length);
        nodes = builder.ToArray();
    }

    public void Query(
        BoundingBox bounds,
        List<int> destination)
    {
        destination.Clear();

        if (nodes.Length == 0)
            return;

        Visit(0, bounds, destination);

        // Сохраняем прежний порядок точных проверок.
        destination.Sort();
    }

    private int Build(
        List<Node> builder,
        int start,
        int count)
    {
        BoundingBox bounds = triangles[indices[start]].Bounds;

        for (int i = start + 1; i < start + count; i++)
        {
            BoundingBox other = triangles[indices[i]].Bounds;

            bounds.Min = Vector3.Min(bounds.Min, other.Min);
            bounds.Max = Vector3.Max(bounds.Max, other.Max);
        }

        int nodeIndex = builder.Count;
        builder.Add(default);

        if (count <= LeafSize)
        {
            builder[nodeIndex] = new Node(
                bounds,
                start,
                count,
                -1,
                -1);

            return nodeIndex;
        }

        Vector3 size = bounds.Max - bounds.Min;

        int axis = size.X >= size.Y && size.X >= size.Z
            ? 0
            : size.Y >= size.Z ? 1 : 2;

        Array.Sort(
            indices,
            start,
            count,
            Comparer<int>.Create((first, second) =>
            {
                float a = Center(first, axis);
                float b = Center(second, axis);

                int comparison = a.CompareTo(b);

                return comparison != 0
                    ? comparison
                    : first.CompareTo(second);
            }));

        int leftCount = count / 2;

        int left = Build(
            builder,
            start,
            leftCount);

        int right = Build(
            builder,
            start + leftCount,
            count - leftCount);

        builder[nodeIndex] = new Node(
            bounds,
            0,
            0,
            left,
            right);

        return nodeIndex;
    }

    private float Center(int triangleIndex, int axis)
    {
        BoundingBox bounds = triangles[triangleIndex].Bounds;
        Vector3 center = (bounds.Min + bounds.Max) * 0.5f;

        return axis switch
        {
            0 => center.X,
            1 => center.Y,
            _ => center.Z
        };
    }

    private void Visit(
        int nodeIndex,
        BoundingBox bounds,
        List<int> destination)
    {
        Node node = nodes[nodeIndex];

        if (!CapsuleCollision.Overlaps(bounds, node.Bounds))
            return;

        if (node.Count > 0)
        {
            for (int i = node.Start;
                 i < node.Start + node.Count;
                 i++)
            {
                int triangleIndex = indices[i];

                if (CapsuleCollision.Overlaps(
                        bounds,
                        triangles[triangleIndex].Bounds))
                {
                    destination.Add(triangleIndex);
                }
            }

            return;
        }

        Visit(node.Left, bounds, destination);
        Visit(node.Right, bounds, destination);
    }
}