using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight;

internal sealed class ShootingCube : IDisposable
{
    private static readonly Matrix ModelOrientation =
        Matrix.CreateRotationY(-MathHelper.PiOver2);

    private readonly GraphicsDevice graphicsDevice;
    private readonly CompiledModel gunModel;
    private readonly SceneLighting lighting;

    private readonly AttackComponent attack = new(
        damage: 15,
        range: 20f,
        hitTimeNormalized: 0.3f);

    public Vector3 Position { get; }
    public Vector3 Size { get; }
    public BoundingBox Bounds { get; }

    public bool ShouldSpawnBullet => attack.ShouldDealDamage;
    public Vector3 AimDirection { get; private set; } = Vector3.Forward;

    public ShootingCube(GraphicsDevice graphicsDevice, Vector3 position, Vector3 size, CompiledModel gunModel, SceneLighting lighting, float fireRateSeconds = 2.0f)
    {
        this.graphicsDevice = graphicsDevice;
        this.gunModel = gunModel;
        this.lighting = lighting;
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

    public void Draw(Matrix view, Matrix projection)
    {
        Matrix rotation = Matrix.CreateLookAt(Vector3.Zero, AimDirection, Vector3.Up);
        rotation = Matrix.Invert(rotation);

        Matrix world = Matrix.CreateScale(Size) * ModelOrientation * rotation * Matrix.CreateTranslation(Position);

        gunModel.Draw(world, view, projection, lighting);
    }

    public void Dispose()
    {
        
    }
}
