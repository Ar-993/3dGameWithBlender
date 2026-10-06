using _3DLight.Assets;
using Microsoft.Xna.Framework;

namespace _3DLight;

internal sealed class ModelHierarchy
{
    private readonly ModelData modelData;

    public Matrix[] LocalTransforms { get; }
    public Matrix[] BindPoseGlobalTransforms { get; }
    public Matrix InverseRootTransform { get; }
    public int NodeCount => modelData.Nodes.Count;

    public ModelHierarchy(ModelData modelData)
    {
        this.modelData = modelData;
        LocalTransforms = modelData.Nodes
            .Select(node => ToXnaMatrix(node.Bind))
            .ToArray();
        BindPoseGlobalTransforms = new Matrix[NodeCount];
        CalculateGlobalTransforms(LocalTransforms, BindPoseGlobalTransforms);
        InverseRootTransform = NodeCount == 0
            ? Matrix.Identity
            : Matrix.Invert(BindPoseGlobalTransforms[0]);
    }

    public void CalculateGlobalTransforms(
        Matrix[] sourceLocalTransforms,
        Matrix[] destinationGlobalTransforms)
    {
        // The model compiler writes every parent before its children.
        for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
        {
            int parentIndex = modelData.Nodes[nodeIndex].Parent;
            Matrix localTransform = sourceLocalTransforms[nodeIndex];
            destinationGlobalTransforms[nodeIndex] = parentIndex < 0
                ? localTransform
                : localTransform * destinationGlobalTransforms[parentIndex];
        }
    }

    public bool TryGetNodePosition(string nodeName, out Vector3 position)
    {
        for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
        {
            if (!string.Equals(
                    modelData.Nodes[nodeIndex].Name,
                    nodeName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            position = BindPoseGlobalTransforms[nodeIndex].Translation;
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
        var mask = new bool[NodeCount];
        mask[rootNodeIndex] = true;
        for (int nodeIndex = rootNodeIndex + 1; nodeIndex < NodeCount; nodeIndex++)
        {
            int parentIndex = modelData.Nodes[nodeIndex].Parent;
            mask[nodeIndex] = parentIndex >= 0 && mask[parentIndex];
        }
        return mask;
    }

    private static Matrix ToXnaMatrix(System.Numerics.Matrix4x4 matrix) => new(
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44);
}
