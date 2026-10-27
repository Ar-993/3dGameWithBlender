using _3DLight;
using _3DLight.Gameplay;
using Microsoft.Xna.Framework;

internal static class ProjectileChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("bullets move at the configured speed and expire without graphics", () =>
        {
            Bullet bullet = new(new Vector3(1f, 2f, 3f), Vector3.Right * 4f, damage: 7, speed: 8f);
            bullet.Update(0.25f);

            Near(bullet.Position, new Vector3(3f, 2f, 3f));
            Near(bullet.Direction, Vector3.Right);
            Near(bullet.Bounds.Center, bullet.Position);
            Require(!bullet.IsDead, "The bullet expired before its lifetime elapsed");

            bullet.Update(2.75f);
            Require(bullet.IsDead, "The bullet remained alive after its lifetime elapsed");
        });

        run("projectiles require a target in range and fire once at the attack hit time", () =>
        {
            ProjectileSystem projectiles = CreateSystem();
            Player player = CreatePlayer(Vector3.Right * 30f);
            projectiles.Update(1f, player);
            Require(projectiles.Bullets.Count == 0, "The shooter fired at a target outside its range");

            player.SpawnAt(Vector3.Right * 10f);
            projectiles.Update(0.25f, player);
            Require(projectiles.Bullets.Count == 0, "The shooter fired before the attack hit time");

            projectiles.Update(0.125f, player);
            Require(projectiles.Bullets.Count == 1, "The attack did not emit exactly one bullet");
            Bullet bullet = projectiles.Bullets[0];
            Near(bullet.Direction, Vector3.Right);
            Near(bullet.Position, Vector3.Right * 2.7f);

            projectiles.Update(0.125f, player);
            Require(projectiles.Bullets.Count == 1 && ReferenceEquals(projectiles.Bullets[0], bullet),
                "One attack emitted another bullet after its hit time");
        });

        run("a projectile hit damages the player once and removes the bullet", () =>
        {
            ProjectileSystem projectiles = CreateSystem();
            Player player = CreatePlayer(Vector3.Right * 6f);
            projectiles.Update(0.25f, player);
            projectiles.Update(0.125f, player);
            Require(projectiles.Bullets.Count == 1, "No bullet was available to hit the player");

            projectiles.Update(0.25f, player);
            Require(player.Health == 85 && projectiles.Bullets.Count == 0,
                "The hit did not apply damage and remove the bullet in the same step");

            projectiles.Update(0.125f, player);
            Require(player.Health == 85 && projectiles.Bullets.Count == 0,
                "The removed bullet damaged the player again");
        });

        run("projectiles that miss are removed when their lifetime expires", () =>
        {
            ProjectileSystem projectiles = CreateSystem();
            Player player = CreatePlayer(Vector3.Right * 10f);
            projectiles.Update(0.5f, player);
            Require(projectiles.Bullets.Count == 1, "The shooter did not create a bullet");
            Bullet bullet = projectiles.Bullets[0];

            player.SpawnAt(Vector3.Right * 1000f);
            projectiles.Update(2.5f, player);
            Require(bullet.IsDead && projectiles.Bullets.Count == 0,
                "An expired bullet remained in the active simulation");
            Require(player.Health == player.MaxHealth, "A missed projectile damaged the player");
        });
    }

    private static ProjectileSystem CreateSystem() => new(
        new ShootingCube(Vector3.Zero, Vector3.One, fireRateSeconds: 1f));

    private static Player CreatePlayer(Vector3 position) => new(
        new CharacterPhysicsComponent(position, 0.35f, 1.8f, gravity: -28f),
        new StatsComponent(maxHealth: 100),
        new AttackComponent(damage: 25, range: 1.6f, hitTimeNormalized: 0.45f),
        new PlayerDefinition());

    private static void Near(Vector3 actual, Vector3 expected) =>
        Require(Vector3.Distance(actual, expected) <= 0.0001f, $"Expected {expected}, got {actual}");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
