using Microsoft.Xna.Framework;

namespace _3DLight.Gameplay;

internal static class GameplayCharacters
{
    private const float Gravity = -28f;

    public const float CharacterCollisionRadius = 0.35f;
    public const float CharacterCollisionHeight = 1.8f;
    public static readonly Vector3 PlayerSpawnCoordinates = new(-1f, 0.03f, -0.06f);
    public static readonly Vector3 SkeletonSpawnCoordinates = new(3f, 0.03f, -0.06f);

    public static Player CreatePlayer()
    {
        var physics = new CharacterPhysicsComponent(
            PlayerSpawnCoordinates,
            CharacterCollisionRadius,
            CharacterCollisionHeight,
            Gravity);
        var stats = new StatsComponent(maxHealth: 100);
        var attack = new AttackComponent(
            damage: 25,
            range: 1.6f,
            hitTimeNormalized: 0.45f);

        return new Player(physics, stats, attack, PlayerDefinition.Load());
    }

    public static Skeleton CreateSkeleton()
    {
        var physics = new CharacterPhysicsComponent(
            SkeletonSpawnCoordinates,
            CharacterCollisionRadius,
            CharacterCollisionHeight,
            Gravity);
        var animation = new CharacterAnimationComponent(modelScale: 1f);
        var stats = new StatsComponent(maxHealth: 100);
        var ai = new SkeletonAIComponent();

        return new Skeleton(physics, animation, stats, ai);
    }
}
