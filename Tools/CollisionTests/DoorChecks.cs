using Ars3D.ModelFormat;
using Ars3D.Rendering;
using _3DLight;
using _3DLight.Interaction;
using Levels.Doors;
using Microsoft.Xna.Framework;
using NMatrix = System.Numerics.Matrix4x4;
using NVector = System.Numerics.Vector3;

internal static class DoorChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("configured doors honor the model origin even when it is centered", () =>
        {
            ModelData data = Fixture();
            data.Nodes[1] = new NodeData("Door", 0, NMatrix.CreateTranslation(3f, 0f, 4f));
            data.Meshes[1] = new MeshData
            {
                Node = 1,
                Vertices = data.Meshes[1].Vertices.Select(vertex => vertex with
                {
                    Position = vertex.Position - new NVector(0.5f, 0f, 0f)
                }).ToArray(),
                Indices = data.Meshes[1].Indices
            };
            var hierarchy = new ModelHierarchy(data);
            var doors = new LevelDoors(data, hierarchy, [Definition()]);
            var door = (LevelDoor)doors.Interactables.Single();
            door.Interact();
            doors.Update(1f, []);
            Near(door.Hinge, Vector3.Zero);
            Near(Vector3.Transform(door.Hinge, doors.LocalTransforms[1]), new(3f, 0f, 4f));
            Near(Vector3.Transform(new Vector3(0.5f, 0f, 0f), doors.LocalTransforms[1]), new(3f, 0f, 3.5f));
            Require(hierarchy.LocalTransforms[1] == Matrix.CreateTranslation(3f, 0f, 4f), "Bind transforms were overwritten");
        });

        run("door JSON selects model pivot or computed hinge and defaults to model pivot", () =>
        {
            string modelName = "pivot_test_" + Guid.NewGuid().ToString("N");
            string path = Path.Combine(AppContext.BaseDirectory, "Content", "Levels", modelName + ".doors.json");
            try
            {
                File.WriteAllText(path, """
                    [
                      { "node": "Door", "openAngleDegrees": 90, "usesModelPivot": true },
                      { "node": "Door", "openAngleDegrees": 90, "usesModelPivot": false },
                      { "node": "Door", "openAngleDegrees": 90 }
                    ]
                    """);
                DoorDefinition[] definitions = DoorDefinition.Load(modelName);
                Require(definitions.Select(value => value.UsesModelPivot).SequenceEqual(new[] { true, false, true }),
                    "JSON did not preserve the pivot choices or the default");
                ModelData data = Fixture();
                data.Nodes[1] = new NodeData("Door", 0, NMatrix.CreateTranslation(3f, 0f, 4f));
                data.Meshes[1] = new MeshData
                {
                    Node = 1,
                    Vertices = data.Meshes[1].Vertices.Select(vertex => vertex with
                    {
                        Position = vertex.Position - new NVector(0.5f, 0f, 0f)
                    }).ToArray(),
                    Indices = data.Meshes[1].Indices
                };
                var hierarchy = new ModelHierarchy(data);
                foreach (DoorDefinition definition in definitions)
                {
                    var doors = new LevelDoors(data, hierarchy, [definition]);
                    var door = (LevelDoor)doors.Interactables.Single();
                    Vector3 hinge = definition.UsesModelPivot ? Vector3.Zero : new(-0.5f, 0f, 0f);
                    Near(door.Hinge, hinge);
                    door.Interact();
                    doors.Update(1f, []);
                    Near(Vector3.Transform(hinge, doors.LocalTransforms[1]), hinge + new Vector3(3f, 0f, 4f));
                }
            }
            finally { File.Delete(path); }
        });

        foreach (bool discoverNamedDoors in new[] { false, true })
        foreach (bool fbxPivot in new[] { false, true })
        {
            run($"{(discoverNamedDoors ? "discovered" : "configured")} doors keep their {(fbxPivot ? "FBX helper pivot" : "right-hand origin")} fixed under a rotated scaled parent", () =>
                CheckModelPivot(discoverNamedDoors, fbxPivot));
        }

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
            var door = (LevelDoor)doors.Interactables.Single();
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
            Require(definitions.Any(value => value.Node == "Door_03") &&
                definitions.Any(value => value.Node == "Door_01"), "The shop and toilet door configuration was not copied");
            var hierarchy = new ModelHierarchy(data);
            var doors = new LevelDoors(data, hierarchy, definitions);
            var shopDoor = (LevelDoor)doors.Interactables.Single(value => ((LevelDoor)value).Definition.Node == "Door_03");
            Require(shopDoor.MeshIds.Count == 3, "Some parts of the shop door were omitted");
            BoundingBox bounds = shopDoor.Bounds;
            Vector3 eye = new((bounds.Min.X + bounds.Max.X) / 2f, 1.6f, bounds.Max.Z + 1.5f);
            Level level = new();
            level.InitializeGeometry(data, hierarchy, definitions);
            Require(Interact(level, true, eye)?.Contains("ЗАКРЫТЬ") == true, "The real shop door could not be targeted");
        });
    }

    private static DoorDefinition Definition() => new() { Node = "Door", OpenAngleDegrees = 90f };

    private static void CheckModelPivot(bool discoverNamedDoors, bool fbxPivot)
    {
        var data = new ModelData();
        data.Nodes.Add(new("Root", -1, NMatrix.CreateScale(1.3f, 2.1f, 0.8f) *
            NMatrix.CreateRotationX(0.45f) * NMatrix.CreateRotationZ(0.2f) *
            NMatrix.CreateTranslation(3f, 1f, 4f)));
        int parent = 0;
        int pivotNode = -1;
        if (fbxPivot)
        {
            data.Nodes.Add(new("Door_$AssimpFbx$_Translation", parent, NMatrix.CreateTranslation(2f, 0f, 1f)));
            pivotNode = data.Nodes.Count;
            data.Nodes.Add(new("Door_$AssimpFbx$_RotationPivot", 1, NMatrix.CreateTranslation(0.75f, 0f, 0f)));
            data.Nodes.Add(new("Door_$AssimpFbx$_RotationPivotInverse", pivotNode, NMatrix.CreateTranslation(-0.75f, 0f, 0f)));
            parent = data.Nodes.Count - 1;
        }
        int doorNode = data.Nodes.Count;
        data.Nodes.Add(new("Door", parent, NMatrix.CreateRotationY(0.3f)));
        int handleNode = data.Nodes.Count;
        data.Nodes.Add(new("Handle", doorNode, NMatrix.CreateTranslation(-0.2f, 1f, 0.1f)));
        data.Meshes.Add(Quad(doorNode, new(-1f, 0f, 0f), new(0f, 0f, 0f), new(0f, 2f, 0f), new(-1f, 2f, 0f)));
        data.Meshes.Add(Quad(handleNode, new(0f, 0f, 0f), new(0.1f, 0f, 0f), new(0.1f, 0.1f, 0f), new(0f, 0.1f, 0f)));
        var hierarchy = new ModelHierarchy(data);
        Matrix[] bind = hierarchy.BindPoseGlobalTransforms.ToArray();
        Vector3 worldPivot = bind[pivotNode < 0 ? doorNode : pivotNode].Translation;
        var doors = new LevelDoors(data, hierarchy, discoverNamedDoors ? [] : [Definition()], discoverNamedDoors);
        var door = (LevelDoor)doors.Interactables.Single();
        Require(door.MeshIds.Count == 2, "The child handle was not attached to the door");
        door.Interact();
        doors.Update(0.5f, []);
        var open = new Matrix[data.Nodes.Count];
        hierarchy.CalculateGlobalTransforms(doors.LocalTransforms, open);
        Near(Vector3.Transform(door.Hinge, open[doorNode]), worldPivot);
        foreach (MeshData mesh in data.Meshes)
        foreach (VertexData vertex in mesh.Vertices)
        {
            Vector3 position = new(vertex.Position.X, vertex.Position.Y, vertex.Position.Z);
            Vector3 offset = Vector3.Transform(position, bind[mesh.Node]) - worldPivot;
            // A quarter-turn about world Y keeps height and swaps the horizontal axes.
            Vector3 expected = worldPivot + new Vector3(offset.Z, offset.Y, -offset.X);
            Near(Vector3.Transform(position, open[mesh.Node]), expected);
        }
        Require(hierarchy.BindPoseGlobalTransforms.SequenceEqual(bind), "Door rotation changed the bind pose");
        door.Interact();
        doors.Update(1f, []);
        Require(doors.LocalTransforms.SequenceEqual(hierarchy.LocalTransforms), "Closing did not restore the exact bind transforms");
    }

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
