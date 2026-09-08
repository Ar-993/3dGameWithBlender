using System.Reflection;
using System.Runtime.CompilerServices;
using _3DLight;
using Microsoft.Xna.Framework;

const float radius = 0.35f;
const float height = 1.8f;
int failures = 0;

Run("fast fall onto a zero-thickness floor", () =>
{
    Level level = Floor(0f);
    Land(level, new Vector3(0f, 12f, 0f), new Vector3(0f, -30f, 0f), 0f);
});
Run("lower floor in a mesh with several heights", () =>
{
    Level level = Floor(0f);
    AddQuad(level, "Floor", new(5f, 3f, -2f), new(9f, 3f, -2f),
        new(9f, 3f, 2f), new(5f, 3f, 2f));
    // The previous floor importer described the entire mesh with this box.
    Platforms(level)[0] = new(0, "Floor", new(new(-2f, 0f, -2f), new(9f, 3f, 2f)));
    Land(level, new(0f, 0.2f, 0f), new(0f, -0.5f, 0f), 0f);
});
Run("local height of a thin sloping floor", () =>
{
    Level level = new();
    Platforms(level).Add(new(0, "Floor", new(new(-2f, 0f, -2f), new(2f, 1f, 2f))));
    AddQuad(level, "Floor", new(-2f, 0f, -2f), new(2f, 1f, -2f),
        new(2f, 1f, 2f), new(-2f, 0f, 2f));
    Vector3 result = Move(level, new(-1f, 0.6f, 0f), new(0f, -1f, 0f), out var ground);
    Require(ground is not null && result.Y >= 0.24f && result.Y <= 0.35f,
        $"Expected local surface near y=0.25; got {result}, grounded={ground is not null}");
});
Run("hole inside a floor mesh stays open", () =>
{
    Level level = new();
    Platforms(level).Add(new(0, "Floor", new(new(-4f, 0f, -4f), new(4f, 0f, 4f))));
    AddQuad(level, "Floor", new(-4f, 0f, -4f), new(-2f, 0f, -4f),
        new(-2f, 0f, 4f), new(-4f, 0f, 4f));
    AddQuad(level, "Floor", new(2f, 0f, -4f), new(4f, 0f, -4f),
        new(4f, 0f, 4f), new(2f, 0f, 4f));
    Vector3 result = Move(level, new(0f, 0.2f, 0f), new(0f, -0.5f, 0f), out var ground);
    Require(ground is null && result.Y < 0f, $"Hole was filled: {result}");
});
Run("downward-facing thin floor also supports characters", () =>
{
    Level level = new();
    Platforms(level).Add(new(0, "Floor", new(new(-2f, 0f, -2f), new(2f, 0f, 2f))));
    AddQuad(level, "Floor", new(-2f, 0f, -2f), new(-2f, 0f, 2f),
        new(2f, 0f, 2f), new(2f, 0f, -2f));
    Land(level, new(0f, 1f, 0f), new(0f, -2f, 0f), 0f);
});
Run("zero-thickness ceiling stops a jump", () =>
{
    Level level = Floor(3f);
    Vector3 result = Move(level, new(0f, 0f, 0f), new(0f, 5f, 0f), out var ground);
    Near(result.Y, 3f - height);
    Require(ground is null, "Ceiling is not ground");
});
Run("stationary character stays grounded for 600 frames", () =>
{
    Level level = Floor(0f);
    Vector3 position = new(0f, 0.05f, 0f);
    Level.Platform? ground = null;
    float velocity = 0f;
    for (int frame = 0; frame < 600; frame++)
    {
        velocity -= 20f / 60f;
        position = level.MoveCharacter(position, new(0f, velocity / 60f, 0f),
            radius, height, ground, ref velocity, out ground);
    }
    Near(position.Y, 0f);
    Require(ground is not null, "Lost ground contact");
});
Run("a character below a floor is not teleported onto it", () =>
{
    // The whole capsule is below the floor. A partially intersecting capsule
    // belongs to penetration recovery, tested separately below.
    Vector3 result = Move(Floor(0f), new(0f, -height - 0.2f, 0f), new(0f, -0.2f, 0f), out var ground);
    Near(result.Y, -height - 0.4f);
    Require(ground is null, "Attached to an overhead floor");
});
Run("nearest of two stacked thin floors catches a fast fall", () =>
{
    Level level = Floor(0f);
    AddQuad(level, "Floor", new(-2f, 3f, -2f), new(2f, 3f, -2f),
        new(2f, 3f, 2f), new(-2f, 3f, 2f));
    Land(level, new(0f, 10f, 0f), new(0f, -20f, 0f), 3f);
});
Run("foot radius supports a character at the edge", () =>
{
    float expandedRadius = radius + CapsuleCollision.SkinWidth;
    float roundedFootY = MathF.Sqrt(expandedRadius * expandedRadius - 0.2f * 0.2f) - radius;
    Land(Floor(0f), new(2.2f, 0.2f, 0f), new(0f, -0.4f, 0f), roundedFootY);
    Vector3 result = Move(Floor(0f), new(2.4f, 0.2f, 0f), new(0f, -0.4f, 0f), out var ground);
    Require(ground is null && result.Y < 0f, "Supported beyond the foot radius");
});
Run("small floor supports a character", () =>
{
    Level level = new();
    AddQuad(level, "FloorSmall", new(-0.1f, 0f, -0.1f), new(0.1f, 0f, -0.1f),
        new(0.1f, 0f, 0.1f), new(-0.1f, 0f, 0.1f));
    Land(level, new(0f, 0.2f, 0f), new(0f, -0.4f, 0f), 0f);
});
Run("unnamed stair risers can be climbed and descended", () =>
{
    Level level = Floor(0f);
    AddQuad(level, "Object.001", new(0f, 0.5f, -1f), new(1f, 0.5f, -1f),
        new(1f, 0.5f, 1f), new(0f, 0.5f, 1f));
    AddQuad(level, "Object.001", new(0f, 0f, -1f), new(0f, 0.5f, -1f),
        new(0f, 0.5f, 1f), new(0f, 0f, 1f));
    AddQuad(level, "Object.002", new(1f, 1f, -1f), new(2f, 1f, -1f),
        new(2f, 1f, 1f), new(1f, 1f, 1f));
    AddQuad(level, "Object.002", new(1f, 0.5f, -1f), new(1f, 1f, -1f),
        new(1f, 1f, 1f), new(1f, 0.5f, 1f));
    Vector3 position = new(-0.6f, 0f, 0f);
    Level.Platform? ground = Platforms(level)[0];
    float velocity = 0f;
    for (int frame = 0; frame < 40; frame++)
        position = level.MoveCharacter(position, new(0.05f, -0.01f, 0f),
            radius, height, ground, ref velocity, out ground);
    Require(position.X > 1f && ground is not null, $"Did not reach the second tread: {position}");
    Near(position.Y, 1f);
    for (int frame = 0; frame < 40; frame++)
        position = level.MoveCharacter(position, new(-0.05f, -0.01f, 0f),
            radius, height, ground, ref velocity, out ground);
    Near(position.Y, 0f);
    Require(ground is not null, "Lost contact while descending");
});
Run("a tall object is not treated as a climbable step", () =>
{
    Level level = Floor(0f);
    AddQuad(level, "Object.001", new(0f, 2f, -1f), new(2f, 2f, -1f),
        new(2f, 2f, 1f), new(0f, 2f, 1f));
    AddQuad(level, "Object.001", new(0f, 0f, -1f), new(0f, 2f, -1f),
        new(0f, 2f, 1f), new(0f, 0f, 1f));
    Vector3 position = new(-0.6f, 0f, 0f);
    Level.Platform? ground = Platforms(level)[0];
    float velocity = 0f;
    for (int frame = 0; frame < 40; frame++)
        position = level.MoveCharacter(position, new(0.05f, -0.01f, 0f),
            radius, height, ground, ref velocity, out ground);
    Require(position.X <= -radius, $"Walked through obstacle: {position}");
    Near(position.Y, 0f);
});
Run("step height is limited across the entire movement", () =>
{
    Level level = Floor(0f);
    AddQuad(level, "Object.001", new(0f, 0.5f, -1f), new(0.1f, 0.5f, -1f),
        new(0.1f, 0.5f, 1f), new(0f, 0.5f, 1f));
    AddQuad(level, "Object.002", new(0.1f, 1f, -1f), new(2f, 1f, -1f),
        new(2f, 1f, 1f), new(0.1f, 1f, 1f));
    float velocity = 0f;
    Vector3 result = level.MoveCharacter(new(-0.3f, 0f, 0f), new(0.1f, -0.01f, 0f),
        radius, height, Platforms(level)[0], ref velocity, out _);
    Require(result.Y >= 0f && result.Y <= 0.55f + CapsuleCollision.SkinWidth,
        $"Exceeded maximum step height: {result}");
});
Run("a capsule cannot squeeze onto a step under a low ceiling", () =>
{
    Level level = Floor(0f);
    AddQuad(level, "Object.001", new(0f, 0.5f, -1f), new(2f, 0.5f, -1f),
        new(2f, 0.5f, 1f), new(0f, 0.5f, 1f));
    AddQuad(level, "Object.001", new(0f, 0f, -1f), new(0f, 0.5f, -1f),
        new(0f, 0.5f, 1f), new(0f, 0f, 1f));
    AddQuad(level, "Object.002", new(-2f, 2f, -1f), new(2f, 2f, -1f),
        new(2f, 2f, 1f), new(-2f, 2f, 1f));
    float velocity = 0f;
    Vector3 result = new(-0.36f, 0f, 0f);
    Level.Platform? ground = Platforms(level)[0];
    for (int frame = 0; frame < 40; frame++)
    {
        result = level.MoveCharacter(result, new(0.1f, -0.01f, 0f),
            radius, height, ground, ref velocity, out ground);
        Require(result.Y + height <= 2f && result.X < 0f, $"Climbed into the ceiling: {result}");
        var capsule = new CapsuleShape(result, radius, height);
        Require(Triangles(level).All(t => CapsuleCollision.GetContact(capsule, t).Penetration < 0.002f),
            $"Capsule intersects the ceiling or step: {result}");
    }
});

