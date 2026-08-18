namespace _3DLight;

internal sealed class SkeletonAIComponent : AIComponent
{
    public SkeletonAIComponent()
        : base(
            detectionRange: 60f,
            attackRange: 1.4f,
            hitTimeNormalized: 0.45f,
            attackCooldown: 0.3f)
    {
    }
}
