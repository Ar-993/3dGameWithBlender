using Microsoft.Xna.Framework;

namespace _3DLight.Gameplay;

// Только симуляция: стрельба, движение снарядов и попадания по игроку.
internal sealed class ProjectileSystem
{
    private readonly List<Bullet> activeBullets = new();

    public ShootingCube Shooter { get; }
    public IReadOnlyList<Bullet> Bullets { get; }

    public ProjectileSystem(ShootingCube shooter)
    {
        Shooter = shooter;
        Bullets = activeBullets.AsReadOnly();
    }

    public void Update(float seconds, Player player)
    {
        Shooter.Update(seconds, player.Position);
        if (Shooter.ShouldSpawnBullet)
        {
            Vector3 spawnPosition = Shooter.Position + Shooter.AimDirection * 1.2f;
            activeBullets.Add(new Bullet(
                spawnPosition,
                Shooter.AimDirection,
                damage: 15,
                speed: 12f));
        }

        for (int i = activeBullets.Count - 1; i >= 0; i--)
        {
            Bullet bullet = activeBullets[i];
            bullet.Update(seconds);

            float distanceToPlayer = Vector3.Distance(bullet.Position, player.Position);
            if (distanceToPlayer <= bullet.Bounds.Radius + player.CollisionRadius)
            {
                player.TakeDamage(bullet.Damage);
                activeBullets.RemoveAt(i);
                continue;
            }

            if (bullet.IsDead)
                activeBullets.RemoveAt(i);
        }
    }
}
