using Microsoft.Xna.Framework;

namespace _3DLight;

internal sealed class ShootingCube
{
    private readonly AttackComponent attack = new(
        damage: 15,
        range: 20f,
        hitTimeNormalized: 0.3f);

    public Vector3 Position { get; }
    public Vector3 Size { get; }
    public BoundingBox Bounds { get; }

    public bool ShouldSpawnBullet => attack.ShouldDealDamage;
    public Vector3 AimDirection { get; private set; } = Vector3.Forward;

    public ShootingCube(Vector3 position, Vector3 size, float fireRateSeconds = 2f)
    {
        Position = position;
        Size = size;

        attack.SetDuration(fireRateSeconds);

        Vector3 halfSize = size / 2f;
        Bounds = new BoundingBox(position - halfSize, position + halfSize);
    }

    public void Update(float deltaTime, Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - Position;
        float distance = toTarget.Length();

        if (distance > 0.001f)
        {
            AimDirection = Vector3.Normalize(toTarget);
        }

        if (distance <= attack.Range)
        {
            if (!attack.IsAttacking)
            {
                attack.TryStart();
            }
        }

        attack.Update(deltaTime);
    }
}
