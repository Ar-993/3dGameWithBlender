using System.Numerics;

namespace Ars3D.Items.Physics;

/// <summary>A world-space contact. Positive penetration requires separation.</summary>
public readonly record struct BodyContact(Vector3 Point, Vector3 Normal, float Penetration);

/// <summary>The first contact along a sphere movement, with Fraction in [0, 1].</summary>
public readonly record struct SweepHit(float Fraction, BodyContact Contact);
