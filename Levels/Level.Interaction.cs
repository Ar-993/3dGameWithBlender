using _3DLight.Interaction;
using Microsoft.Xna.Framework;

namespace _3DLight;

public partial class Level
{
    private readonly List<IInteractable> interactables = [];
    internal IEnumerable<IInteractable> Interactables => interactables;
    internal void AddInteractable(IInteractable interactable) => interactables.Add(interactable);

    internal bool IsInteractionVisible(Ray ray, float distance, IReadOnlySet<int> ignoredMeshes)
    {
        Vector3 end = ray.Position + ray.Direction * distance;
        Vector3 margin = new(0.01f);
        BoundingBox query = new(Vector3.Min(ray.Position, end) - margin, Vector3.Max(ray.Position, end) + margin);
        foreach (TriangleCollider triangle in GetCollisionCandidates(query))
        {
            if (triangle.SupportPlatform is { } platform && ignoredMeshes.Contains(platform.Id))
                continue;
            if (RayHit(ray, triangle) is { } hit && hit < distance - 0.02f)
                return false;
        }
        return true;
    }

    private static float? RayHit(Ray ray, TriangleCollider triangle)
    {
        Vector3 edge1 = triangle.B - triangle.A;
        Vector3 edge2 = triangle.C - triangle.A;
        Vector3 cross = Vector3.Cross(ray.Direction, edge2);
        float determinant = Vector3.Dot(edge1, cross);
        if (MathF.Abs(determinant) < 0.000001f)
            return null;
        float inverse = 1f / determinant;
        Vector3 offset = ray.Position - triangle.A;
        float u = Vector3.Dot(offset, cross) * inverse;
        Vector3 q = Vector3.Cross(offset, edge1);
        float v = Vector3.Dot(ray.Direction, q) * inverse;
        if (u < 0f || v < 0f || u + v > 1f)
            return null;
        float distance = Vector3.Dot(edge2, q) * inverse;
        return distance >= 0f ? distance : null;
    }
}
