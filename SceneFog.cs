using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight;

public sealed class SceneFog
{
    public bool Enabled { get; set; }
    public bool CullFullyFoggedMeshes { get; set; }
    public Color Color { get; set; } = new(148, 155, 151);
    public float StartDistance { get; set; } = 7f;
    public float EndDistance { get; set; } = 28f;

    internal float EffectiveStartDistance => float.IsFinite(StartDistance)
        ? MathF.Max(0f, StartDistance)
        : 0f;

    internal float EffectiveEndDistance => float.IsFinite(EndDistance)
        ? MathF.Max(EffectiveStartDistance + 0.001f, EndDistance)
        : EffectiveStartDistance + 1f;

    // A transformed AABB conservatively encloses even rotated and nonuniformly scaled meshes.
    internal bool IsFullyFogged(BoundingBox localBounds, Matrix world, Vector3 camera)
    {
        if (!Enabled || !CullFullyFoggedMeshes)
            return false;
        Vector3 center = Vector3.Transform((localBounds.Min + localBounds.Max) * 0.5f, world);
        Vector3 half = (localBounds.Max - localBounds.Min) * 0.5f;
        Vector3 extent = new(
            MathF.Abs(world.M11) * half.X + MathF.Abs(world.M21) * half.Y + MathF.Abs(world.M31) * half.Z,
            MathF.Abs(world.M12) * half.X + MathF.Abs(world.M22) * half.Y + MathF.Abs(world.M32) * half.Z,
            MathF.Abs(world.M13) * half.X + MathF.Abs(world.M23) * half.Y + MathF.Abs(world.M33) * half.Z);
        Vector3 nearest = Vector3.Clamp(camera, center - extent, center + extent);
        float end = EffectiveEndDistance;
        return Vector3.DistanceSquared(camera, nearest) >= end * end;
    }

    internal void Apply(EffectParameterCollection parameters)
    {
        parameters["FogEnabled"]?.SetValue(Enabled ? 1f : 0f);
        parameters["FogColor"]?.SetValue(Color.ToVector3());
        parameters["FogStart"]?.SetValue(EffectiveStartDistance);
        parameters["FogEnd"]?.SetValue(EffectiveEndDistance);
    }
}
