using Microsoft.Xna.Framework;

namespace _3DLight.Npcs;

internal sealed class NpcDirector
{
    private readonly Dictionary<string, Patrol> patrols = new(StringComparer.OrdinalIgnoreCase);

    public Vector3 Register(string id, Vector3[] route, float waitSeconds)
    {
        patrols.Add(id, new Patrol(route, waitSeconds));
        return route[0];
    }

    public NpcOrder Update(string id, float seconds, bool reachedDestination)
    {
        Patrol patrol = patrols[id];
        if (reachedDestination)
        {
            patrol.Next = (patrol.Next + 1) % patrol.Route.Length;
            patrol.WaitRemaining = patrol.WaitSeconds;
        }

        if (patrol.WaitRemaining > 0f)
        {
            patrol.WaitRemaining -= seconds;
            return new(NpcOrderKind.Wait, default);
        }

        return new(NpcOrderKind.GoTo, patrol.Route[patrol.Next]);
    }

    private sealed class Patrol(Vector3[] route, float waitSeconds)
    {
        public Vector3[] Route { get; } = route;
        public float WaitSeconds { get; } = waitSeconds;
        public int Next { get; set; } = 1;
        public float WaitRemaining { get; set; }
    }
}