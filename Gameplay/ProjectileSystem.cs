using _3DLight.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Gameplay;

internal sealed class ProjectileSystem : IDisposable
{
    private readonly GraphicsDevice graphics;
    private readonly SceneLighting lighting;
    private readonly CompiledModel gunModel;
    private readonly CompiledModel bulletModel;
    private readonly ShootingCube shootingCube;
    private readonly List<Bullet> activeBullets = new();
    private bool disposed;

    public ProjectileSystem(GraphicsDevice graphics, ContentManager content, SceneLighting lighting)
    {
        this.graphics = graphics;
        this.lighting = lighting;

        CompiledModel? loadedGun = null;
        CompiledModel? loadedBullet = null;
        ShootingCube? loadedCube = null;
        try
        {
            Texture2D fallbackTexture = content.Load<Texture2D>(
                "Assets/Gun/T_PRP_Pistol_Colt_A_BaseColor");
            Effect toonEffect = content.Load<Effect>("ToonShader");
            loadedGun = ModelAssetLoader.Load(
                graphics, "gun", fallbackTexture, toonEffect, textureFolderName: "Gun");
            loadedBullet = ModelAssetLoader.Load(
                graphics, "bullet", fallbackTexture, toonEffect, textureFolderName: "Gun");
            loadedCube = new ShootingCube(
                graphics,
                position: new Vector3(70f, 2f, 5f),
                size: new Vector3(1f, 1f, 1f),
                gunModel: loadedGun,
                lighting: lighting,
                fireRateSeconds: 1.5f);

            gunModel = loadedGun;
            bulletModel = loadedBullet;
            shootingCube = loadedCube;
        }
        catch
        {
            loadedCube?.Dispose();
            loadedBullet?.Dispose();
            loadedGun?.Dispose();
            throw;
        }
    }

    public void Update(float seconds, Player player)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        shootingCube.Update(seconds, player.Position);
        if (shootingCube.ShouldSpawnBullet)
        {
            Vector3 spawnPosition = shootingCube.Position + shootingCube.AimDirection * 1.2f;
            activeBullets.Add(new Bullet(
                graphics,
                spawnPosition,
                shootingCube.AimDirection,
                bulletModel: bulletModel,
                lighting: lighting,
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
                bullet.Dispose();
                activeBullets.RemoveAt(i);
                continue;
            }

            if (bullet.IsDead)
            {
                bullet.Dispose();
                activeBullets.RemoveAt(i);
            }
        }
    }

    public void Draw(Matrix view, Matrix projection)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        shootingCube.Draw(view, projection);
        foreach (Bullet bullet in activeBullets)
            bullet.Draw(view, projection);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        foreach (Bullet bullet in activeBullets)
            bullet.Dispose();
        activeBullets.Clear();
        shootingCube.Dispose();
        bulletModel.Dispose();
        gunModel.Dispose();
    }
}
