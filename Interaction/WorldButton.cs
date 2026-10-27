using Microsoft.Xna.Framework;

namespace _3DLight.Interaction;

// Кнопка вызывает переданное действие: свет, питание, дверь или другой сценарий.
internal sealed class WorldButton(
    BoundingBox bounds,
    IReadOnlySet<int> meshIds,
    Action onPressed,
    string interactionText = "НАЖАТЬ КНОПКУ",
    float interactionDistance = 2.5f) : IInteractable
{
    public string InteractionText { get; } = interactionText;
    public float InteractionDistance { get; } = interactionDistance;
    public IReadOnlySet<int> MeshIds { get; } = meshIds;
    public float? GetHitDistance(Ray ray) => ray.Intersects(bounds);
    public void Interact() => onPressed();
}
