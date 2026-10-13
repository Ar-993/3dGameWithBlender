<<<<<<< HEAD
using Ars3D.Items;
=======
﻿using Ars3D.Items;
>>>>>>> 8382ade9ed414a4aba4fbc5a5a803e2a91aff003
using System.Text.Json;
using System.Text.Json.Serialization;

namespace _3DLight;

public sealed class PlayerDefinition
{
    public ItemInteractionSettings Interaction { get; }

<<<<<<< HEAD
    public PlayerDefinition(ItemInteractionSettings? interaction = null) =>
        Interaction = interaction ?? new ItemInteractionSettings();

    public static PlayerDefinition Load(string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "Content", "Player", "player.json");
=======
    public PlayerDefinition(ItemInteractionSettings? interaction = null)
    {
        Interaction = interaction ?? new ItemInteractionSettings();
    }

    public static PlayerDefinition Load(string? path = null)
    {
        path ??= Path.Combine(
            AppContext.BaseDirectory,
            "Content", "Player", "player.json");

>>>>>>> 8382ade9ed414a4aba4fbc5a5a803e2a91aff003
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
<<<<<<< HEAD
        return JsonSerializer.Deserialize<PlayerDefinition>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException($"Empty player configuration: {path}");
=======

        return JsonSerializer.Deserialize<PlayerDefinition>(
            File.ReadAllText(path), options)
            ?? throw new InvalidDataException(
                $"Empty player configuration: {path}");
>>>>>>> 8382ade9ed414a4aba4fbc5a5a803e2a91aff003
    }
}
