using Microsoft.Xna.Framework;

namespace _3DLight.Npcs;

internal enum NpcOrderKind
{
    Wait,
    GoTo
}

internal readonly record struct NpcOrder(
    NpcOrderKind Kind,
    Vector3 Destination);

internal readonly record struct NpcIntent(
    Vector3 Movement,
    bool ReachedDestination = false);