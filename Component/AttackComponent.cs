using System;

namespace _3DLight;

internal sealed class AttackComponent : global::IGameComponent
{
    private float duration;
    private float elapsedTime;
    private bool hitWasRequested;

    public int Damage { get; }
    public float Range { get; }

    // Момент попадания от 0 до 1.
    // 0.45 означает 45% длительности анимации.
    public float HitTimeNormalized { get; }

    public bool IsAttacking { get; private set; }
    public float ElapsedSeconds => elapsedTime;

    // Равен true только один кадр за одну атаку.
    public bool ShouldDealDamage { get; private set; }

    public AttackComponent(
        int damage,
        float range,
        float hitTimeNormalized)
    {
        if (damage < 0)
            throw new ArgumentOutOfRangeException(nameof(damage));

        if (range <= 0f)
            throw new ArgumentOutOfRangeException(nameof(range));

        Damage = damage;
        Range = range;

        HitTimeNormalized = Math.Clamp(
            hitTimeNormalized,
            0f,
            1f);
    }

    public void SetDuration(float duration)
    {
        this.duration = Math.Max(duration, 0.0001f);
    }

    public bool TryStart()
    {
        if (IsAttacking || duration <= 0f)
            return false;

        IsAttacking = true;
        elapsedTime = 0f;
        hitWasRequested = false;
        ShouldDealDamage = false;

        return true;
    }

    public void Update(float deltaTime)
    {
        ShouldDealDamage = false;

        if (!IsAttacking)
            return;

        elapsedTime += deltaTime;

        float hitTime =
            duration * HitTimeNormalized;

        if (!hitWasRequested &&
            elapsedTime >= hitTime)
        {
            hitWasRequested = true;
            ShouldDealDamage = true;
        }

        if (elapsedTime >= duration)
        {
            elapsedTime = 0f;
            IsAttacking = false;
        }
    }

    public void Cancel()
    {
        IsAttacking = false;
        ShouldDealDamage = false;
        hitWasRequested = false;
        elapsedTime = 0f;
    }
}