Run("capsule has the requested total height and hemispheres", () =>
{
    CapsuleShape shape = new(new(1f, 2f, 3f), radius, height);
    Near(shape.AxisStart.Y, 2f + radius);
    Near(shape.AxisEnd.Y, 2f + height - radius);
    Near(shape.Bounds.Min.Y, 2f);
    Near(shape.Bounds.Max.Y, 2f + height);
});
Run("rounded head clears an overhang outside the hemisphere", () =>
{
    Level level = new();
    AddQuad(level, "Overhang", new(0.3f, 1.7f, -1f), new(0.3f, 3f, -1f),
        new(0.3f, 3f, 1f), new(0.3f, 1.7f, 1f));
    CapsuleShape capsule = new(Vector3.Zero, radius, height);
    float distance = Triangles(level).Min(triangle => CapsuleCollision.GetContact(capsule, triangle).Distance);
    Near(distance, MathF.Sqrt(0.3f * 0.3f + 0.25f * 0.25f));
    Require(!CapsuleCollision.Cast(capsule, new(0.04f, 0f, 0f), Triangles(level), out _),
        "A rectangular head incorrectly blocked movement");
});
Run("a slanted triangle is tested at its actual height", () =>
{
    Vector3 a = new(0.2f, 2.1f, -1f), b = new(0.2f, 2.1f, 1f), c = new(2f, 0f, 0f);
    Level.TriangleCollider triangle = new("Slant", a, b, c,
        Vector3.Normalize(Vector3.Cross(b - a, c - a)), new(Vector3.Min(a, c), Vector3.Max(b, c)));
    CapsuleShape capsule = new(Vector3.Zero, radius, height);
    Require(CapsuleCollision.GetContact(capsule, triangle).Distance > radius,
        "A projected wall created a false collision");
    Require(!CapsuleCollision.Cast(capsule, new(0.05f, 0f, 0f), new[] { triangle }, out _),
        "A slanted face blocked empty space");
});
Run("fast horizontal motion cannot cross a thin wall", () =>
{
    Level level = new();
    AddQuad(level, "Wall", new(0f, -2f, -5f), new(0f, 5f, -5f),
        new(0f, 5f, 5f), new(0f, -2f, 5f));
    Vector3 result = Move(level, new(-4f, 0f, 0f), new(12f, 0f, 0f), out _);
    Near(result.X, -radius - CapsuleCollision.SkinWidth);
});
Run("capsule slides along a wall without losing tangential motion", () =>
{
    Level level = new();
    AddQuad(level, "Wall", new(0f, -2f, -5f), new(0f, 5f, -5f),
        new(0f, 5f, 5f), new(0f, -2f, 5f));
    Vector3 result = Move(level, new(-1f, 0f, -1f), new(3f, 0f, 2f), out _);
    Near(result.X, -radius - CapsuleCollision.SkinWidth);
    Near(result.Z, 1f);
});
Run("both planes of a corner stop the capsule", () =>
{
    Level level = new();
    AddQuad(level, "WallX", new(0f, -2f, -5f), new(0f, 5f, -5f),
        new(0f, 5f, 5f), new(0f, -2f, 5f));
    AddQuad(level, "WallZ", new(-5f, -2f, 0f), new(5f, -2f, 0f),
        new(5f, 5f, 0f), new(-5f, 5f, 0f));
    Vector3 result = Move(level, new(-1f, 0f, -1f), new(3f, 0f, 3f), out _);
    Near(result.X, -radius - CapsuleCollision.SkinWidth);
    Near(result.Z, -radius - CapsuleCollision.SkinWidth);
});
Run("fast capsule cast stops on a diagonal wall", () =>
{
    Level level = new();
    AddQuad(level, "Diagonal", new(-4f, -3f, 4f), new(4f, -3f, -4f),
        new(4f, 4f, -4f), new(-4f, 4f, 4f));
    Vector3 result = Move(level, new(-1f, 0f, -1f), new(4f, 0f, 4f), out _);
    float coordinate = -(radius + CapsuleCollision.SkinWidth) / MathF.Sqrt(2f);
    Near(result.X, coordinate);
    Near(result.Z, coordinate);
});
Run("slope contact includes the lower sphere radius", () =>
{
    Level level = new();
    AddQuad(level, "Slope", new(-4f, -1f, -4f), new(4f, 1f, -4f),
        new(4f, 1f, 4f), new(-4f, -1f, 4f));
    float expected = (radius + CapsuleCollision.SkinWidth) * MathF.Sqrt(1f + 0.25f * 0.25f) - radius;
    Land(level, new(0f, 2f, 0f), new(0f, -4f, 0f), expected);
});
Run("sloped ceiling contacts the upper sphere", () =>
{
    Level level = new();
    AddQuad(level, "Ceiling", new(-4f, 0f, -4f), new(4f, 4f, -4f),
        new(4f, 4f, 4f), new(-4f, 0f, 4f));
    Vector3 result = Move(level, Vector3.Zero, Vector3.Up * 3f, out var ground);
    float expected = 2f - (height - radius) - (radius + CapsuleCollision.SkinWidth) * MathF.Sqrt(1.25f);
    Near(result.Y, expected);
    Require(ground is null, "A ceiling became ground");
});
Run("capsule overlap is recovered above a thin floor", () =>
{
    Vector3 result = Move(Floor(0f), new(0f, -0.05f, 0f), Vector3.Zero, out var ground);
    Near(result.Y, CapsuleCollision.SkinWidth);
    Require(ground is not null, "Recovery did not find ground");
});
Run("a jump detaches from the floor immediately", () =>
{
    Level level = Floor(0f);
    float velocity = 4f;
    Vector3 result = level.MoveCharacter(new(0f, 0.001f, 0f), new(0f, 0.15f, 0f),
        radius, height, null, ref velocity, out var ground);
    Require(result.Y > 0.14f && ground is null && velocity > 0f, "Ground snap cancelled the jump");
});
Run("a sphere is the limiting case of a capsule", () =>
{
    Level level = Floor(0f);
    CapsuleShape sphere = new(new(0f, 3f, 0f), radius, 2f * radius);
    Require(CapsuleCollision.Cast(sphere, Vector3.Down * 6f, Triangles(level), out var hit), "Sphere missed floor");
    Near(sphere.Feet.Y - 6f * hit.Fraction, CapsuleCollision.SkinWidth);
});
Run("a capsule cast finds a triangle vertex contact", () =>
{
    Vector3 a = new(0.3f, 1.7f, 0.3f), b = new(2f, 1.7f, 0.3f), c = new(0.3f, 3f, 0.3f);
    Level.TriangleCollider triangle = new("Vertex", a, b, c,
        Vector3.Normalize(Vector3.Cross(b - a, c - a)), new(Vector3.Min(a, c), Vector3.Max(b, c)));
    CapsuleShape capsule = new(Vector3.Zero, radius, height);
    Require(CapsuleCollision.Cast(capsule, new(0.4f, 0f, 0.4f), new[] { triangle }, out var hit),
        "Missed a vertex contact");
    float expandedRadius = radius + CapsuleCollision.SkinWidth;
    float horizontalGap = MathF.Sqrt((expandedRadius * expandedRadius - 0.25f * 0.25f) / 2f);
    Near(hit.Fraction, (0.3f - horizontalGap) / 0.4f);
});
Run("horizontal input does not climb a steep slope", () =>
{
    Level level = Floor(0f);
    AddQuad(level, "Steep", new(0f, 0f, -2f), new(2f, 4f, -2f),
        new(2f, 4f, 2f), new(0f, 0f, 2f));
    Vector3 position = new(-0.6f, 0f, 0f);
    Level.Platform? ground = Platforms(level)[0];
    float velocity = 0f;
    for (int frame = 0; frame < 40; frame++)
        position = level.MoveCharacter(position, new(0.05f, -0.01f, 0f), radius, height, ground, ref velocity, out ground);
    Require(position.X < 0f && position.Y < 0.01f, $"Climbed steep geometry: {position}");
});

