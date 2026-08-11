using Microsoft.Xna.Framework;

internal static class CharacterFactory
{
    private const float Gravity = -28f;
    private const float CollisionRadius = 0.35f;
    private const float CollisionHeight = 1.8f;

    public static Player CreatePlayer() =>
        new(CreateFacade(new Vector3(850f, 17.5f, 45f), modelScale: 0.01f));

    public static Skeleton CreateSkeleton(Vector3 startPosition) =>
        new(CreateFacade(startPosition, modelScale: 1f));

    private static CharacterFacade CreateFacade(Vector3 startPosition, float modelScale) =>
        new(new GameEntity()
            .Add(new CharacterPhysicsComponent(
                startPosition,
                CollisionRadius,
                CollisionHeight,
                Gravity))
            .Add(new CharacterAnimationComponent(modelScale)));
}
