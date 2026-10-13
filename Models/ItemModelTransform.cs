using _3DLight.Assets;
using Microsoft.Xna.Framework;

namespace _3DLight.Models;

internal static class ItemModelTransform
{
    public static Matrix FitToHeight(
        ModelData data,
        ModelHierarchy hierarchy,
        float height)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(hierarchy);
        if (!float.IsFinite(height) || height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(height), "Высота предмета должна быть положительной.");

        BoundingBox bounds = GetBindPoseBounds(data, hierarchy);
        float sourceHeight = bounds.Max.Y - bounds.Min.Y;
        if (sourceHeight <= 0f)
            throw new InvalidDataException("Модель предмета должна содержать геометрию с ненулевой высотой.");

        var bottomCenter = new Vector3(
            (bounds.Min.X + bounds.Max.X) * 0.5f,
            bounds.Min.Y,
            (bounds.Min.Z + bounds.Max.Z) * 0.5f);
        return Matrix.CreateTranslation(-bottomCenter) * Matrix.CreateScale(height / sourceHeight);
    }

    public static BoundingBox GetBindPoseBounds(ModelData data, ModelHierarchy hierarchy)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(hierarchy);

        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);

        foreach (MeshData mesh in data.Meshes)
        {
            Matrix[] skinTransforms = mesh.Bones.Select(bone =>
                ToXnaMatrix(bone.Offset) *
                hierarchy.BindPoseGlobalTransforms[bone.Node] *
                hierarchy.InverseRootTransform).ToArray();

            foreach (VertexData vertex in mesh.Vertices)
            {
                Vector3 source = new(vertex.Position.X, vertex.Position.Y, vertex.Position.Z);
                // Keep the bounds in the same space as CompiledModel.Draw's model world.
                Vector3 position = mesh.Bones.Length == 0
                    ? Vector3.Transform(source, hierarchy.BindPoseGlobalTransforms[mesh.Node])
                    : SkinPosition(source, vertex, skinTransforms);

                if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
                    throw new InvalidDataException("Модель предмета содержит некорректные координаты.");

                minimum = Vector3.Min(minimum, position);
                maximum = Vector3.Max(maximum, position);
            }
        }

        if (!float.IsFinite(minimum.Y) || !float.IsFinite(maximum.Y))
            throw new InvalidDataException("Модель предмета должна содержать геометрию с ненулевой высотой.");
        return new BoundingBox(minimum, maximum);
    }

    private static Vector3 SkinPosition(
        Vector3 position,
        VertexData vertex,
        Matrix[] skinTransforms) =>
        TransformInfluence(position, vertex.B0, vertex.Weights.X, skinTransforms) +
        TransformInfluence(position, vertex.B1, vertex.Weights.Y, skinTransforms) +
        TransformInfluence(position, vertex.B2, vertex.Weights.Z, skinTransforms) +
        TransformInfluence(position, vertex.B3, vertex.Weights.W, skinTransforms);

    private static Vector3 TransformInfluence(
        Vector3 position,
        byte boneIndex,
        float weight,
        Matrix[] skinTransforms) =>
        weight == 0f ? Vector3.Zero : Vector3.Transform(position, skinTransforms[boneIndex]) * weight;

    private static Matrix ToXnaMatrix(System.Numerics.Matrix4x4 matrix) => new(
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44);
}
