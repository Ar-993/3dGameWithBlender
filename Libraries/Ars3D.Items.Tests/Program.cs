using System.Numerics;
using System.Xml.Linq;
using Ars3D.Items;
using Ars3D.Items.Physics;

int failures = 0;
int checks = 0;

Run("library and standalone tests have no game, graphics or package dependencies", () =>
{
    string root = FindRepositoryRoot();
    XDocument library = XDocument.Load(Path.Combine(root, "Libraries", "Ars3D.Items", "Ars3D.Items.csproj"));
    Require(!library.Descendants("PackageReference").Any() && !library.Descendants("ProjectReference").Any(),
        "The core library must depend only on the .NET runtime");
    XDocument tests = XDocument.Load(Path.Combine(root, "Libraries", "Ars3D.Items.Tests", "Ars3D.Items.Tests.csproj"));
    string[] references = tests.Descendants("ProjectReference")
        .Select(element => ((string?)element.Attribute("Include") ?? "").Replace('\\', '/')).ToArray();
    Require(references.SequenceEqual(new[] { "../Ars3D.Items/Ars3D.Items.csproj" }) &&
        !tests.Descendants("PackageReference").Any(), "Standalone tests gained a game or test-package dependency");
    foreach (var reference in typeof(ItemInstance).Assembly.GetReferencedAssemblies())
        Require(reference.Name is not null && reference.Name.StartsWith("System", StringComparison.Ordinal),
            $"Non-runtime assembly dependency: {reference.Name}");
});

Run("100 instances share one definition and retain unique instance IDs", () =>
{
    var definition = new ItemDefinition("brass-key", "Brass key");
    var controller = new ItemInteractionController();
    var instances = Enumerable.Range(0, 100)
        .Select(index => new ItemInstance(definition, new Vector3(index * 0.1f, 2f, 0f))).ToArray();
    foreach (ItemInstance instance in instances)
        controller.Add(instance);
    Require(instances.All(instance => ReferenceEquals(instance.Definition, definition)), "Definitions were copied per instance");
    Require(instances.Select(instance => instance.InstanceId).Distinct().Count() == 100,
        "Spawned instances did not receive unique IDs");
    controller.Add(instances[0]);
    Require(controller.Items.Count == 100, "Adding the same object twice duplicated it");
    Throws<ArgumentException>(() => controller.Add(new ItemInstance(definition, Vector3.Zero,
        instanceId: instances[0].InstanceId)));
    Require(controller.Items.Count == 100, "A rejected duplicate ID changed the registered items");
    Require(definition.Id == "brass-key" && definition.DisplayName == "Brass key",
        "Generic item metadata was replaced by an asset-specific value");
    Require(typeof(ItemInstance).Assembly.GetExportedTypes().All(type =>
        !type.FullName!.Contains("Santa", StringComparison.OrdinalIgnoreCase)), "Asset-specific public types leaked into the library");
});

Run("free rigid items fall, spin and rotate around their center of mass", () =>
{
    var definition = new ItemDefinition("body", "Moving body", spawnAngularVelocity: new Vector3(0.8f, 1.2f, 0.4f));
    var item = new ItemInstance(definition, new(1f, 4f, -2f));
    Vector3 originalCenter = item.Center;
    Quaternion originalOrientation = item.Orientation;
    item.UpdatePhysics(new AnalyticWorld(), 1f / 30f);
    Require(item.Velocity.Y < 0f && item.Center.Y < originalCenter.Y, "Gravity did not affect the body");
    Require(MathF.Abs(Quaternion.Dot(item.Orientation, originalOrientation)) < 0.9999f, "Spawn spin did not change orientation");
    Near(new(item.Center.X, 0f, item.Center.Z), new(1f, 0f, -2f));
    Near(item.Position + Vector3.Transform(item.LocalCenter, item.Orientation), item.Center);
    Near(Vector3.Transform(item.LocalCenter, item.WorldPose.ToMatrix()), item.Center);
});

Run("standalone sphere queries settle an item on a plane and wake it when support disappears", () =>
{
    var world = new AnalyticWorld { FloorHeight = 0f };
    var item = new ItemInstance(new ItemDefinition("round-body", "Round body"), new(0f, 2f, 0f));
    for (int frame = 0; frame < 480; frame++)
        item.UpdatePhysics(world, 1f / 120f);
    Require(item.IsResting, $"Body failed to sleep: velocity={item.Velocity}, angular={item.AngularVelocity}");
    Near(item.Velocity, Vector3.Zero);
    Near(item.AngularVelocity, Vector3.Zero);
    Require(MathF.Abs(item.Bounds.Min.Y - AnalyticWorld.Skin) <= 0.0002f,
        $"Body did not rest at the plane: {item.Bounds}");
    world.FloorHeight = null;
    item.UpdatePhysics(world, 1f / 60f);
    Require(!item.IsResting && item.Velocity.Y < 0f, "Removing the host plane left the body asleep in mid-air");
});

