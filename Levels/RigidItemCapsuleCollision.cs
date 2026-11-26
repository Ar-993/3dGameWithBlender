using Microsoft.Xna.Framework;

namespace _3DLight;

public partial class Level
{
    private readonly List<(TriangleCollider Triangle, CapsuleCollision.Contact Contact)> rigidCapsuleCandidates = [];

    internal bool SweepRigidItemCapsule(Vector3 start, Vector3 end, Vector3 startMovement,
        Vector3 endMovement, float radius, Vector3 startVelocity, Vector3 endVelocity,
        out RigidItemSweepHit result)
    {
        float maximumMovement = MathF.Max(startMovement.Length(), endMovement.Length());
        if (maximumMovement < 0.000001f)
        {
            result = default;
            return false;
        }

        var sweep = new CapsuleSweep(start, end, startMovement, endMovement, radius, startVelocity, endVelocity);
        GatherCapsuleSweepCandidates(GetCapsuleSweepBounds(sweep), start, end, radius);

        float timeTolerance = 0.00001f / maximumMovement;
        RigidItemSweepHit? firstHit = FindFirstCapsuleHit(sweep, timeTolerance);
        result = firstHit.GetValueOrDefault();
        return firstHit.HasValue;
    }

    private static BoundingBox GetCapsuleSweepBounds(CapsuleSweep sweep)
    {
        Vector3 finalStart = sweep.Start + sweep.StartMovement;
        Vector3 finalEnd = sweep.End + sweep.EndMovement;
        Vector3 min = Vector3.Min(Vector3.Min(sweep.Start, sweep.End), Vector3.Min(finalStart, finalEnd));
        Vector3 max = Vector3.Max(Vector3.Max(sweep.Start, sweep.End), Vector3.Max(finalStart, finalEnd));
        Vector3 padding = new(sweep.Radius + CapsuleCollision.SkinWidth + RigidContactMargin);
        return new BoundingBox(min - padding, max + padding);
    }

    private RigidItemSweepHit? FindFirstCapsuleHit(CapsuleSweep sweep, float timeTolerance)
    {
        RigidItemSweepHit? firstHit = null;
        foreach (var (triangle, initialContact) in rigidCapsuleCandidates)
        {
            if (IsHiddenRigidEdge(initialContact, triangle) || IsSeparatingCapsuleContact(sweep, initialContact))
                continue;

            float firstTime = firstHit?.Fraction ?? 1f;
            float castLimit = MathF.Min(1f, firstTime + timeTolerance);
            if (!CastMovingCapsuleTriangle(sweep, triangle, castLimit, out float time, out var contact))
                continue;

            if (firstHit is { } previous && !IsPreferredCapsuleHit(time, contact, previous, timeTolerance))
                continue;

            // При близких попаданиях сохраняем самое раннее время движения.
            float fraction = MathF.Min(firstTime, time);
            firstHit = new RigidItemSweepHit(fraction, new(contact.SurfacePoint, contact.Normal, contact.Penetration));
        }

        return firstHit;
    }

    private static bool IsPreferredCapsuleHit(float time, CapsuleCollision.Contact contact,
        RigidItemSweepHit previous, float tolerance)
    {
        // При почти одновременных попаданиях предпочитаем нормаль, направленную ближе к вертикали вверх.
        return time < previous.Fraction - tolerance ||
            (MathF.Abs(time - previous.Fraction) <= tolerance && contact.Normal.Y > previous.Contact.Normal.Y);
    }

    private void GatherCapsuleSweepCandidates(BoundingBox bounds, Vector3 start, Vector3 end, float radius)
    {
        rigidCapsuleCandidates.Clear();
        rigidSphereContacts.Clear();
        foreach (TriangleCollider triangle in GetCollisionCandidates(bounds))
        {
            var contact = CapsuleCollision.GetContact(start, end, radius, triangle);
            rigidCapsuleCandidates.Add((triangle, contact));
            if (contact.Penetration >= -RigidContactMargin)
                rigidSphereContacts.Add((contact, triangle));
        }
    }

    private static bool IsSeparatingCapsuleContact(CapsuleSweep sweep, CapsuleCollision.Contact contact)
    {
        if (contact.Penetration < -RigidContactMargin)
            return false;

        float amount = sweep.AmountAlongAxis(contact.AxisPoint);
        Vector3 velocity = Vector3.Lerp(sweep.StartVelocity, sweep.EndVelocity, amount);
        Vector3 movement = Vector3.Lerp(sweep.StartMovement, sweep.EndMovement, amount);
        return Vector3.Dot(velocity, contact.Normal) >= -0.002f &&
            Vector3.Dot(movement, contact.Normal) >= -CapsuleCollision.SkinWidth * 0.5f;
    }

    private static bool CastMovingCapsuleTriangle(CapsuleSweep sweep, TriangleCollider triangle,
        float maximumTime, out float time, out CapsuleCollision.Contact contact)
    {
        time = 0f;
        contact = default;
        for (int iteration = 0; iteration < 48; iteration++)
        {
            Vector3 start = sweep.Start + sweep.StartMovement * time;
            Vector3 end = sweep.End + sweep.EndMovement * time;
            contact = CapsuleCollision.GetContact(start, end, sweep.Radius, triangle);

            // Самый быстро приближающийся конец ограничивает скорость сближения всего отрезка.
            float closing = MathF.Max(-Vector3.Dot(sweep.StartMovement, contact.Normal),
                -Vector3.Dot(sweep.EndMovement, contact.Normal));
            if (closing <= 0.0000001f)
                return false;

            float gap = contact.Distance - sweep.Radius - CapsuleCollision.SkinWidth;
            if (gap <= 0.00001f)
                return true;

            float nextTime = time + gap / closing;
            if (nextTime > maximumTime)
                return false;
            if (nextTime <= time)
                return true;

            time = nextTime;
        }

        // При исчерпании итераций останавливаем тело на последней рассчитанной позиции.
        return true;
    }

    internal void GetRigidItemCapsuleContacts(Vector3 start, Vector3 end, float radius,
        List<RigidItemContact> contacts)
    {
        Vector3 extent = new(radius + CapsuleCollision.SkinWidth + RigidContactMargin);
        rigidSphereContacts.Clear();
        foreach (TriangleCollider triangle in GetCollisionCandidates(new(
            Vector3.Min(start, end) - extent, Vector3.Max(start, end) + extent)))
        {
            Add(CapsuleCollision.GetContact(start, end, radius, triangle), triangle);
            // Контакты обоих концов дают капсуле устойчивую опору на плоскости.
            Add(CapsuleCollision.GetContact(start, start, radius, triangle), triangle);
            Add(CapsuleCollision.GetContact(end, end, radius, triangle), triangle);
        }
        AppendVisibleRigidContacts(contacts);

        void Add(CapsuleCollision.Contact contact, TriangleCollider triangle)
        {
            if (contact.Penetration >= -RigidContactMargin)
                rigidSphereContacts.Add((contact, triangle));
        }
    }

    private readonly record struct CapsuleSweep(
        Vector3 Start,
        Vector3 End,
        Vector3 StartMovement,
        Vector3 EndMovement,
        float Radius,
        Vector3 StartVelocity,
        Vector3 EndVelocity)
    {
        public float AmountAlongAxis(Vector3 point)
        {
            Vector3 axis = End - Start;
            float lengthSquared = axis.LengthSquared();
            if (lengthSquared <= 0.000000000001f)
                return 0f;

            return MathHelper.Clamp(Vector3.Dot(point - Start, axis) / lengthSquared, 0f, 1f);
        }
    }
}