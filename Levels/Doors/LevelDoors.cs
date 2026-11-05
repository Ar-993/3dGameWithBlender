using Ars3D.ModelFormat;
using Ars3D.Rendering;
using _3DLight.Collision;
using _3DLight.Interaction;
using Microsoft.Xna.Framework;
using _3DLight;

namespace Levels.Doors;

// Матрицы створок общие для рендера и коллизии.
internal sealed class LevelDoors
{
    private readonly ModelData data;
    private readonly ModelHierarchy hierarchy;
    private readonly Matrix[] globalTransforms;
    private readonly List<LevelDoor> doors = [];

    public Matrix[] LocalTransforms { get; }
    public HashSet<int> MeshIds { get; } = [];
    public IEnumerable<IInteractable> Interactables => doors;
    public IEnumerable<Level.TriangleCollider> Colliders => doors.SelectMany(door => door.Colliders);

    public LevelDoors(ModelData data, ModelHierarchy hierarchy, IReadOnlyList<DoorDefinition> definitions,
        bool discoverNamedDoors = false)
    {
        this.data = data;
        this.hierarchy = hierarchy;
        LocalTransforms = (Matrix[])hierarchy.LocalTransforms.Clone();
        globalTransforms = new Matrix[hierarchy.NodeCount];

        var discovery = new DoorDiscovery(data, hierarchy);
        foreach (DoorDefinition definition in definitions)
        {
            definition.Validate();
            int node = data.Nodes.FindIndex(value => value.Name.Equals(definition.Node, StringComparison.OrdinalIgnoreCase));
            if (node < 0)
                throw new InvalidDataException($"Узел двери '{definition.Node}' не найден в модели.");

            HashSet<int> meshIds = discovery.GetDoorMeshes(node);
            if (meshIds.Count == 0)
                throw new InvalidDataException($"У двери '{definition.Node}' нет мешей.");
            if (meshIds.Overlaps(MeshIds))
                throw new InvalidDataException($"Меши двери '{definition.Node}' уже заняты другой дверью.");

            AddDoor(definition, node, meshIds, discovery);
        }

        if (!discoverNamedDoors)
            return;

        // Родители в модели записаны раньше детей. Именованная створка забирает
        // свои дочерние меши, например ручку и стекло.
        foreach (int node in discovery.FindNamedNodes())
        {
            HashSet<int> meshIds = discovery.GetDoorMeshes(node);
            if (meshIds.Count == 0)
                continue;
            if (meshIds.Overlaps(MeshIds))
                continue;

            var definition = new DoorDefinition { Node = data.Nodes[node].Name };
            AddDoor(definition, node, meshIds, discovery);
        }
    }

    private void AddDoor(DoorDefinition definition, int node, HashSet<int> meshIds, DoorDiscovery discovery)
    {
        Vector3 hinge = definition.UsesModelPivot
            ? discovery.FindModelPivot(node)
            : discovery.FindLeftHinge(node, meshIds);
        var door = new LevelDoor(definition, node, meshIds, hinge);
        Refresh(door);
        door.RememberClosedBounds();
        MeshIds.UnionWith(meshIds);
        doors.Add(door);
    }

    private void Refresh(LevelDoor door)
    {
        int node = door.NodeIndex;
        if (door.Angle != 0f)
        {
            Matrix bindWorld = hierarchy.BindPoseGlobalTransforms[node];
            Vector3 worldHinge = Vector3.Transform(door.Hinge, bindWorld);
            int parent = data.Nodes[node].Parent;
            Matrix worldToParent = parent < 0 ? Matrix.Identity :
                Matrix.Invert(hierarchy.BindPoseGlobalTransforms[parent]);
            // Y мира остаётся вертикальной осью даже у повёрнутых FBX-узлов.
            LocalTransforms[node] = bindWorld * Matrix.CreateTranslation(-worldHinge) *
                Matrix.CreateRotationY(door.Angle) * Matrix.CreateTranslation(worldHinge) * worldToParent;
        }
        else
        {
            LocalTransforms[node] = hierarchy.LocalTransforms[node];
        }
        hierarchy.CalculateGlobalTransforms(LocalTransforms, globalTransforms);
        List<Level.TriangleCollider> colliders = LevelGeometryBuilder.BuildTriangleColliders(data, globalTransforms, door.MeshIds);
        if (colliders.Count == 0)
            throw new InvalidDataException($"У двери '{door.Definition.Node}' нет треугольников.");
        door.SetColliders(colliders);
    }

    public bool Update(float seconds, ReadOnlySpan<CapsuleShape> occupants)
    {
        if (!float.IsFinite(seconds) || seconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(seconds));

        bool changed = false;
        foreach (LevelDoor door in doors)
        {
            float target = door.TargetOpen ? MathHelper.ToRadians(door.Definition.OpenAngleDegrees) : 0f;
            float difference = target - door.Angle;
            float travel = MathF.Min(MathF.Abs(difference), MathHelper.ToRadians(door.Definition.DegreesPerSecond) * seconds);
            bool reachesTarget = travel >= MathF.Abs(difference);
            // Небольшие угловые шаги не позволяют створке перескочить через персонажа.
            int steps = (int)MathF.Ceiling(travel / MathHelper.ToRadians(3f));
            for (int step = 0; step < steps; step++)
            {
                float previous = door.Angle;
                // Последний шаг точно восстанавливает закрытую bind-матрицу без остаточного угла.
                door.SetAngle(step == steps - 1 && reachesTarget
                    ? target
                    : previous + MathF.CopySign(travel / steps, difference));
                Refresh(door);
                if (TouchesOccupant(door, occupants))
                {
                    door.SetAngle(previous);
                    Refresh(door);
                    break;
                }
                changed = true;
            }
        }
        return changed;
    }

    private static bool TouchesOccupant(LevelDoor door, ReadOnlySpan<CapsuleShape> occupants)
    {
        foreach (CapsuleShape occupant in occupants)
        {
            if (!CapsuleCollision.Overlaps(occupant.Bounds, door.Bounds))
                continue;
            foreach (Level.TriangleCollider triangle in door.Colliders)
                if (CapsuleCollision.GetContact(occupant, triangle).Penetration > 0.001f)
                    return true;
        }
        return false;
    }

}