Run("one slot picks nearest on the action edge, freezes a held body and throws on the next edge", () =>
{
    var world = new AnalyticWorld();
    var controller = new ItemInteractionController();
    var definition = new ItemDefinition("tool", "Tool", spawnAngularVelocity: new(2f, 0.5f, 1f));
    var far = new ItemInstance(definition, new(1.2f, 0f, 0f));
    var near = new ItemInstance(definition, new(0.4f, 0f, 0f));
    controller.Add(far);
    controller.Add(near);
    Update(controller, world, false);
    Require(controller.NearbyItem == near, "Nearest candidate was not selected");
    Update(controller, world, true);
    Require(controller.HeldItem == near && near.IsHeld && !far.IsHeld && controller.NearbyItem is null,
        "Pickup did not fill exactly one hidden slot");
    Near(near.Velocity, Vector3.Zero);
    Near(near.AngularVelocity, Vector3.Zero);
    Require(!controller.TryPickUp(far), "A second body entered an occupied slot");
    ItemPose frozen = near.WorldPose;
    Update(controller, world, true, seconds: 0.1f);
    Near(near.Position, frozen.Position);
    SameRotation(near.Orientation, frozen.Orientation);
    Require(controller.HeldItem == near, "A held action button immediately threw the item");
    Update(controller, world, false);
    Update(controller, world, true);
    Require(controller.HeldItem is null && !near.IsHeld && near.Velocity.Z < -7f && near.AngularVelocity.Length() > 1f,
        "A new action edge did not release the body with throw motion");
    Require(world.LastPlacement is { } placement &&
        MathF.Abs(placement.Radius - (near.BoundingRadius + controller.Settings.ReleaseClearance)) < 0.0001f,
        "Release did not sweep a sphere containing the entire body");
});

Run("per-definition held yaw preserves a pitched held pose when released", () =>
{
    Vector3 eye = new(0f, 1.6f, 0f);
    Vector3 forward = Vector3.Normalize(new Vector3(0.3f, 0.6f, -0.7f));
    var world = new AnalyticWorld();
    var first = new ItemInstance(new ItemDefinition("front", "Front", heldYaw: 0f), new(0.4f, 0f, 0f));
    var second = new ItemInstance(new ItemDefinition("back", "Back", heldYaw: MathF.PI), new(0.4f, 0f, 0f));
    var front = new ItemInteractionController();
    var back = new ItemInteractionController();
    front.Add(first);
    back.Add(second);
    front.Update(world, 0f, Vector3.Zero, eye, forward, true);
    back.Update(world, 0f, Vector3.Zero, eye, forward, true);
    ItemPose frontPose = front.GetHeldPose(eye, forward);
    ItemPose backPose = back.GetHeldPose(eye, forward);
    Near(frontPose.Position, backPose.Position);
    Near(Vector3.Transform(Vector3.UnitY, frontPose.Orientation), Vector3.Transform(Vector3.UnitY, backPose.Orientation));
    Require(Vector3.Dot(Vector3.Transform(Vector3.UnitX, frontPose.Orientation),
        Vector3.Transform(Vector3.UnitX, backPose.Orientation)) < -0.9999f, "Held yaw was shared globally instead of read from each definition");
    front.Update(world, 0f, Vector3.Zero, eye, forward, false);
    front.Update(world, 0f, Vector3.Zero, eye, forward, true);
    Near(first.Position, frontPose.Position);
    SameRotation(first.Orientation, frontPose.Orientation);
    Near(first.Center, Vector3.Transform(first.LocalCenter, frontPose.ToMatrix()));
    Throws<InvalidOperationException>(() => front.GetHeldPose(eye, forward));
});

