using System.Numerics;
using Ars3D.Items.Physics;

namespace Ars3D.Items;

public interface IItemInteractionWorld : ICollisionWorld
{
    bool HasLineOfSight(Vector3 eyePosition, Vector3 target);

    // Recover the source if necessary, then sweep the entire sphere to its destination.
    Vector3 ResolveSpherePlacement(Vector3 sourceCenter, Vector3 desiredCenter, float radius);
}
