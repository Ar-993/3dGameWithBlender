using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Ars3D.Items.Physics
{
    public readonly record struct ColliderCapsule(Vector3 Start, Vector3 End, float Radius);

    public sealed class ItemCollider
    {
        public IReadOnlyList<ColliderCapsule> Capsules { get; }

        public ItemCollider(IEnumerable<ColliderCapsule> capsules)
        {
            ArgumentNullException.ThrowIfNull(capsules);
            ColliderCapsule[] parts = capsules.ToArray();
            if (parts.Length == 0)
                throw new ArgumentException("A collider needs at least one capsule.", nameof(capsules));
            foreach (ColliderCapsule part in parts)
                if (!RigidBody.Finite(part.Start) || !RigidBody.Finite(part.End) ||
                    !float.IsFinite(part.Radius) || part.Radius <= 0f)
                    throw new ArgumentException("Capsule endpoints must be finite and radius positive.", nameof(capsules));
            Capsules = Array.AsReadOnly(parts);
        }
    }
}
