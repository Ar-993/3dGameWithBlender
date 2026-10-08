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

    internal void Apply(EffectParameterCollection parameters)
    {
        parameters["FogEnabled"]?.SetValue(Enabled ? 1f : 0f);
        parameters["FogColor"]?.SetValue(Color.ToVector3());
        parameters["FogStart"]?.SetValue(EffectiveStartDistance);
        parameters["FogEnd"]?.SetValue(EffectiveEndDistance);
    }
}
