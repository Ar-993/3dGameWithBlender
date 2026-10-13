using System.Numerics;

namespace Ars3D.Items;

// Position is the normalized model's bottom pivot, including when it lies sideways.
public readonly record struct ItemPose(Vector3 Position, Quaternion Orientation)
{
    public Matrix4x4 ToMatrix() =>
        Matrix4x4.CreateFromQuaternion(Orientation) * Matrix4x4.CreateTranslation(Position);
}