Run("interaction settings and body settings remain isolated between controllers and definitions", () =>
{
    var world = new AnalyticWorld();
    var slowSettings = new ItemInteractionSettings(throwSpeed: 3f);
    var slow = new ItemInteractionController(slowSettings);
    var standard = new ItemInteractionController();
    var carry = new ItemCarrySettings(heldOffset: new(0.1f, -0.2f, -0.5f),
        throwLift: 0f, throwSpin: Vector3.Zero);
    var itemA = new ItemInstance(new ItemDefinition("slow", "Slow", interaction: carry), new(0.1f, 0f, 0f));
    var itemB = new ItemInstance(new ItemDefinition("standard", "Standard"), new(0.1f, 0f, 0f));
    slow.Add(itemA);
    standard.Add(itemB);
    Vector3 eye = new(0f, 0.25f, 0f);
    slow.Update(world, 0f, Vector3.Zero, eye, -Vector3.UnitZ, true);
    standard.Update(world, 0f, Vector3.Zero, eye, -Vector3.UnitZ, true);
    Require(Vector3.Distance(slow.GetHeldPose(eye, -Vector3.UnitZ).Position,
        standard.GetHeldPose(eye, -Vector3.UnitZ).Position) > 0.3f, "Held offsets changed globally");
    slow.Update(world, 0f, Vector3.Zero, eye, -Vector3.UnitZ, false);
    standard.Update(world, 0f, Vector3.Zero, eye, -Vector3.UnitZ, false);
    slow.Update(world, 0f, Vector3.Zero, eye, -Vector3.UnitZ, true);
    standard.Update(world, 0f, Vector3.Zero, eye, -Vector3.UnitZ, true);
    Near(itemA.Velocity, new(0f, 0f, -3f));
    Near(itemA.AngularVelocity, Vector3.Zero);
    Near(itemB.Velocity, new(0f, 1.7f, -7.5f));
    Require(itemB.AngularVelocity.Length() > 1f, "One controller's spin setting changed another controller");

    Require(standard.TryPickUp(itemA), "The second player could not pick up the released item");
    Near(standard.GetHeldPose(eye, -Vector3.UnitZ).Position, eye + carry.HeldOffset);
    standard.Update(world, 0f, Vector3.Zero, eye, -Vector3.UnitZ, false);
    standard.Update(world, 0f, Vector3.Zero, eye, -Vector3.UnitZ, true);
    Near(itemA.Velocity, new(0f, 0f, -7.5f));
    Near(itemA.AngularVelocity, Vector3.Zero);

    var noGravity = new ItemInstance(new ItemDefinition("floating", "Floating",
        bodySettings: new RigidBodySettings { Gravity = 0f }), new(0f, 3f, 0f));
    var gravity = new ItemInstance(new ItemDefinition("falling", "Falling"), new(0f, 3f, 0f));
    noGravity.UpdatePhysics(world, 0.1f);
    gravity.UpdatePhysics(world, 0.1f);
    Near(noGravity.Position, new(0f, 3f, 0f));
    Require(gravity.Velocity.Y < 0f, "A definition's custom gravity changed another definition");
});

Run("host line of sight, paused input sync and disabled interaction preserve slot semantics", () =>
{
    var world = new AnalyticWorld { BlockLineOfSight = true };
    var controller = new ItemInteractionController();
    var item = new ItemInstance(new ItemDefinition("pickup", "Pickup"), new(0.4f, 0f, 0f));
    controller.Add(item);
    Update(controller, world, true);
    Require(controller.HeldItem is null && controller.NearbyItem is null, "Host occlusion was ignored");
    world.BlockLineOfSight = false;
    controller.SyncInput(true);
    Update(controller, world, true);
    Require(controller.HeldItem is null, "Input held through a pause triggered pickup");
    Update(controller, world, false);
    Update(controller, world, true);
    Require(controller.HeldItem == item, "Pickup did not resume after releasing the action button");
    Update(controller, world, true, canInteract: false);
    Require(controller.HeldItem is null && !item.IsHeld, "Disabled interaction did not drop the held body");
    Near(item.Velocity, Vector3.Zero);
    Near(item.AngularVelocity, Vector3.Zero);
    Update(controller, world, true);
    Require(controller.HeldItem is null, "Returning while the action was held picked the dropped item back up");
    Update(controller, world, false);
    Update(controller, world, true);
    Require(controller.HeldItem == item, "The dropped body could not be picked up again");
});

