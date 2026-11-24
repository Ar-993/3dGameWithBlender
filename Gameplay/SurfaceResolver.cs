using System;

namespace _3DLight.Gameplay;

internal enum SurfaceType
{
    Default,
    Asphalt,
    Carpet,
    Wood,
    Tile,
    Grass
}

internal static class SurfaceResolver
{
    public static SurfaceType Resolve(string? platformName)
    {
        if (string.IsNullOrWhiteSpace(platformName))
            return SurfaceType.Default;

        if (platformName.StartsWith("Asphalt", StringComparison.OrdinalIgnoreCase))
            return SurfaceType.Asphalt;

        if (platformName.StartsWith("Carpet", StringComparison.OrdinalIgnoreCase))
            return SurfaceType.Carpet;

        if (platformName.StartsWith("Wood", StringComparison.OrdinalIgnoreCase))
            return SurfaceType.Wood;

        if (platformName.StartsWith("Tile", StringComparison.OrdinalIgnoreCase))
            return SurfaceType.Tile;

        if (platformName.StartsWith("Grass", StringComparison.OrdinalIgnoreCase))
            return SurfaceType.Grass;

        return SurfaceType.Default;
    }
}