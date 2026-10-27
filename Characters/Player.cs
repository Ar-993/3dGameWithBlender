using _3DLight;
using Camera;
using Characters;
using Microsoft.Xna.Framework;

// The local player is a physics capsule: no mesh or animation is required.
public sealed class Player
{
    private readonly CharacterPhysicsComponent physics;
    private readonly FirstPersonController controller;
    private readonly StatsComponent stats;
    private readonly AttackComponent attack;

    public PlayerDefinition Definition { get; }
    public Vector3 Position => physics.Position;
    public float RotationY => controller.Yaw;
    public float Speed { get; set; } = 6f;
    public float CollisionRadius => physics.Radius;
    public int Health => stats.Health;
    public int MaxHealth => stats.MaxHealth;
    public bool IsDead => stats.IsDead;
    public Level.Platform? CurrentPlatform => physics.CurrentPlatform;
    public bool AttackShouldDealDamage => attack.ShouldDealDamage;
    public int AttackDamage => attack.Damage;
    public float AttackRange => attack.Range;

    internal Player(CharacterPhysicsComponent physics, StatsComponent stats, AttackComponent attack,
        PlayerDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        this.physics = physics;
        this.stats = stats;
        this.attack = attack;
        controller = new FirstPersonController(physics);
        attack.SetDuration(2f);
    }


    public void TakeDamage(int damage)
    {
        stats.TakeDamage(damage);
        if (IsDead)
            attack.Cancel();
    }

    public void SpawnAt(Vector3 position) => physics.SpawnAt(position);
    public void Update(float seconds, Vector2 movement, bool jumpPressed,
        bool attackPressed, float cameraYaw, Level level)
    {
        if (attackPressed && !IsDead)
            attack.TryStart();
        attack.Update(seconds);

        controller.Update(movement, jumpPressed, cameraYaw, level, seconds, Speed,
            canMove: !IsDead, canJump: !IsDead && !attack.IsAttacking);
    }
}
