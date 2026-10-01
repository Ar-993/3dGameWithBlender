using Microsoft.Xna.Framework;

namespace _3DLight;

internal sealed class ShootingPumpkin : IDisposable
{
    private readonly CompiledModel model;
    private readonly AttackComponent attack = new(damage: 15, range: 20f, hitTimeNormalized: 0.3f);
    public Vector3 Position { get; }
    public bool ShouldSpawnBullet => attack.ShouldDealDamage;
    public Vector3 AimDirection { get; private set; } = Vector3.Forward;

    public ShootingPumpkin(CompiledModel model, Vector3 position, float fireRateSeconds = 1.5f)
    {
        this.model = model;
        Position = position;
        attack.SetDuration(fireRateSeconds);
    }

    public void Update(float deltaTime, Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - Position;
        float distance = toTarget.Length();
        if (distance > 0.001f) AimDirection = toTarget / distance;
        if (distance <= attack.Range && !attack.IsAttacking) attack.TryStart();
        attack.Update(deltaTime);
    }

    public void Draw(Matrix view, Matrix projection, SceneLighting lighting)
    {
        float yaw = MathF.Atan2(-AimDirection.X, -AimDirection.Z);
        model.Draw(Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(Position), view, projection, lighting);
    }

    public void Dispose() => model.Dispose();
}
