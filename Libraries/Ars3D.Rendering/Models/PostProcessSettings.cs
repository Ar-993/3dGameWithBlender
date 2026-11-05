using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ars3D.Rendering;

/// <summary>Neutral by default; applied once to the completed scene, before the HUD.</summary>
public sealed class PostProcessSettings
{
    public float Exposure { get; set; } = 1f;
    public float Saturation { get; set; } = 1f;
    public float VignetteStrength { get; set; }

    internal void Apply(Effect effect, float aspectRatio)
    {
        effect.Parameters["Exposure"].SetValue(
            float.IsFinite(Exposure) ? MathF.Max(0f, Exposure) : 1f);
        effect.Parameters["Saturation"].SetValue(
            float.IsFinite(Saturation) ? MathF.Max(0f, Saturation) : 1f);
        effect.Parameters["VignetteStrength"].SetValue(
            float.IsFinite(VignetteStrength) ? MathHelper.Clamp(VignetteStrength, 0f, 1f) : 0f);
        effect.Parameters["VignetteAspectRatio"].SetValue(aspectRatio);
    }
}