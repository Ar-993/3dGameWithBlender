using System;
using System.Collections.Generic;
using System.Numerics;

namespace Ars3D.Items.Physics;

// Sequential contact impulses, accumulated within one solve. Later contacts
// can reduce an earlier impulse without ever pulling the body into a surface.
internal sealed class ContactSolver(RigidBodySettings settings)
{
    private float[] normalImpulses = [];
    private float[] restitutionSpeeds = [];
    private Vector3[] frictionImpulses = [];

    public void Solve(BodyMotion motion, List<BodyContact> contacts, bool allowBounce)
    {
        int count = contacts.Count;
        if (count == 0)
            return;
        PrepareBuffers(motion, contacts, allowBounce);
        for (int iteration = 0; iteration < settings.ContactIterations; iteration++)
        for (int index = 0; index < count; index++)
        {
            BodyContact contact = contacts[index];
            Vector3 lever = contact.Point - motion.Center;
            float closingSpeed = Vector3.Dot(motion.PointVelocity(lever), contact.Normal);
            float normalDelta = (restitutionSpeeds[index] - closingSpeed) /
                motion.ImpulseDenominator(lever, contact.Normal);
            float previousNormal = normalImpulses[index];
            float accumulatedNormal = MathF.Max(previousNormal + normalDelta, 0f);
            normalImpulses[index] = accumulatedNormal;
            motion.ApplyImpulse(lever, contact.Normal * (accumulatedNormal - previousNormal));

            Vector3 pointVelocity = motion.PointVelocity(lever);
            Vector3 tangent = pointVelocity - contact.Normal * Vector3.Dot(pointVelocity, contact.Normal);
            float tangentSpeed = tangent.Length();
            Vector3 previousFriction = frictionImpulses[index];
            Vector3 accumulatedFriction = previousFriction;
            if (tangentSpeed > 0.000001f)
            {
                tangent /= tangentSpeed;
                accumulatedFriction -= tangent * (tangentSpeed / motion.ImpulseDenominator(lever, tangent));
            }
            accumulatedFriction -= contact.Normal * Vector3.Dot(accumulatedFriction, contact.Normal);
            float maximumFriction = settings.Friction * accumulatedNormal;
            float frictionMagnitude = accumulatedFriction.Length();
            if (frictionMagnitude > maximumFriction)
                accumulatedFriction *= maximumFriction / frictionMagnitude;
            frictionImpulses[index] = accumulatedFriction;
            motion.ApplyImpulse(lever, accumulatedFriction - previousFriction);
        }
    }

    private void PrepareBuffers(BodyMotion motion, List<BodyContact> contacts, bool allowBounce)
    {
        int count = contacts.Count;
        if (normalImpulses.Length < count)
        {
            int capacity = Math.Max(count, Math.Max(16, normalImpulses.Length * 2));
            Array.Resize(ref normalImpulses, capacity);
            Array.Resize(ref restitutionSpeeds, capacity);
            Array.Resize(ref frictionImpulses, capacity);
        }
        Array.Clear(normalImpulses, 0, count);
        Array.Clear(frictionImpulses, 0, count);
        for (int index = 0; index < count; index++)
        {
            BodyContact contact = contacts[index];
            float closingSpeed = Vector3.Dot(motion.PointVelocity(contact.Point - motion.Center), contact.Normal);
            restitutionSpeeds[index] = allowBounce && closingSpeed < -settings.BounceSpeed
                ? -closingSpeed * settings.Restitution : 0f;
        }
    }
}