Run("invalid definitions, identities and interaction settings are rejected", () =>
{
    Throws<ArgumentException>(() => new ItemDefinition(" ", "Item"));
    Throws<ArgumentException>(() => new ItemDefinition("id", " "));
    Throws<ArgumentOutOfRangeException>(() => new ItemDefinition("id", "Item", radius: -1f));
    Throws<ArgumentOutOfRangeException>(() => new ItemDefinition("id", "Item", height: float.NaN));
    Throws<ArgumentOutOfRangeException>(() => new ItemDefinition("id", "Item", halfExtents: Vector3.Zero));
    Throws<ArgumentOutOfRangeException>(() => new ItemDefinition("id", "Item", spawnAngularVelocity: new(float.NaN, 0f, 0f)));
    Throws<ArgumentOutOfRangeException>(() => new ItemDefinition("id", "Item", heldYaw: float.PositiveInfinity));
    Throws<ArgumentException>(() => new ItemDefinition("id", "Item", bodySettings: new RigidBodySettings { Gravity = -1f }));
    Throws<ArgumentException>(() => new ItemInstance(new ItemDefinition("id", "Item"), Vector3.Zero, instanceId: Guid.Empty));
    Throws<ArgumentOutOfRangeException>(() => new ItemInteractionSettings(pickupDistance: 0f));
    Throws<ArgumentOutOfRangeException>(() => new ItemInteractionSettings(throwSpeed: -1f));
    Throws<ArgumentOutOfRangeException>(() => new ItemCarrySettings(heldOffset: new(float.NaN, 0f, 0f)));
    Throws<ArgumentOutOfRangeException>(() => new ItemCarrySettings(throwSpin: new(0f, float.PositiveInfinity, 0f)));
    Throws<ArgumentOutOfRangeException>(() => new ItemInteractionSettings(releaseClearance: -1f));
});

Console.WriteLine($"{checks - failures}/{checks} standalone item library checks passed.");
return failures == 0 ? 0 : 1;

void Run(string name, Action check)
{
    checks++;
    try { check(); Console.WriteLine($"PASS {name}"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL {name}: {exception}"); }
}

static void Update(ItemInteractionController controller, AnalyticWorld world, bool actionDown,
    float seconds = 0f, bool canInteract = true) =>
    controller.Update(world, seconds, Vector3.Zero, new(0f, 1.6f, 0f), -Vector3.UnitZ, actionDown, canInteract);

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Near(Vector3 actual, Vector3 expected)
{
    Require(float.IsFinite(actual.X) && float.IsFinite(actual.Y) && float.IsFinite(actual.Z) &&
        Vector3.Distance(actual, expected) <= 0.0002f, $"Expected {expected}, got {actual}");
}

static void SameRotation(Quaternion actual, Quaternion expected) =>
    Require(MathF.Abs(Quaternion.Dot(actual, expected)) >= 0.99999f, $"Expected rotation {expected}, got {actual}");

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}");
}

static string FindRepositoryRoot()
{
    for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Libraries", "Ars3D.Items", "Ars3D.Items.csproj")))
            return directory.FullName;
    }
    throw new InvalidOperationException("The test executable must run from this repository's build output.");
}

// An infinite analytic plane demonstrates the host contract without MonoGame or a level mesh.
internal sealed class AnalyticWorld : ICollisionWorld
{
    public const float Skin = 0.001f;
    private const float ContactMargin = 0.0004f;
    public float? FloorHeight { get; set; }
    public bool BlockLineOfSight { get; set; }
    public (Vector3 Source, Vector3 Destination, float Radius)? LastPlacement { get; private set; }

    public bool HasLineOfSight(Vector3 eyePosition, Vector3 target) => !BlockLineOfSight;

    public Vector3 ResolveSpherePlacement(Vector3 sourceCenter, Vector3 desiredCenter, float radius)
    {
        LastPlacement = (sourceCenter, desiredCenter, radius);
        if (FloorHeight is { } floor)
            sourceCenter.Y = MathF.Max(sourceCenter.Y, floor + radius + Skin);
        Vector3 movement = desiredCenter - sourceCenter;
        return SweepSphere(sourceCenter, movement, radius, movement, out SweepHit hit)
            ? sourceCenter + movement * hit.Fraction
            : desiredCenter;
    }

    public bool SweepSphere(Vector3 center, Vector3 movement, float radius,
        Vector3 contactVelocity, out SweepHit hit)
    {
        hit = default;
        if (FloorHeight is not { } floor || movement.Y >= -0.0000001f)
            return false;
        float gap = center.Y - floor - radius - Skin;
        if (gap <= ContactMargin && contactVelocity.Y >= -0.002f && movement.Y >= -Skin * 0.5f)
            return false;
        float fraction = MathF.Max(0f, gap / -movement.Y);
        if (fraction > 1f)
            return false;
        Vector3 atHit = center + movement * fraction;
        hit = new(fraction, new(new(atHit.X, floor, atHit.Z), Vector3.UnitY,
            radius + Skin - (atHit.Y - floor)));
        return true;
    }

    public void GetSphereContacts(Vector3 center, float radius, List<BodyContact> contacts)
    {
        if (FloorHeight is not { } floor)
            return;
        float penetration = radius + Skin - (center.Y - floor);
        if (penetration >= -ContactMargin)
            contacts.Add(new(new(center.X, floor, center.Z), Vector3.UnitY, penetration));
    }
}
