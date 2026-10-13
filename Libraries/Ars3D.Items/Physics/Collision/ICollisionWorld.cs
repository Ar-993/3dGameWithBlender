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
}
