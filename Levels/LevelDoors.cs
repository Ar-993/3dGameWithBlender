using Ars3D.ModelFormat;
using Ars3D.Rendering;
using _3DLight.Collision;
using _3DLight.Interaction;
using Microsoft.Xna.Framework;

namespace _3DLight;

// Ось петель вычисляется по левому краю геометрии. Матрицы общие для рендера и коллизии.
internal sealed class LevelDoors
{
    internal sealed class Door(DoorDefinition definition, int nodeIndex, HashSet<int> meshIds, Vector3 hinge) : IInteractable
    {
        public DoorDefinition Definition { get; } = definition;
        public int NodeIndex { get; } = nodeIndex;
        public Vector3 Hinge { get; } = hinge;
        public IReadOnlySet<int> MeshIds { get; } = meshIds;
        public bool TargetOpen { get; private set; }
        public float Angle { get; private set; }
        public BoundingBox ClosedBounds { get; private set; }
        public BoundingBox Bounds { get; private set; }
        public IReadOnlyList<Level.TriangleCollider> Colliders { get; private set; } = [];
        public string InteractionText => $"{(TargetOpen ? "ЗАКРЫТЬ" : "ОТКРЫТЬ")} {Definition.DisplayName}";
        public float InteractionDistance => Definition.InteractionDistance;
        public void Interact() => TargetOpen = !TargetOpen;
        internal void SetAngle(float value) => Angle = value;
        internal void RememberClosedBounds() => ClosedBounds = Bounds;
        internal void SetColliders(List<Level.TriangleCollider> colliders)
        {
            Colliders = colliders;
            Bounds = colliders.Select(triangle => triangle.Bounds).Aggregate(BoundingBox.CreateMerged);
        }

        public float? GetHitDistance(Ray ray)
        {
            // Проём остаётся целью и после открытия створки.
            float distance = MathF.Min(ray.Intersects(Bounds) ?? float.PositiveInfinity,
                ray.Intersects(ClosedBounds) ?? float.PositiveInfinity);
            return float.IsFinite(distance) ? distance : null;
        }
    }

    private readonly ModelData data;
    private readonly ModelHierarchy hierarchy;
    private readonly Matrix[] globalTransforms;
    private readonly List<Door> doors = [];

    public Matrix[] LocalTransforms { get; }
    public HashSet<int> MeshIds { get; } = [];
    public IEnumerable<IInteractable> Interactables => doors;
    public IEnumerable<Level.TriangleCollider> Colliders => doors.SelectMany(door => door.Colliders);

    public LevelDoors(ModelData data, ModelHierarchy hierarchy, IReadOnlyList<DoorDefinition> definitions)
    {
        this.data = data;
        this.hierarchy = hierarchy;
        LocalTransforms = (Matrix[])hierarchy.LocalTransforms.Clone();
        globalTransforms = new Matrix[hierarchy.NodeCount];

        foreach (DoorDefinition definition in definitions)
        {
            definition.Validate();
            int node = data.Nodes.FindIndex(value => value.Name.Equals(definition.Node, StringComparison.OrdinalIgnoreCase));
            if (node < 0)
                throw new InvalidDataException($"Узел двери '{definition.Node}' не найден в модели.");

            bool[] mask = hierarchy.CreateNodeHierarchyMask(definition.Node);
            HashSet<int> meshIds = Enumerable.Range(0, data.Meshes.Count)
                .Where(index => mask[data.Meshes[index].Node]).ToHashSet();
            if (meshIds.Count == 0 || meshIds.Overlaps(MeshIds))
                throw new InvalidDataException($"У двери '{definition.Node}' нет мешей или её узлы уже заняты другой дверью.");

            MeshIds.UnionWith(meshIds);
            var door = new Door(definition, node, meshIds, FindLeftHinge(node, meshIds));
            doors.Add(door);
            Refresh(door);
            door.RememberClosedBounds();
        }
    }

    private void Refresh(Door door)
    {
        Matrix rotation = Matrix.CreateTranslation(-door.Hinge) * Matrix.CreateRotationY(door.Angle) *
            Matrix.CreateTranslation(door.Hinge);
        LocalTransforms[door.NodeIndex] = rotation * hierarchy.LocalTransforms[door.NodeIndex];
        hierarchy.CalculateGlobalTransforms(LocalTransforms, globalTransforms);
        List<Level.TriangleCollider> colliders = LevelGeometryBuilder.BuildTriangleColliders(data, globalTransforms, door.MeshIds);
        if (colliders.Count == 0)
            throw new InvalidDataException($"У двери '{door.Definition.Node}' нет треугольников.");
        door.SetColliders(colliders);
    }

    private Vector3 FindLeftHinge(int doorNode, IReadOnlySet<int> meshIds)
    {
        Matrix worldToDoor = Matrix.Invert(hierarchy.BindPoseGlobalTransforms[doorNode]);
        var points = new List<Vector3>();
        foreach (int meshId in meshIds)
        {
            MeshData mesh = data.Meshes[meshId];
            Matrix meshToDoor = hierarchy.BindPoseGlobalTransforms[mesh.Node] * worldToDoor;
            foreach (VertexData vertex in mesh.Vertices)
                points.Add(Vector3.Transform(new(vertex.Position.X, vertex.Position.Y, vertex.Position.Z), meshToDoor));
        }

        float left = points.Min(point => point.X);
        // Толщину берём у левого ребра: выступающая ручка не должна смещать ось петель.
        Vector3[] edge = points.Where(point => point.X <= left + 0.001f).ToArray();
        float middleZ = (edge.Min(point => point.Z) + edge.Max(point => point.Z)) * 0.5f;
        return new Vector3(left, 0f, middleZ);
    }

    public bool Update(float seconds, ReadOnlySpan<CapsuleShape> occupants)
    {
        if (!float.IsFinite(seconds) || seconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(seconds));

        bool changed = false;
        foreach (Door door in doors)
        {
            float target = door.TargetOpen ? MathHelper.ToRadians(door.Definition.OpenAngleDegrees) : 0f;
            float difference = target - door.Angle;
            float travel = MathF.Min(MathF.Abs(difference), MathHelper.ToRadians(door.Definition.DegreesPerSecond) * seconds);
            // Небольшие угловые шаги не позволяют створке перескочить через персонажа.
            int steps = (int)MathF.Ceiling(travel / MathHelper.ToRadians(3f));
            for (int step = 0; step < steps; step++)
            {
                float previous = door.Angle;
                door.SetAngle(previous + MathF.CopySign(travel / steps, difference));
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

    private static bool TouchesOccupant(Door door, ReadOnlySpan<CapsuleShape> occupants)
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
