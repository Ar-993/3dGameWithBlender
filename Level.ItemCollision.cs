using System;
using Microsoft.Xna.Framework;

namespace _3DLight;

public partial class Level
{
    // Items use the same swept triangle contacts as the character. Sweeping the
    // complete capsule also catches thin walls/floors during a fast throw.
    internal Vector3 MoveItem(
        Vector3 position,
        float radius,
        float height,
        float seconds,
        ref Vector3 velocity,
        out bool supported)
    {
        CapsuleShape capsule = RecoverPenetration(new(position, radius, height));
        supported = false;
        float remainingTime = seconds;

        for (int iteration = 0;
             iteration < MaximumSlideIterations && remainingTime > 0.000001f;
             iteration++)
        {
            Vector3 movement = velocity * remainingTime;
            if (movement.LengthSquared() < 0.0000000001f)
                break;

            if (!CastCapsule(capsule, movement, out var hit))
            {
                capsule = capsule with { Feet = capsule.Feet + movement };
                break;
            }

            capsule = capsule with
            {
                Feet = capsule.Feet + movement * hit.Fraction
            };
            remainingTime *= 1f - hit.Fraction;

            float normalSpeed = Vector3.Dot(velocity, hit.Normal);
            if (normalSpeed < 0f)
            {
                Vector3 tangent = velocity - hit.Normal * normalSpeed;
                // Low impacts settle instead of bouncing forever. Friction is
                // time based so the resting/sliding speed does not depend on FPS.
                bool floor = hit.Normal.Y > 0.45f;
                if (floor)
                    tangent *= MathF.Exp(-8f * seconds);

                float bounce = normalSpeed < -1.6f ? -normalSpeed * 0.16f : 0f;
                velocity = tangent + hit.Normal * bounce;
                supported |= floor && bounce == 0f;
            }
        }

        if (supported && velocity.LengthSquared() < 0.008f)
            velocity = Vector3.Zero;

        return capsule.Feet;
    }

    internal Vector3 ResolveItemReleasePosition(
        Vector3 source,
        Vector3 destination,
        float radius,
        float height)
    {
        CapsuleShape capsule = RecoverPenetration(new(source, radius, height));
        Vector3 movement = destination - capsule.Feet;
        return CastCapsule(capsule, movement, out var hit)
            ? capsule.Feet + movement * hit.Fraction
            : destination;
    }

    internal bool HasItemLineOfSight(Vector3 eyePosition, Vector3 itemCenter)
    {
        const float probeRadius = 0.005f;
        CapsuleShape probe = new(
            eyePosition - Vector3.Up * probeRadius,
            probeRadius,
            2f * probeRadius);
        return !CastCapsule(probe, itemCenter - eyePosition, out _);
    }
}
