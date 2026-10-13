using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

// The local player is a physics capsule: no mesh or animation is required.
public sealed class Player
{
    private readonly CharacterPhysicsComponent physics;
    private readonly FirstPersonController controller;
    private readonly StatsComponent stats;
    private readonly AttackComponent attack;
    private readonly AttackComponent flyKick = new(30, 1.6f, 0.45f);

    public float pickupDistance;
    public float throwSpeed;
    public PlayerDefinition Definition { get; }
    public Vector3 Position => physics.Position;
    public float RotationY => controller.Yaw;
    public float Speed { get; set; } = 6f;
    public float CollisionRadius => physics.Radius;
    public int Health => stats.Health;
    public int MaxHealth => stats.MaxHealth;
    public bool IsDead => stats.IsDead;
    public Level.Platform? CurrentPlatform => physics.CurrentPlatform;
    public bool AttackShouldDealDamage => attack.ShouldDealDamage || flyKick.ShouldDealDamage;
    public int AttackDamage => flyKick.ShouldDealDamage ? flyKick.Damage : attack.Damage;
    public float AttackRange => flyKick.ShouldDealDamage ? flyKick.Range : attack.Range;

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
        flyKick.SetDuration(1f);
    }


    public void TakeDamage(int damage)
    {
        stats.TakeDamage(damage);
        if (IsDead)
        {
            attack.Cancel();
            flyKick.Cancel();
        }
    }

    public void SpawnAt(Vector3 position) => physics.SpawnAt(position);
    public void SynchronizeInput(KeyboardState keyboard) => controller.SynchronizeInput(keyboard);

    public void Update(GameTime gameTime, KeyboardState keyboard, float cameraYaw,
        Level level, bool attackPressed, bool flyKickPressed)
    {
        float seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (attackPressed && !IsDead && !flyKick.IsAttacking)
            attack.TryStart();
        if (flyKickPressed && !IsDead && !attack.IsAttacking)
            flyKick.TryStart();
        attack.Update(seconds);
        flyKick.Update(seconds);

        bool canMove = !IsDead && !flyKick.IsAttacking;
        Vector3 kickMovement = !IsDead && flyKick.IsAttacking
            ? Vector3.TransformNormal(Vector3.Forward, Matrix.CreateRotationY(cameraYaw)) * Speed * 1.2f * seconds
            : Vector3.Zero;
        controller.Update(keyboard, cameraYaw, level, seconds, Speed,
            canMove, canMove && !attack.IsAttacking, kickMovement);
    }
}
