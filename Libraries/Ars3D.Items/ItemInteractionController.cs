using System.Collections.ObjectModel;
using System.Numerics;
using Ars3D.Items.Physics;

namespace Ars3D.Items;

// The caller supplies its chosen action button as a Boolean. No input framework is required.
public sealed class ItemInteractionController
{
    private readonly List<ItemInstance> items = [];
    private readonly Dictionary<Guid, ItemInstance> itemsById = [];
    private readonly ReadOnlyCollection<ItemInstance> readOnlyItems;
    private bool wasActionDown;

    public IReadOnlyList<ItemInstance> Items => readOnlyItems;
    public ItemInteractionSettings Settings { get; }
    public ItemInstance? HeldItem { get; private set; }
    public ItemInstance? NearbyItem { get; private set; }

    public ItemInteractionController(ItemInteractionSettings? settings = null)
    {
        Settings = settings ?? new ItemInteractionSettings();
        readOnlyItems = items.AsReadOnly();
    }

    public void Add(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (itemsById.TryGetValue(item.InstanceId, out ItemInstance? existing))
        {
            if (ReferenceEquals(existing, item))
                return;
            throw new ArgumentException("An item with this instance ID is already registered.", nameof(item));
        }
        itemsById.Add(item.InstanceId, item);
        items.Add(item);
    }

    // Call while paused or while a menu owns input to consume an already-held button.
    public void SyncInput(bool actionDown)
    {
        wasActionDown = actionDown;
        NearbyItem = null;
    }

    public void Update(
        ICollisionWorld world,
        float seconds,
        Vector3 playerFeet,
        Vector3 eyePosition,
        Vector3 forward,
        bool actionDown,
        bool canInteract = true)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (ItemInstance item in items)
            item.UpdatePhysics(world, seconds);

        bool pressed = actionDown && !wasActionDown;
        wasActionDown = actionDown;
        NearbyItem = null;

        if (!canInteract)
        {
            ReleaseHeldItem(world, eyePosition, forward, throwItem: false);
            return;
        }

        if (HeldItem is null)
            NearbyItem = FindNearest(world, playerFeet, eyePosition);
        if (!pressed)
            return;

