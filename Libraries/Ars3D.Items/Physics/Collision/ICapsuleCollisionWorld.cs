using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Ars3D.Items.Physics.Collision
{
    internal interface ICapsuleCollisionWorld : ICollisionWorld
    {
        void GetCapsuleContacts(Vector3 start, Vector3 end, float radius, List<BodyContact> contacts);

        bool SweepCapsule(Vector3 start, Vector3 end, Vector3 startMovement, Vector3 endMovement,
            float radius, Vector3 startVelocity, Vector3 endVelocity, out SweepHit hit);
    }
}
