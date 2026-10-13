using Ars3D.Items.Geometry;
using Microsoft.Xna.Framework;
using NumericsVector = System.Numerics.Vector3;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsMatrix = System.Numerics.Matrix4x4;

namespace _3DLight.Items;

// The only conversion boundary between MonoGame and the independent item library.
internal static class ItemMath
{
    public static NumericsVector ToLibrary(Vector3 value) => new(value.X, value.Y, value.Z);
    public static Vector3 ToGame(NumericsVector value) => new(value.X, value.Y, value.Z);
    public static NumericsQuaternion ToLibrary(Quaternion value) => new(value.X, value.Y, value.Z, value.W);
    public static Quaternion ToGame(NumericsQuaternion value) => new(value.X, value.Y, value.Z, value.W);
    public static BoundingBox ToGame(Aabb value) => new(ToGame(value.Min), ToGame(value.Max));
    public static Matrix ToGame(NumericsMatrix value) => new(
        value.M11, value.M12, value.M13, value.M14,
        value.M21, value.M22, value.M23, value.M24,
        value.M31, value.M32, value.M33, value.M34,
        value.M41, value.M42, value.M43, value.M44);
}
