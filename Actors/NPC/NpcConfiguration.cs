using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace _3DLight.Npcs;

internal sealed class NpcConfiguration
{
    public Dictionary<string, NpcSkin> Skins { get; init; } = [];
    public NpcDefinition[] Actors { get; init; } = [];

    public static NpcConfiguration Load(string levelName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Content", "Levels", levelName + ".npcs.json");
        if (!File.Exists(path))
            return new();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var config = JsonSerializer.Deserialize<NpcConfiguration>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException($"Пустой файл NPC: {path}");
        config.Validate();
        return config;
    }

    private void Validate()
    {
        if (Skins is null || Actors is null)
            throw new InvalidDataException("Настройки NPC должны содержать skins и actors.");

        foreach ((string id, NpcSkin skin) in Skins)
        {
            if (skin is null || string.IsNullOrWhiteSpace(skin.Model))
                throw new InvalidDataException($"У skin '{id}' укажи model.");
            if (string.IsNullOrWhiteSpace(skin.IdleClip) || string.IsNullOrWhiteSpace(skin.WalkClip))
                throw new InvalidDataException($"У skin '{id}' укажи idleClip и walkClip.");
            if (!float.IsFinite(skin.VisualYawDegrees))
                throw new InvalidDataException($"У skin '{id}' visualYawDegrees должен быть конечным числом.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (NpcDefinition actor in Actors)
        {
            if (actor is null || string.IsNullOrWhiteSpace(actor.Id))
                throw new InvalidDataException("У каждого NPC должен быть id.");
            if (!ids.Add(actor.Id))
                throw new InvalidDataException($"Повторяется id NPC '{actor.Id}'.");
            if (actor.Skin is null || !Skins.ContainsKey(actor.Skin))
                throw new InvalidDataException($"У NPC '{actor.Id}' неизвестный skin '{actor.Skin}'.");

            actor.Validate();
        }
    }
}

internal sealed class NpcSkin
{
    public string Model { get; init; } = "";
    public string IdleClip { get; init; } = "Idle";
    public string WalkClip { get; init; } = "Walk";
    public float VisualYawDegrees { get; init; }
}

internal sealed class NpcDefinition
{
    public string Id { get; init; } = "";
    public string Skin { get; init; } = "";
    public string Behavior { get; init; } = "patrol";
    public float Speed { get; init; } = 1.35f;
    public float Radius { get; init; } = 0.3f;
    public float Height { get; init; } = 1.75f;
    public float WaitSeconds { get; init; } = 0.7f;
    public NpcWaypoint[] Route { get; init; } = [];

    public void Validate()
    {
        if (Behavior != "patrol")
            throw new InvalidDataException($"У NPC '{Id}' ожидается behavior 'patrol'.");
        if (!float.IsFinite(Speed) || Speed <= 0f)
            throw new InvalidDataException($"У NPC '{Id}' speed должен быть положительным конечным числом.");
        if (!float.IsFinite(Radius) || Radius <= 0f || !float.IsFinite(Height) || Height < 2f * Radius)
            throw new InvalidDataException($"У NPC '{Id}' нужны конечные radius > 0 и height >= 2 × radius.");
        if (!float.IsFinite(WaitSeconds) || WaitSeconds < 0f)
            throw new InvalidDataException($"У NPC '{Id}' waitSeconds должен быть неотрицательным конечным числом.");
        if (Route is not { Length: >= 2 } || Array.Exists(Route, point => point is null))
            throw new InvalidDataException($"У NPC '{Id}' route должен содержать хотя бы две заполненные точки.");
    }
}

internal sealed class NpcWaypoint
{
    public string? Marker { get; init; }
    public float[]? Position { get; init; }

    public Vector3 Resolve(Level level, string actorId)
    {
        switch (Position, Marker)
        {
            case ([var x, var y, var z], null) when float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z):
                return new(x, y, z);

            case (null, string marker) when !string.IsNullOrWhiteSpace(marker):
                if (level.TryGetMarkerPosition(marker, out Vector3 markerPosition))
                    return markerPosition;
                throw new InvalidDataException($"Маркер '{marker}' из маршрута NPC '{actorId}' отсутствует на уровне.");

            default:
                throw new InvalidDataException($"Для точки маршрута NPC '{actorId}' укажи position [x, y, z] или marker.");
        }
    }
}