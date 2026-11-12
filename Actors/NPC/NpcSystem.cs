using Ars3D.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Npcs;

internal sealed class NpcSystem : IDisposable
{
    private readonly List<NpcController> actors = [];
    private NpcDirector director = new();
    private CapsuleShape[] capsules = [];
    private CapsuleShape[] obstacles = [];

    public ReadOnlySpan<CapsuleShape> Capsules => capsules;

    public static NpcSystem Load(Level level, string levelName, GraphicsDevice graphics,
        Texture2D fallbackTexture, Effect effect)
    {
        NpcConfiguration config = NpcConfiguration.Load(levelName);
        var system = new NpcSystem();
        try
        {
            foreach (NpcDefinition definition in config.Actors)
            {
                var actor = new NpcController(definition, config.Skins[definition.Skin]);
                system.actors.Add(actor);
                actor.LoadContent(graphics, fallbackTexture, effect);
            }
            system.capsules = new CapsuleShape[system.actors.Count];
            system.obstacles = new CapsuleShape[system.actors.Count + 1];
            system.ResetRoutes(level);
            return system;
        }
        catch
        {
            system.Dispose();
            throw;
        }
    }

    public void ResetRoutes(Level level)
    {
        var replacement = new NpcDirector();
        var spawns = new Vector3[actors.Count];
        for (int i = 0; i < actors.Count; i++)
        {
            NpcDefinition definition = actors[i].Definition;
            Vector3[] route = definition.Route.Select(point => point.Resolve(level, definition.Id)).ToArray();
            spawns[i] = replacement.Register(definition.Id, route, definition.WaitSeconds);
        }
        for (int i = 0; i < actors.Count; i++)
        {
            actors[i].SpawnAt(spawns[i]);
            capsules[i] = actors[i].Capsule;
        }
        director = replacement;
    }

    public void Update(Level level, float seconds, CapsuleShape player, CapsuleShape skeleton)
    {
        if (actors.Count == 0)
            return;

        obstacles[0] = player;
        obstacles[1] = skeleton;
        for (int i = 0; i < actors.Count; i++)
        {
            // Копируем капсулы до и после текущего NPC.
            capsules.AsSpan(0, i).CopyTo(obstacles.AsSpan(2));
            capsules.AsSpan(i + 1).CopyTo(obstacles.AsSpan(i + 2));

            NpcController actor = actors[i];
            var order = director.Update(actor.Definition.Id, seconds, actor.ReachedDestination);
            actor.Update(level, order, seconds, obstacles);
            capsules[i] = actor.Capsule;
        }
    }

    public void Draw(Matrix view, Matrix projection, SceneLighting lighting, SceneRenderQueue queue)
    {
        foreach (NpcController actor in actors)
            actor.Draw(view, projection, lighting, queue);
    }

    public void Dispose()
    {
        foreach (NpcController actor in actors)
            actor.Dispose();
        actors.Clear();
        capsules = [];
        obstacles = [];
        director = new();
    }
}