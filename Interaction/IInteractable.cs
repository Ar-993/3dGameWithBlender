using Microsoft.Xna.Framework;

namespace _3DLight.Interaction;

// Только взаимодействие с объектами сцены. Подбор предметов сюда не входит.
internal interface IInteractable
{
    string InteractionText { get; }
    float InteractionDistance { get; }
    IReadOnlySet<int> MeshIds { get; }
    float? GetHitDistance(Ray ray);
    void Interact();
}