foreach (string modelPath in args)
{
    Level level = ReadLevel(modelPath);
    Console.WriteLine($"MODEL {modelPath}: {Platforms(level).Count} platforms, {Triangles(level).Count} triangles");
    Run("every imported mesh participates in collisions", () =>
    {
        object model = typeof(Level).GetField("model", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(level)!;
        object data = model.GetType().GetField("modelData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(model)!;
        var meshes = ((System.Collections.IEnumerable)data.GetType().GetProperty("Meshes")!.GetValue(data)!).Cast<object>().ToArray();
        int[] expectedIds = Enumerable.Range(0, meshes.Length).Where(index =>
            ((Array)meshes[index].GetType().GetProperty("Vertices")!.GetValue(meshes[index])!).Length > 0 &&
            ((int[])meshes[index].GetType().GetProperty("Indices")!.GetValue(meshes[index])!).Length >= 3).ToArray();
        var actualIds = Triangles(level).Select(triangle => triangle.SupportPlatform!.Id).ToHashSet();
        Require(actualIds.SetEquals(expectedIds), $"Missing mesh IDs: {string.Join(", ", expectedIds.Except(actualIds))}");
        Require(Platforms(level).Select(platform => platform.Id).ToHashSet().SetEquals(expectedIds),
            "Platform bounds omit meshes or include empty nodes");
    });
    Run("renaming nodes does not change any collider", () =>
    {
        Func<int, string>[] renamings =
        [
            index => $"Объект_{index}",
            _ => "SameName",
            index => new[] { "Stair", "Step.001", "Object.Step1", "Doorway_Sides", "" }[index % 5]
        ];
        foreach (var rename in renamings)
        {
            Level renamed = ReadLevel(modelPath, rename);
            Require(Platforms(renamed).Count == Platforms(level).Count, "Renaming changed platform count");
            Require(Triangles(renamed).Count == Triangles(level).Count, "Renaming changed triangle count");
            for (int i = 0; i < Triangles(level).Count; i++)
            {
                var original = Triangles(level)[i];
                var changed = Triangles(renamed)[i];
                Require(original.A == changed.A && original.B == changed.B && original.C == changed.C &&
                    original.Normal == changed.Normal && original.SupportPlatform!.Id == changed.SupportPlatform!.Id,
                    $"Renaming changed collider {i}");
            }
        }
    });
    if (!Path.GetFileNameWithoutExtension(modelPath).Equals("level_one", StringComparison.OrdinalIgnoreCase))
        continue;
    Run("imported Plane.003 is a collidable floor", () =>
    {
        var planes = Triangles(level).Where(t => t.Name == "Plane.003" && MathF.Abs(t.Normal.Y) >= 0.7f).ToArray();
        Require(planes.Length > 0, "Plane.003 was omitted by the importer");
        // Isolate the imported floor from walls/roof geometry to test its actual facets.
        Level floorOnly = new();
        Triangles(floorOnly).AddRange(planes);
        foreach (var triangle in planes)
        {
            Vector3 center = (triangle.A + triangle.B + triangle.C) / 3f;
            Land(floorOnly, center + Vector3.Up * 5f, Vector3.Down * 10f, center.Y);
        }
    });
    foreach (string marker in new[] { "PlayerSpawn", "SkeletonSpawn" })
    {
        Run($"{marker} settles on the imported level floor", () =>
        {
            Require(level.TryGetMarkerPosition(marker, out Vector3 position), $"Missing marker {marker}");
            Level.Platform? ground = null;
            float velocity = 0f;
            for (int frame = 0; frame < 600; frame++)
            {
                velocity -= 28f / 60f;
                position = level.MoveCharacter(position, new(0f, velocity / 60f, 0f),
                    radius, height, ground, ref velocity, out ground);
            }
            Require(ground is not null, $"Character fell through level: {position}");
            Console.WriteLine($"  {marker}: {position}, support={ground!.Name}");
        });
    }
}

Console.WriteLine($"Collision regressions: {failures} failure(s).");
return failures == 0 ? 0 : 1;

void Run(string name, Action test)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failures++; Console.WriteLine($"FAIL {name}: {error.Message}"); }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Near(float actual, float expected) =>
    Require(MathF.Abs(actual - expected) <= 0.002f, $"Expected {expected}, got {actual}");

static List<Level.Platform> Platforms(Level level) =>
    (List<Level.Platform>)typeof(Level).GetField("platforms", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(level)!;

static List<Level.TriangleCollider> Triangles(Level level) =>
    (List<Level.TriangleCollider>)typeof(Level).GetField("meshColliders", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(level)!;

static Level Floor(float y)
{
    Level level = new();
    Platforms(level).Add(new(0, "Floor", new(new(-2f, y, -2f), new(2f, y, 2f))));
    AddQuad(level, "Floor", new(-2f, y, -2f), new(2f, y, -2f),
        new(2f, y, 2f), new(-2f, y, 2f));
    return level;
}

static void AddQuad(Level level, string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
{
    AddTriangle(a, b, c);
    AddTriangle(a, c, d);
    void AddTriangle(Vector3 first, Vector3 second, Vector3 third) => Triangles(level).Add(new(
        name, first, second, third, Vector3.Normalize(Vector3.Cross(second - first, third - first)),
        new(Vector3.Min(first, Vector3.Min(second, third)), Vector3.Max(first, Vector3.Max(second, third)))));
}

static Vector3 Move(Level level, Vector3 position, Vector3 movement, out Level.Platform? ground)
{
    float velocity = movement.Y;
    return level.MoveCharacter(position, movement, radius, height, null, ref velocity, out ground);
}

static void Land(Level level, Vector3 position, Vector3 movement, float expectedY)
{
    Vector3 result = Move(level, position, movement, out var ground);
    Near(result.Y, expectedY);
    Require(ground is not null, "Expected ground contact");
}

// Exercise the real importer without creating a graphics device or opening the game.
static Level ReadLevel(string path, Func<int, string>? rename = null)
{
    Assembly assembly = typeof(Level).Assembly;
    Type modelType = assembly.GetType("_3DLight.CompiledModel", true)!;
    Type ioType = Assembly.Load("ModelFormat").GetType("_3DLight.Assets.ModelDataIo", true)!;
    using FileStream stream = File.OpenRead(path);
    object data = ioType.GetMethod("Read")!.Invoke(null, [stream])!;
    object model = RuntimeHelpers.GetUninitializedObject(modelType);
    const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
    modelType.GetField("modelData", instance)!.SetValue(model, data);
    var nodes = (System.Collections.IList)data.GetType().GetProperty("Nodes")!.GetValue(data)!;
    if (rename is not null)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            object node = nodes[index]!;
            nodes[index] = Activator.CreateInstance(node.GetType(), rename(index),
                node.GetType().GetProperty("Parent")!.GetValue(node), node.GetType().GetProperty("Bind")!.GetValue(node));
        }
    }
    MethodInfo convert = modelType.GetMethod("ToXnaMatrix", BindingFlags.Static | BindingFlags.NonPublic)!;
    Matrix[] local = nodes.Cast<object>().Select(node =>
        (Matrix)convert.Invoke(null, [node.GetType().GetProperty("Bind")!.GetValue(node)])!).ToArray();
    Matrix[] global = new Matrix[local.Length];
    modelType.GetMethod("CalculateGlobalTransforms", instance)!.Invoke(model, [local, global]);
    modelType.GetField("bindPoseGlobalTransforms", instance)!.SetValue(model, global);
    Level level = new();
    typeof(Level).GetField("model", instance)!.SetValue(level, model);
    Platforms(level).AddRange((List<Level.Platform>)modelType.GetMethod("BuildPlatforms")!.Invoke(model, null)!);
    Triangles(level).AddRange((List<Level.TriangleCollider>)modelType.GetMethod("BuildTriangleColliders")!.Invoke(model, null)!);
    return level;
}
