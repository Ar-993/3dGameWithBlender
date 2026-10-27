using Microsoft.Xna.Framework;

namespace _3DLight.Interaction;

internal sealed class InteractionSystem(Func<Ray, float, IReadOnlySet<int>, bool> isVisible)
{
    public IInteractable? Target { get; private set; }
    public string? Hint => Target is null ? null : "[E] " + Target.InteractionText;

    public void Update(IEnumerable<IInteractable> objects, Vector3 eye, Vector3 forward,
        bool interactPressed, bool enabled)
    {
        Target = enabled ? FindTarget(objects, new Ray(eye, Vector3.Normalize(forward))) : null;
        if (interactPressed)
            Target?.Interact();
    }

    private IInteractable? FindTarget(IEnumerable<IInteractable> objects, Ray ray)
    {
        IInteractable? nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (IInteractable candidate in objects)
        {
            if (candidate.GetHitDistance(ray) is not { } distance ||
                distance > candidate.InteractionDistance || distance >= nearestDistance)
                continue;
            if (!isVisible(ray, distance, candidate.MeshIds))
                continue;

            nearest = candidate;
            nearestDistance = distance;
        }
        return nearest;
    }
}
