using System.Numerics;

namespace Ars3D.Items;

// One camera frame supplies both the rendered held pose and the released body's pose.
internal static class HeldItemPlacement
{
    internal readonly record struct Frame(Vector3 Forward, Vector3 Right, Vector3 Up);

    public static Frame CreateFrame(Vector3 forward)
    {
        float lengthSquared = forward.LengthSquared();
        forward = Finite(forward) && float.IsFinite(lengthSquared) && lengthSquared > 0.000001f
            ? Vector3.Normalize(forward)
            : -Vector3.UnitZ;
        Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
        right = right.LengthSquared() > 0.000001f ? Vector3.Normalize(right) : Vector3.UnitX;
        return new(forward, right, Vector3.Cross(right, forward));
    }

    public static ItemPose CreatePose(
        ItemInstance item,
        Vector3 eyePosition,
        Frame frame)
    {
        Vector3 offset = item.Definition.Interaction.HeldOffset;
        Vector3 position = eyePosition + frame.Right * offset.X + frame.Up * offset.Y - frame.Forward * offset.Z;
        Matrix4x4 cameraRotation = new(
            frame.Right.X, frame.Right.Y, frame.Right.Z, 0f,
            frame.Up.X, frame.Up.Y, frame.Up.Z, 0f,
            -frame.Forward.X, -frame.Forward.Y, -frame.Forward.Z, 0f,
            0f, 0f, 0f, 1f);
        Quaternion orientation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateRotationY(item.Definition.HeldYaw) * cameraRotation));
        return new(position, orientation);
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
