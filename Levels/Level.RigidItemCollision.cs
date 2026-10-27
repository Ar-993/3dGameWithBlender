using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace _3DLight;

internal readonly record struct RigidItemContact(
    Vector3 Point,
    Vector3 Normal,
    float Penetration);

internal readonly record struct RigidItemSweepHit(
    float Fraction,
    RigidItemContact Contact);

public partial class Level
{
    private Items.LevelItemWorld? itemWorld;
    internal Items.LevelItemWorld ItemWorld => itemWorld ??= new Items.LevelItemWorld(this);
    // Preserve the same support manifold across sub-millimeter rocking and
    // normal-velocity jitter; collision casts still stop at the exact skin.
    private const float RigidContactMargin = 0.002f;
    private readonly List<TriangleCollider> rigidSweepCandidates = [];
    private readonly List<(CapsuleCollision.Contact Contact, TriangleCollider Triangle)> rigidSphereContacts = [];

    internal bool SweepRigidItemSphere(
        Vector3 center,
        Vector3 movement,
        float radius,
        Vector3 contactVelocity,
        out RigidItemSweepHit result)
    {
        CapsuleShape sphere = new(center - Vector3.Up * radius, radius, 2f * radius);
        Vector3 extent = new(radius + CapsuleCollision.SkinWidth + RigidContactMargin);
        BoundingBox sweptBounds = new(
            Vector3.Min(center, center + movement) - extent,
            Vector3.Max(center, center + movement) + extent);
        rigidSweepCandidates.Clear();
        rigidSphereContacts.Clear();
        foreach (TriangleCollider triangle in GetCollisionCandidates(sweptBounds))
        {
            CapsuleCollision.Contact contact = CapsuleCollision.GetContact(sphere, triangle);
            rigidSweepCandidates.Add(triangle);
            if (contact.Penetration >= -RigidContactMargin)
                rigidSphereContacts.Add((contact, triangle));
        }
        for (int index = rigidSweepCandidates.Count - 1; index >= 0; index--)
        {
            TriangleCollider triangle = rigidSweepCandidates[index];
            CapsuleCollision.Contact contact = CapsuleCollision.GetContact(sphere, triangle);
            // An already solved support contact may have a tiny inward chord
            // from rotation, even when its instantaneous velocity is tangent.
            // Allow that numerical motion, then recover the very small overlap
            // at the end of the bounded angular step. Other triangles still cast.
            if (IsHiddenRigidEdge(contact, triangle) ||
                (contact.Penetration >= -RigidContactMargin &&
                Vector3.Dot(contactVelocity, contact.Normal) >= -0.002f &&
                Vector3.Dot(movement, contact.Normal) >= -CapsuleCollision.SkinWidth * 0.5f))
            {
                rigidSweepCandidates.RemoveAt(index);
            }
        }

        if (!CapsuleCollision.Cast(sphere, movement, rigidSweepCandidates, out var hit))
        {
            result = default;
            return false;
        }

        CapsuleCollision.Contact atHit = CapsuleCollision.GetContact(
            sphere with { Feet = sphere.Feet + movement * hit.Fraction }, hit.Triangle);
        result = new(hit.Fraction, new(atHit.SurfacePoint, hit.Normal, atHit.Penetration));
        return true;
    }

    internal void GetRigidItemSphereContacts(
        Vector3 center,
        float radius,
        List<RigidItemContact> contacts)
    {
        CapsuleShape sphere = new(center - Vector3.Up * radius, radius, 2f * radius);
        Vector3 extent = new(radius + CapsuleCollision.SkinWidth + RigidContactMargin);
        rigidSphereContacts.Clear();
        foreach (TriangleCollider triangle in GetCollisionCandidates(new(center - extent, center + extent)))
        {
            CapsuleCollision.Contact contact = CapsuleCollision.GetContact(sphere, triangle);
            if (contact.Penetration < -RigidContactMargin)
                continue;
            rigidSphereContacts.Add((contact, triangle));
        }

        foreach (var candidate in rigidSphereContacts)
        {
            CapsuleCollision.Contact contact = candidate.Contact;
            // The diagonal between coplanar floor triangles is not a physical
            // ridge. Its farther edge contact must not torque a sphere that
            // already touches the nearer face of the same continuous plane.
            if (IsHiddenRigidEdge(contact, candidate.Triangle))
                continue;

            bool duplicate = false;
            foreach (RigidItemContact previous in contacts)
            {
                if (Vector3.DistanceSquared(previous.Point, contact.SurfacePoint) < 0.00000001f &&
                    Vector3.Dot(previous.Normal, contact.Normal) > 0.999f)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
                contacts.Add(new(contact.SurfacePoint, contact.Normal, contact.Penetration));
        }
    }

    private bool IsHiddenRigidEdge(CapsuleCollision.Contact contact, TriangleCollider triangle)
    {
        if (contact.Penetration < -RigidContactMargin ||
            MathF.Abs(Vector3.Dot(contact.Normal, triangle.Normal)) >= 0.9999f)
            return false;

        foreach (var other in rigidSphereContacts)
        {
            if (MathF.Abs(Vector3.Dot(other.Contact.Normal, other.Triangle.Normal)) >= 0.9999f &&
                MathF.Abs(Vector3.Dot(triangle.Normal, other.Triangle.Normal)) >= 0.9999f &&
                MathF.Abs(Vector3.Dot(triangle.A - other.Triangle.A, triangle.Normal)) < 0.0001f &&
                other.Contact.Distance <= contact.Distance + 0.00001f)
                return true;
        }
        return false;
    }
}
