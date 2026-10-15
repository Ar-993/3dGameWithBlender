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

    public void Solve(RigidBody body, List<BodyContact> contacts, bool allowBounce)
    {
        int count = contacts.Count;
        if (count == 0)
            return;
        PrepareBuffers(body, contacts, allowBounce);
        for (int iteration = 0; iteration < settings.ContactIterations; iteration++)
        for (int index = 0; index < count; index++)
        {
            BodyContact contact = contacts[index];
            Vector3 lever = contact.Point - body.Center;
            float closingSpeed = Vector3.Dot(body.PointVelocity(lever), contact.Normal);
            float normalDelta = (restitutionSpeeds[index] - closingSpeed) /
                body.ImpulseDenominator(lever, contact.Normal);
            float previousNormal = normalImpulses[index];
            float accumulatedNormal = MathF.Max(previousNormal + normalDelta, 0f);
            normalImpulses[index] = accumulatedNormal;
            body.ApplyImpulse(lever, contact.Normal * (accumulatedNormal - previousNormal));

            Vector3 pointVelocity = body.PointVelocity(lever);
            Vector3 tangent = pointVelocity - contact.Normal * Vector3.Dot(pointVelocity, contact.Normal);
            float tangentSpeed = tangent.Length();
            Vector3 previousFriction = frictionImpulses[index];
            Vector3 accumulatedFriction = previousFriction;
            if (tangentSpeed > 0.000001f)
            {
                tangent /= tangentSpeed;
                accumulatedFriction -= tangent * (tangentSpeed / body.ImpulseDenominator(lever, tangent));
            }
            accumulatedFriction -= contact.Normal * Vector3.Dot(accumulatedFriction, contact.Normal);
            float maximumFriction = settings.Friction * accumulatedNormal;
            float frictionMagnitude = accumulatedFriction.Length();
            if (frictionMagnitude > maximumFriction)
                accumulatedFriction *= maximumFriction / frictionMagnitude;
            frictionImpulses[index] = accumulatedFriction;
            body.ApplyImpulse(lever, accumulatedFriction - previousFriction);
        }
    }

    private void PrepareBuffers(RigidBody body, List<BodyContact> contacts, bool allowBounce)
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
            float closingSpeed = Vector3.Dot(body.PointVelocity(contact.Point - body.Center), contact.Normal);
            restitutionSpeeds[index] = allowBounce && closingSpeed < -settings.BounceSpeed
                ? -closingSpeed * settings.Restitution : 0f;
        }
    }
}
