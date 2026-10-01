using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight;

internal sealed class Bullet : System.IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly CompiledModel _bulletModel;
    private readonly SceneLighting _lighting;

    public Vector3 Position { get; private set; }
    public Vector3 Direction { get; }
    public float Speed { get; private set; }
    public int Damage { get; }
    public float LifeTime { get; private set; } = 3f;
    public bool IsDead => LifeTime <= 0f;

    public BoundingSphere Bounds => new BoundingSphere(Position, 0.2f);

    public Bullet(GraphicsDevice graphicsDevice, Vector3 position, Vector3 direction, CompiledModel bulletModel, SceneLighting lighting, int damage, float speed = 18f)
    {
        this._graphicsDevice = graphicsDevice;
        this._bulletModel = bulletModel;
        this._lighting = lighting;
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

    public void Draw(Matrix view, Matrix projection)
    {
        Matrix rotation = Matrix.CreateLookAt(Vector3.Zero, Direction, Vector3.Up);
        rotation = Matrix.Invert(rotation);

        Matrix world = rotation * Matrix.CreateTranslation(Position);

        _bulletModel.Draw(world, view, projection, _lighting);
    }

    public void Dispose()
    {

    }
}