using Microsoft.Xna.Framework;

namespace _3DLight;

internal sealed class Bullet
{
    public Vector3 Position { get; private set; }
    public Vector3 Direction { get; }
    public float Speed { get; private set; }
    public int Damage { get; }
    public float LifeTime { get; private set; } = 3f;
    public bool IsDead => LifeTime <= 0f;

    public BoundingSphere Bounds => new BoundingSphere(Position, 0.2f);

    public Bullet(Vector3 position, Vector3 direction, int damage, float speed = 18f)
    {
        Position = position;
        Direction = Vector3.Normalize(direction);
        Speed = speed;
        Damage = damage;
    }

    public void Update(float deltaTime)
    {
        Position += Direction * Speed * deltaTime;
        LifeTime -= deltaTime;
    }
}
