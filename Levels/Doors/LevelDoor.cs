using _3DLight;
using _3DLight.Interaction;
using Microsoft.Xna.Framework;

namespace Levels.Doors;

internal sealed class LevelDoor(DoorDefinition definition, int nodeIndex, HashSet<int> meshIds,
    Vector3 hinge) : IInteractable
{
    public DoorDefinition Definition { get; } = definition;
    public int NodeIndex { get; } = nodeIndex;
    public Vector3 Hinge { get; } = hinge;
    public IReadOnlySet<int> MeshIds { get; } = meshIds;
    public bool TargetOpen { get; private set; }
    public float Angle { get; private set; }
    public BoundingBox ClosedBounds { get; private set; }
    public BoundingBox Bounds { get; private set; }
    public IReadOnlyList<Level.TriangleCollider> Colliders { get; private set; } = [];
    public string InteractionText => $"{(TargetOpen ? "ЗАКРЫТЬ" : "ОТКРЫТЬ")} {Definition.DisplayName}";
    public float InteractionDistance => Definition.InteractionDistance;
    public void Interact() => TargetOpen = !TargetOpen;
    internal void SetAngle(float value) => Angle = value;
    internal void RememberClosedBounds() => ClosedBounds = Bounds;
    internal void SetColliders(List<Level.TriangleCollider> colliders)
    {
        Colliders = colliders;
        Bounds = colliders.Select(triangle => triangle.Bounds).Aggregate(BoundingBox.CreateMerged);
    }

    public float? GetHitDistance(Ray ray)
    {
        // Проём остаётся целью и после открытия створки.
        float distance = MathF.Min(ray.Intersects(Bounds) ?? float.PositiveInfinity,
            ray.Intersects(ClosedBounds) ?? float.PositiveInfinity);
        return float.IsFinite(distance) ? distance : null;
    }
}
