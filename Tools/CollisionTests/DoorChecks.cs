using Ars3D.ModelFormat;
using Ars3D.Rendering;
using _3DLight;
using _3DLight.Interaction;
using Microsoft.Xna.Framework;
using NMatrix = System.Numerics.Matrix4x4;
using NVector = System.Numerics.Vector3;

internal static class DoorChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("doors rotate about the authored origin without changing the bind pose", () =>
        {
            ModelData data = Fixture();
            data.Nodes[1] = new NodeData("Door", 0, NMatrix.CreateTranslation(3f, 0f, 4f));
            var hierarchy = new ModelHierarchy(data);
            var doors = new LevelDoors(data, hierarchy, [Definition()]);
            var door = (LevelDoors.Door)doors.Interactables.Single();
            door.Interact();
            doors.Update(1f, []);
            Near(Vector3.Transform(Vector3.Zero, doors.LocalTransforms[1]), new(3f, 0f, 4f));
            Near(Vector3.Transform(Vector3.UnitX, doors.LocalTransforms[1]), new(3f, 0f, 3f));
            Require(hierarchy.LocalTransforms[1] == Matrix.CreateTranslation(3f, 0f, 4f), "Bind transforms were overwritten");
        });

        run("closed door blocks movement and repeated interaction opens and closes the passage", () =>
        {
            Level level = CreateLevel(Fixture());
            Require(CrossDoor(level).Z > 0.19f, "The closed door did not block the character");
            Require(Interact(level, pressed: true)!.Contains("ЗАКРЫТЬ"), "Opening did not change the hint");
            Require(CrossDoor(level).Z < -0.5f, "The opened door left an invisible collider in the passage");
            Require(Interact(level, pressed: true)!.Contains("ОТКРЫТЬ"), "Closing did not change the hint");
            Require(CrossDoor(level).Z > 0.19f, "The closed door did not restore its collider");
        });

        run("doors reject distant or occluded interaction", () =>
        {
            Level level = CreateLevel(Fixture(wall: true));
            Require(Interact(level, pressed: true) is null, "The door could be used through a wall");
            level = CreateLevel(Fixture());
            Require(Interact(level, true, new(0.5f, 1.5f, 5f)) is null && CrossDoor(level).Z > 0.19f, "A distant press opened the door");
            Interact(level, true, enabled: false);
            Require(CrossDoor(level).Z > 0.19f, "Suppressed interaction opened the door");
        });

        run("a closing door stops at the character and finishes after the passage is clear", () =>
        {
            ModelData data = Fixture();
            var doors = new LevelDoors(data, new ModelHierarchy(data), [Definition()]);
            var door = (LevelDoors.Door)doors.Interactables.Single();
            door.Interact();
            doors.Update(1f, []);
            door.Interact();
            doors.Update(1f, [new CapsuleShape(new(0.5f, 0f, -0.1f), 0.2f, 1.8f)]);
            Require(door.Angle > 0.01f, "The door closed through the character");
            doors.Update(1f, []);
            Require(MathF.Abs(door.Angle) < 0.0001f, "The door did not finish closing after clearance");
        });

        run("the real shop door includes its metal, handle and glass and can be opened", () =>
        {
            using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "Models", "gas_station.3dmodel"));
            ModelData data = ModelDataIo.Read(stream);
            DoorDefinition[] definitions = DoorDefinition.Load("gas_station");
            Require(definitions.Length == 1 && definitions[0].Node == "Door_03", "The shop door configuration was not copied");
            var hierarchy = new ModelHierarchy(data);
            var doors = new LevelDoors(data, hierarchy, definitions);
            Require(doors.MeshIds.Count == 3, "Some parts of the shop door were omitted");
            BoundingBox bounds = doors.Colliders.Select(value => value.Bounds).Aggregate(BoundingBox.CreateMerged);
            Vector3 eye = new((bounds.Min.X + bounds.Max.X) / 2f, 1.6f, bounds.Max.Z + 1.5f);
            Level level = new();
            level.InitializeGeometry(data, hierarchy, definitions);
            Require(Interact(level, true, eye)?.Contains("ЗАКРЫТЬ") == true, "The real shop door could not be targeted");
        });
    }

    private static DoorDefinition Definition() => new() { Node = "Door", OpenAngleDegrees = 90f };
    private static Level CreateLevel(ModelData data)
    {
        Level level = new();
        level.InitializeGeometry(data, new ModelHierarchy(data), [Definition()]);
        return level;
    }

    private static string? Interact(Level level, bool pressed, Vector3? eye = null, bool enabled = true)
    {
        var interaction = new InteractionSystem(level.IsInteractionVisible);
        interaction.Update(level.Interactables, eye ?? new(0.5f, 1.5f, 1f), Vector3.Forward, pressed, enabled);
        level.UpdateDoors(1f, []);
        return interaction.Hint;
    }

    private static Vector3 CrossDoor(Level level)
    {
        float velocity = 0f;
        return level.MoveCharacter(new(0.5f, 0.001f, 1f), new(0f, 0f, -2f),
            0.2f, 1.8f, null, ref velocity, out _);
    }

    private static ModelData Fixture(bool wall = false)
    {
        var data = new ModelData();
        data.Nodes.Add(new("Root", -1, NMatrix.Identity));
        data.Nodes.Add(new("Door", 0, NMatrix.Identity));
        data.Meshes.Add(Quad(0, new(-5f, 0f, -5f), new(5f, 0f, -5f), new(5f, 0f, 5f), new(-5f, 0f, 5f)));
        data.Meshes.Add(Quad(1, new(0f, 0f, 0f), new(1f, 0f, 0f), new(1f, 2.5f, 0f), new(0f, 2.5f, 0f)));
        if (wall)
            data.Meshes.Add(Quad(0, new(-1f, 0f, 0.5f), new(2f, 0f, 0.5f), new(2f, 3f, 0.5f), new(-1f, 3f, 0.5f)));
        return data;
    }

    private static MeshData Quad(int node, NVector a, NVector b, NVector c, NVector d) => new()
    {
        Node = node,
        Vertices = new[] { a, b, c, d }.Select(position =>
            new VertexData(position, NVector.UnitZ, default, 0, 0, 0, 0, default)).ToArray(),
        Indices = [0, 1, 2, 0, 2, 3]
    };

    private static void Near(Vector3 actual, Vector3 expected) =>
        Require(Vector3.Distance(actual, expected) < 0.001f, $"Expected {expected}, got {actual}");
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
