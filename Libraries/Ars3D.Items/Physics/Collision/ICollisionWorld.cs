using System.Collections.Generic;
using System.Numerics;

namespace Ars3D.Items.Physics;

/// <summary>Geometry queries supplied by the host game. No renderer is required.</summary>
public interface ICollisionWorld
{
    bool SweepSphere(Vector3 center, Vector3 movement, float radius,
        Vector3 contactVelocity, out SweepHit hit);

    // Append contacts to the supplied list; the body gathers several spheres
    // into one manifold. The host handles contact skin and internal mesh seams.
    void GetSphereContacts(Vector3 center, float radius, List<BodyContact> contacts);

    bool HasLineOfSight(Vector3 eyePosition, Vector3 target);

    // Recover the source if necessary, then sweep the entire sphere to its destination.
    Vector3 ResolveSpherePlacement(Vector3 sourceCenter, Vector3 desiredCenter, float radius);
}

/// <summary>A world-space contact. Positive penetration requires separation.</summary>
public readonly record struct BodyContact(Vector3 Point, Vector3 Normal, float Penetration);

/// <summary>The first contact along a sphere movement, with Fraction in [0, 1].</summary>
public readonly record struct SweepHit(float Fraction, BodyContact Contact);
