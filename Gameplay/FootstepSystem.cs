using System;
using Characters;
using Microsoft.Xna.Framework;

namespace _3DLight.Gameplay;

internal sealed class FootstepSystem
{
    private const float StepDistance = 2.2f;
    private readonly Random random = new();

    private Vector3 lastPosition;
    private float distanceAccumulated;
    private bool initialized;

    public event Action<string>? StepPlayed;

    public void Reset(Vector3 position)
    {
        lastPosition = position;
        distanceAccumulated = 0f;
        initialized = true;
    }

    public void Update(Player player)
    {
        if (!initialized)
        {
            Reset(player.Position);
            return;
        }

        Vector3 currentPosition = player.Position;

        // В воздухе или при смерти не копим дистанцию шага
        if (player.IsDead || player.CurrentPlatform is null)
        {
            lastPosition = currentPosition;
            distanceAccumulated = 0f;
            return;
        }

        Vector2 delta = new(
            currentPosition.X - lastPosition.X,
            currentPosition.Z - lastPosition.Z);

        lastPosition = currentPosition;
        distanceAccumulated += delta.Length();

        if (distanceAccumulated >= StepDistance)
        {
            distanceAccumulated -= StepDistance;
            EmitStep(player.CurrentPlatform.Name);
        }
    }

    private void EmitStep(string platformName)
    {
        SurfaceType surface = SurfaceResolver.Resolve(platformName);
        string prefix = surface switch
        {
            SurfaceType.Asphalt => "step_asphalt",
            SurfaceType.Carpet => "step_carpet",
            SurfaceType.Wood => "step_wood",
            SurfaceType.Tile => "step_tile",
            SurfaceType.Grass => "step_grass",
            _ => "step_default"
        };

        int variant = random.Next(1, 3);
        StepPlayed?.Invoke($"{prefix}{variant}");
    }
}