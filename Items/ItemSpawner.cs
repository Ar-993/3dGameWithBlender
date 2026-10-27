using _3DLight;
using _3DLight.Items;
using Microsoft.Xna.Framework;

namespace Items;

internal static class ItemSpawner
{
    public static WorldItem Spawn(Level level, ItemCatalog catalog, string itemId,
        string markerName, Vector3 fallbackPosition)
    {
        Vector3 position = level.TryGetMarkerPosition(markerName, out Vector3 marker)
            ? marker : fallbackPosition;
        Console.WriteLine($"Item '{itemId}' at '{markerName}': {position}");
        return catalog.Create(itemId, position);
    }

    public static void SpawnConfigured(Level level, ItemCatalog catalog,
        ItemInteraction interaction, Vector3 playerSpawn)
    {
        foreach (ItemSpawnEntry spawn in catalog.Spawns)
        {
            Vector3 fallback = spawn.Fallback ?? playerSpawn + new Vector3(0.8f, 1f, -1.2f);
            interaction.Add(Spawn(level, catalog, spawn.ItemId, spawn.Marker, fallback));
        }
    }
}
