using Microsoft.Xna.Framework;

namespace _3DLight;

internal abstract class AIComponent : global::IGameComponent
{
    public enum AiState
    {
        Idle,
        Chasing,
        Attacking,
        Hurt,
        Dead
    }

    private float attackDuration;
    private float attackTime;
    private float cooldownTime;
    private bool hitWasApplied;
    private float hurtDuration;
    private float hurtTime;

    protected float DetectionRange { get; }
    protected float AttackRange { get; }
    protected float HitTimeNormalized { get; }
    protected float AttackCooldown { get; }

    public AiState State { get; private set; } = AiState.Idle;
    public Vector3 Direction { get; private set; } = Vector3.Zero;
    public bool ShouldDealDamage { get; private set; }
    public bool IsEnabled { get; private set; } = true;

    protected AIComponent(
        float detectionRange,
        float attackRange,
        float hitTimeNormalized,
        float attackCooldown)
    {
        DetectionRange = detectionRange;
        AttackRange = attackRange;
        HitTimeNormalized = MathHelper.Clamp(hitTimeNormalized, 0f, 1f);
        AttackCooldown = attackCooldown;
    }

    public void SetAttackDuration(float duration)
    {
        attackDuration = MathF.Max(duration, 0.0001f);
    }

    public void SetHurtDuration(float duration)
    {
        hurtDuration = MathF.Max(duration, 0.0001f);
    }

    public void BindStats(StatsComponent stats)
    {
        stats.Damaged += OnDamaged;
        stats.Died += OnDied;
    }

    private void OnDamaged(int damage)
    {
        if (!IsEnabled)
            return;

        // Реакция поведения на событие StatsComponent, а не применение урона.
        // Получение урона прерывает движение или текущую атаку.
        Direction = Vector3.Zero;
        attackTime = 0f;
        hitWasApplied = false;
        ShouldDealDamage = false;

        hurtTime = 0f;
        State = AiState.Hurt;
    }

    private void OnDied()
    {
        Direction = Vector3.Zero;
        attackTime = 0f;
        hitWasApplied = false;
        ShouldDealDamage = false;
        State = AiState.Dead;
        IsEnabled = false;
    }

    public void Update(
        Vector3 ownerPosition,
        Vector3 targetPosition,
        bool canReachTarget,
        float deltaTime)
    {
        ShouldDealDamage = false;

        if (!IsEnabled)
        {
            Direction = Vector3.Zero;
            return;
        }

        if (State == AiState.Hurt)
        {
            Direction = Vector3.Zero;
            hurtTime += deltaTime;

            if (hurtTime >= hurtDuration)
                State = AiState.Idle;

            return;
        }

        cooldownTime = MathF.Max(0f, cooldownTime - deltaTime);

        Vector3 direction = targetPosition - ownerPosition;
        direction.Y = 0f;
        float distance = direction.Length();

        if (distance > 0.0001f)
            direction.Normalize();
        else
            direction = Vector3.Zero;

        Direction = direction;

        // Начатый удар доигрывается, даже если цель успела отойти.
        if (State == AiState.Attacking)
        {
            UpdateAttack(distance, deltaTime);
            return;
        }

        if (!canReachTarget || distance > DetectionRange)
        {
            State = AiState.Idle;
            Direction = Vector3.Zero;
            return;
        }

        if (distance <= AttackRange)
        {
            if (cooldownTime <= 0f && attackDuration > 0f)
                BeginAttack();
            else
                State = AiState.Idle;

            return;
        }

        State = AiState.Chasing;
    }

    private void BeginAttack()
    {
        State = AiState.Attacking;
        attackTime = 0f;
        hitWasApplied = false;
    }

    private void UpdateAttack(float distance, float deltaTime)
    {
        attackTime += deltaTime;
        float hitTime = attackDuration * HitTimeNormalized;

        if (!hitWasApplied && attackTime >= hitTime)
        {
            hitWasApplied = true;
            ShouldDealDamage = distance <= AttackRange;
        }

        if (attackTime < attackDuration)
            return;

        attackTime = 0f;
        cooldownTime = AttackCooldown;
        State = AiState.Idle;
    }
}