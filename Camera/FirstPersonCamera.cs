using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Camera;

internal sealed class FirstPersonCamera
{
    private const float MouseSensitivity = 0.003f;
    private static readonly float MaximumPitch = MathHelper.ToRadians(89f);

    public float EyeHeight { get; set; } = 1.62f;
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public Vector3 Position { get; private set; }
    public Vector3 Forward { get; private set; } = Vector3.Forward;
    public Matrix View { get; private set; } = Matrix.Identity;
    public Matrix Projection { get; private set; } = Matrix.Identity;

    public void UpdateMouse(Point windowCenter, MouseState mouse) =>
        ApplyMouseDelta(mouse.X - windowCenter.X, mouse.Y - windowCenter.Y);

    internal void ApplyMouseDelta(float deltaX, float deltaY)
    {
        Yaw = MathHelper.WrapAngle(Yaw - deltaX * MouseSensitivity);
        Pitch = MathHelper.Clamp(Pitch - deltaY * MouseSensitivity,
            -MaximumPitch, MaximumPitch);
    }

    public void UpdateMatrices(Vector3 playerFeet, float aspectRatio)
    {
        // The eye stays inside the player's collision capsule, including near walls.
        Position = playerFeet + Vector3.Up * EyeHeight;
        float cosPitch = MathF.Cos(Pitch);
        Forward = new Vector3(-MathF.Sin(Yaw) * cosPitch,
            MathF.Sin(Pitch), -MathF.Cos(Yaw) * cosPitch);
        View = Matrix.CreateLookAt(Position, Position + Forward, Vector3.Up);
        Projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(70f), aspectRatio, 0.04f, 1000f);
    }
}
