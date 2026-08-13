using System;
using System.Collections.Generic;

internal sealed class GameEntity
{
    private readonly Dictionary<Type, IGameComponent> components = [];

    public GameEntity Add(IGameComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        components[component.GetType()] = component;
        return this;
    }

    public GameEntity Add<T>(T component) where T : class, IGameComponent
    {
        components[typeof(T)] = component;
        return this;
    }

    public T Get<T>() where T : class, IGameComponent
    {
        if (components.TryGetValue(typeof(T), out IGameComponent? component))
            return (T)component;

        throw new InvalidOperationException(
            $"У сущности отсутствует компонент {typeof(T).Name}.");
    }

    public bool Has<T>() where T : class, IGameComponent =>
        components.ContainsKey(typeof(T));
}