using Microsoft.Xna.Framework;

namespace _3DLight;

internal sealed class Bullet : IDisposable
{
    // LightGame owns the model shared by all candy projectiles.
    private readonly CompiledModel model;
    private float rotation;
    public Vector3 Position { get; private set; }
    public Vector3 Direction { get; }
    public float Speed { get; }
    public int Damage { get; }
    public float LifeTime { get; private set; } = 3f;
    public bool IsDead => LifeTime <= 0f;
    public BoundingSphere Bounds => new(Position, 0.2f);

    public Bullet(CompiledModel model, Vector3 position, Vector3 direction, int damage, float speed = 18f)
    {
        this.model = model;
        Position = position;
        Direction = Vector3.Normalize(direction);
        Speed = speed;
        Damage = damage;
    }

    public void Update(float deltaTime)
    {
        Position += Direction * Speed * deltaTime;
        rotation += deltaTime * 6f;
        LifeTime -= deltaTime;
    }

    public void Draw(Matrix view, Matrix projection, SceneLighting lighting) =>
        model.Draw(Matrix.CreateRotationX(rotation) * Matrix.CreateRotationY(rotation * 0.7f) *
            Matrix.CreateTranslation(Position), view, projection, lighting);

    public void Dispose() { }
}
