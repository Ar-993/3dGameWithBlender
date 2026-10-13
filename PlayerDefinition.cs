using Ars3D.Items;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace _3DLight;

public sealed class PlayerDefinition
{
    public ItemInteractionSettings Interaction { get; }

    public PlayerDefinition(ItemInteractionSettings? interaction = null) =>
        Interaction = interaction ?? new ItemInteractionSettings();

    public static PlayerDefinition Load(string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "Content", "Player", "player.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        return JsonSerializer.Deserialize<PlayerDefinition>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException($"Empty player configuration: {path}");
    }
}
