using System;

namespace _3DLight;

internal sealed class StatsComponent : global::IGameComponent
{
    public event Action<int>? Damaged;
    public event Action? Died;

    public int MaxHealth { get; }
    public int Health { get; private set; }
    public bool IsDead => Health == 0;

    public StatsComponent(int maxHealth)
    {
        if (maxHealth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHealth));

        MaxHealth = maxHealth;
        Health = maxHealth;
    }

    public int TakeDamage(int damage)
    {
        if (IsDead)
            return 0;

        int appliedDamage = Math.Clamp(damage, 0, Health);
        Health -= appliedDamage;

        if (appliedDamage == 0)
            return 0;

        if (IsDead)
            Died?.Invoke();
        else
            Damaged?.Invoke(appliedDamage);

        return appliedDamage;
    }

    public void Heal(int amount)
    {
        if (amount > 0 && !IsDead)
            Health = Math.Min(Health + amount, MaxHealth);
    }
}