using System.Numerics;

namespace Ars3D.NPC;

public enum NpcOrderKind
{
    Wait,
    GoTo,
    FollowPlayer
}

public enum NpcState
{
    Idle,
    Moving,
    Blocked,
    Hurt,
    Dead
}

public readonly record struct NpcOrder(
    NpcOrderKind Kind,
    Vector3 Destination,
    float StopDistance = 0.5f);

public readonly record struct NpcFacts(
    Vector3 Position,
    bool IsDead,
    bool IsHurt,
    bool SeesPlayer,
    Vector3 PlayerPosition);

public readonly record struct NpcIntent(
    Vector3 DesiredVelocity,
    NpcState State,
    bool ReachedDestination = false);

public interface INpcNavigation
{
    bool TryGetNextWaypoint(
        Vector3 from,
        Vector3 destination,
        out Vector3 waypoint);
}