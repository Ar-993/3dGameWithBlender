using Ars3D.ModelFormat;
using Ars3D.Rendering;
using _3DLight.Collision;
using _3DLight.Interaction;
using Microsoft.Xna.Framework;

namespace _3DLight;

public partial class Level
{
    private LevelDoors? levelDoors;

    internal void InitializeGeometry(ModelData data, ModelHierarchy hierarchy, IReadOnlyList<DoorDefinition> definitions)
    {
        List<Platform> newPlatforms = LevelGeometryBuilder.BuildPlatforms(data, hierarchy.BindPoseGlobalTransforms);
        List<TriangleCollider> newTriangles = LevelGeometryBuilder.BuildTriangleColliders(data, hierarchy.BindPoseGlobalTransforms);
        LevelDoors? newDoors = definitions.Count == 0 ? null : new LevelDoors(data, hierarchy, definitions);
        if (newDoors is not null)
            newTriangles.RemoveAll(triangle => newDoors.MeshIds.Contains(triangle.SupportPlatform!.Id));

        platforms.Clear();
        platforms.AddRange(newPlatforms);
        meshColliders.Clear();
        meshColliders.AddRange(newTriangles);
        levelDoors = newDoors;
        interactables.Clear();
        if (newDoors is not null)
            interactables.AddRange(newDoors.Interactables);
        collisionIndex = null;
    }

    internal void UpdateDoors(float seconds, ReadOnlySpan<CapsuleShape> occupants)
    {
        if (levelDoors is null)
            return;

        if (levelDoors.Update(seconds, occupants))
        {
            foreach (Platform platform in levelDoors.Colliders.Select(triangle => triangle.SupportPlatform!).DistinctBy(value => value.Id))
            {
                int index = platforms.FindIndex(previous => previous.Id == platform.Id);
                platforms[index] = platform;
            }
        }
    }

}
