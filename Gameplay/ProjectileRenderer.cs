using _3DLight.Models;
using Ars3D.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Gameplay;

// Владеет моделями орудия и пули. Читает состояние системы, не изменяя симуляцию.
internal sealed class ProjectileRenderer : IDisposable
{
    private static readonly Matrix GunModelOrientation = Matrix.CreateRotationY(-MathHelper.PiOver2);
    private static readonly Matrix BulletModelOrientation = Matrix.CreateRotationX(-MathHelper.PiOver2);

    private readonly CompiledModel gunModel;
    private readonly CompiledModel bulletModel;
    private bool disposed;

    public ProjectileRenderer(GraphicsDevice graphicsDevice, ContentManager content)
    {
        Texture2D fallbackTexture = content.Load<Texture2D>("Assets/Gun/T_PRP_Pistol_Colt_A_BaseColor");
        Effect toonEffect = content.Load<Effect>("ToonShader");
        gunModel = ModelAssetLoader.Load(
            graphicsDevice, ModelPaths.ModelFile("gun"), fallbackTexture, toonEffect,
            ModelPaths.TextureDirectory("Gun"));
        try
        {
            bulletModel = ModelAssetLoader.Load(
                graphicsDevice, ModelPaths.ModelFile("bullet"), fallbackTexture, toonEffect,
                ModelPaths.TextureDirectory("Gun"));
        }
        catch
        {
            gunModel.Dispose();
            throw;
        }
    }

    public void Draw(ProjectileSystem projectiles, Matrix view, Matrix projection, SceneLighting lighting)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        ShootingCube shooter = projectiles.Shooter;
        Matrix gunWorld = Matrix.CreateScale(shooter.Size) * GunModelOrientation *
            AimRotation(shooter.AimDirection) * Matrix.CreateTranslation(shooter.Position);
        gunModel.Draw(gunWorld, view, projection, lighting);

        foreach (Bullet bullet in projectiles.Bullets)
        {
            Matrix bulletWorld = BulletModelOrientation * AimRotation(bullet.Direction) *
                Matrix.CreateTranslation(bullet.Position);
            bulletModel.Draw(bulletWorld, view, projection, lighting);
        }
    }

    private static Matrix AimRotation(Vector3 direction) =>
        Matrix.Invert(Matrix.CreateLookAt(Vector3.Zero, direction, Vector3.Up));

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        try { bulletModel.Dispose(); }
        finally { gunModel.Dispose(); }
    }
}
