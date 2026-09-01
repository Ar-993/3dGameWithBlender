using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight;

/// <summary>
/// Единые настройки света для всей сцены.
/// </summary>
public sealed class SceneLighting
{
    private Vector3 sunDirection = Vector3.Normalize(
        new Vector3(-0.55f, -1f, -0.35f));

    public bool Enabled { get; set; } = true;

    public Vector3 AmbientColor { get; set; } = new(0.28f, 0.32f, 0.42f);

    public Vector3 SunDirection
    {
        get => sunDirection;
        set => sunDirection = value.LengthSquared() > 0.0001f
            ? Vector3.Normalize(value)
            : sunDirection;
    }

    public Vector3 SunColor { get; set; } = new(1f, 0.92f, 0.76f);
    public float SunIntensity { get; set; } = 1.05f;

    public Vector3 PointPosition { get; set; }
    public Vector3 PointColor { get; set; } = new(1f, 0.52f, 0.20f);
    public float PointIntensity { get; set; } = 1.45f;
    public float PointRange { get; set; } = 5.5f;

    /// <summary>
    /// Держит тёплый точечный свет немного выше центра игрока.
    /// </summary>
    public void FollowPlayer(Vector3 playerFeetPosition)
    {
        PointPosition = playerFeetPosition + new Vector3(0f, 1.25f, 0f);
    }

    internal void Apply(EffectParameterCollection parameters)
    {
        parameters["LightingEnabled"]?.SetValue(Enabled ? 1f : 0f);
        parameters["AmbientColor"]?.SetValue(AmbientColor);
        parameters["SunDirection"]?.SetValue(sunDirection);
        parameters["SunColor"]?.SetValue(SunColor);
        parameters["SunIntensity"]?.SetValue(MathF.Max(0f, SunIntensity));
        parameters["PointLightPosition"]?.SetValue(PointPosition);
        parameters["PointLightColor"]?.SetValue(PointColor);
        parameters["PointLightIntensity"]?.SetValue(MathF.Max(0f, PointIntensity));
        parameters["PointLightRange"]?.SetValue(MathF.Max(0.001f, PointRange));
    }
}
