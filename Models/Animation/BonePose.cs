using Microsoft.Xna.Framework;

namespace _3DLight;

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