        if (HeldItem is not null)
            ReleaseHeldItem(world, eyePosition, forward, throwItem: true);
        else if (NearbyItem is not null && TryPickUp(NearbyItem))
            NearbyItem = null;
    }

    // Use this same pose to draw the held model and to release it without a visual jump.
    public ItemPose GetHeldPose(Vector3 eyePosition, Vector3 forward)
    {
        ItemInstance item = HeldItem ?? throw new InvalidOperationException("The inventory is empty.");
        return CreatePose(item, eyePosition, CreateFrame(forward));
    }

    public bool TryPickUp(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (HeldItem is not null || item.IsHeld)
            return false;
        item.PickUp();
        HeldItem = item;
        return true;
    }

    // Поиск и выпуск предмета.
    private ItemInstance? FindNearest(ICollisionWorld world, Vector3 playerFeet, Vector3 eyePosition)
    {
        float reach = Settings.PickupDistance;
        ItemInstance? nearest = null;
        float nearestDistance = reach;
        foreach (ItemInstance item in items)
        {
            if (item.IsHeld)
                continue;
            float distance = Vector3.Distance(eyePosition, item.Bounds.ClosestPoint(eyePosition));
            if (distance > nearestDistance || item.Center.Y < playerFeet.Y - reach ||
                !world.HasLineOfSight(eyePosition, item.Center))
            {
                continue;
            }

            nearest = item;
            nearestDistance = distance;
        }
        return nearest;
    }

    private void ReleaseHeldItem(ICollisionWorld world, Vector3 eyePosition, Vector3 forward, bool throwItem)
    {
        ItemInstance? item = HeldItem;
        if (item is null)
            return;
        Frame frame = CreateFrame(forward);
        ItemPose heldPose = CreatePose(item, eyePosition, frame);
        ItemCarrySettings carry = item.Definition.Interaction;
        Vector3 centerOffset = Vector3.Transform(item.LocalCenter, heldPose.Orientation);
        Vector3 sourceCenter = eyePosition - item.LocalCenter;
        Vector3 desiredCenter = heldPose.Position + centerOffset;
        float radius = item.BoundingRadius + Settings.ReleaseClearance;
        Vector3 releaseCenter = world.ResolveSpherePlacement(sourceCenter, desiredCenter, radius);
        var releasePose = heldPose with { Position = releaseCenter - centerOffset };
        Vector3 velocity = throwItem
            ? frame.Forward * Settings.ThrowSpeed + Vector3.UnitY * carry.ThrowLift
            : Vector3.Zero;
        Vector3 spin = throwItem
            ? frame.Right * carry.ThrowSpin.X + frame.Up * carry.ThrowSpin.Y + frame.Forward * carry.ThrowSpin.Z
            : Vector3.Zero;
        item.Release(releasePose.Position, velocity, releasePose.Orientation, spin);
        HeldItem = null;
    }

    // Положение предмета относительно камеры, общее для удержания и броска.
    private readonly record struct Frame(Vector3 Forward, Vector3 Right, Vector3 Up);

    private static Frame CreateFrame(Vector3 forward)
    {
        forward = NormalizeForward(forward);
        Vector3 right = CalculateRight(forward);
        Vector3 up = Vector3.Cross(right, forward);
        return new(forward, right, up);
    }

    private static ItemPose CreatePose(
        ItemInstance item,
        Vector3 eyePosition,
        Frame frame)
    {
        Vector3 offset = item.Definition.Interaction.HeldOffset;
        Vector3 position = CalculatePosition(eyePosition, frame, offset);
        Quaternion orientation = CalculateOrientation(frame, item.Definition.HeldYaw);
        return new(position, orientation);
    }

    private static Vector3 NormalizeForward(Vector3 forward)
    {
        float lengthSquared = forward.LengthSquared();
        if (Finite(forward) && float.IsFinite(lengthSquared) && lengthSquared > 0.000001f)
            return Vector3.Normalize(forward);

        return -Vector3.UnitZ;
    }

    private static Vector3 CalculateRight(Vector3 forward)
    {
        Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
        if (right.LengthSquared() > 0.000001f)
            return Vector3.Normalize(right);

        return Vector3.UnitX;
    }

    private static Vector3 CalculatePosition(Vector3 eyePosition, Frame frame, Vector3 offset)
    {
        Vector3 horizontalOffset = frame.Right * offset.X;
        Vector3 verticalOffset = frame.Up * offset.Y;
        Vector3 depthOffset = frame.Forward * offset.Z;
        return eyePosition + horizontalOffset + verticalOffset - depthOffset;
    }

    private static Quaternion CalculateOrientation(Frame frame, float heldYaw)
    {
        Matrix4x4 localRotation = Matrix4x4.CreateRotationY(heldYaw);
        Matrix4x4 cameraRotation = CreateCameraRotation(frame);
        Quaternion orientation = Quaternion.CreateFromRotationMatrix(localRotation * cameraRotation);
        return Quaternion.Normalize(orientation);
    }

    private static Matrix4x4 CreateCameraRotation(Frame frame) => new(
        frame.Right.X, frame.Right.Y, frame.Right.Z, 0f,
        frame.Up.X, frame.Up.Y, frame.Up.Z, 0f,
        -frame.Forward.X, -frame.Forward.Y, -frame.Forward.Z, 0f,
        0f, 0f, 0f, 1f);

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

public sealed class ItemInteractionSettings
{
    public float PickupDistance { get; }
    public float ThrowSpeed { get; }
    public float ReleaseClearance { get; }

    public ItemInteractionSettings(
        float pickupDistance = 2f,
        float throwSpeed = 7.5f,
        float releaseClearance = 0.001f)
    {
        if (!float.IsFinite(pickupDistance) || pickupDistance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(pickupDistance));
        if (!float.IsFinite(throwSpeed) || throwSpeed < 0f)
            throw new ArgumentOutOfRangeException(nameof(throwSpeed));
        if (!float.IsFinite(releaseClearance) || releaseClearance < 0f)
            throw new ArgumentOutOfRangeException(nameof(releaseClearance));

        PickupDistance = pickupDistance;
        ThrowSpeed = throwSpeed;
        ReleaseClearance = releaseClearance;
    }

}
