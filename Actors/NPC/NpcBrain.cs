using Microsoft.Xna.Framework;

namespace _3DLight.Npcs;

internal sealed class NpcBrain(float speed)
{
    private const float ArrivalDistance = 0.18f;
    private const float ArrivalHeightTolerance = 0.55f;

    public NpcIntent Tick(Vector3 position, NpcOrder order, float seconds)
    {
        switch (order.Kind)
        {
            case NpcOrderKind.Wait:
                return default;

            case NpcOrderKind.GoTo:
                Vector3 offset = order.Destination - position;
                float heightDifference = MathF.Abs(offset.Y);
                offset.Y = 0f;
                float distance = offset.Length();

                if (distance <= ArrivalDistance && heightDifference <= ArrivalHeightTolerance)
                    return new(Vector3.Zero, ReachedDestination: true);
                if (distance < 0.001f)
                    return default;

                float step = MathF.Min(speed * seconds, distance);
                return new(offset / distance * step);

            default:
                throw new ArgumentOutOfRangeException(nameof(order), order.Kind, "Неизвестная команда NPC.");
        }
    }
}