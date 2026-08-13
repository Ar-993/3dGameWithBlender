using System;

internal static class EntityFactory
{
    public static GameEntity Create(params IGameComponent[] components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var entity = new GameEntity();

        foreach (IGameComponent component in components)
            entity.Add(component);

        return entity;
    }
}
