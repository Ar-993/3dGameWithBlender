using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace _3DLight;

public sealed class Bullet : System.IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly BasicEffect _effect;
    private readonly VertexPositionColor[] _vertices;

    public Vector3 Position {  get; private set; }
    public Vector3 Direction { get; }
    public float Speed { get; private set; }
    public int Damage { get; }
    public float LifeTime { get; private set; } = 3f;
    public bool IsDead => LifeTime <= 0f;

    public BoundingSphere Bounds => new BoundingSphere(Position, 0.2f);

    public Bullet(GraphicsDevice graphicsDevice, Vector3 position, Vector3 direction, int damage, float speed = 18f)
    {
        this._graphicsDevice = graphicsDevice;
        Position = position;
        Direction = Vector3.Normalize(direction);
        Speed = speed;
        Damage = damage;

        _vertices = CreateSphere(0.15f, Color.Yellow);

        _effect = new BasicEffect(graphicsDevice) { VertexColorEnabled = true };
    }

    public void Update(float deltaTime)
    {
        Position += Direction * Speed * deltaTime;
        LifeTime -= deltaTime;
    }

    public void Draw(Matrix view, Matrix projection)
    {
        _effect.World = Matrix.CreateTranslation(Position);
        _effect.View = view;
        _effect.Projection = projection;

        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;

        _effect.CurrentTechnique.Passes[0].Apply();
        _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _vertices, 0, _vertices.Length / 3);
    }

    private static VertexPositionColor[] CreateSphere(float radius, Color color)
    {
        var list = new List<VertexPositionColor>();
        int slices = 8;
        int stacks = 8;
        
        for (int stack = 0; stack <stacks; stack++)
        {
            float phi1 = MathHelper.Pi * stack / stacks;
            float phi2 = MathHelper.Pi * (stack + 1) / stacks;
            for(int slice = 0; slice < slices; slice++)
            {
                float theta1 = MathHelper.TwoPi * slice / slices;
                float theta2 = MathHelper.TwoPi * (slice + 1) / slices;

                Vector3 p1 = GetPoint(radius, phi1, theta1);
                Vector3 p2 = GetPoint(radius, phi1, theta2);
                Vector3 p3 = GetPoint(radius, phi2, theta1);
                Vector3 p4 = GetPoint(radius, phi2, theta2);

                list.Add(new VertexPositionColor(p1, color));
                list.Add(new VertexPositionColor(p3, color));
                list.Add(new VertexPositionColor(p2, color));
                list.Add(new VertexPositionColor(p2, color));
                list.Add(new VertexPositionColor(p3, color));
                list.Add(new VertexPositionColor(p4, color));
            }
        }
        return list.ToArray();
    }

    private static Vector3 GetPoint(float radius, float phi, float theta) =>
        new(radius * MathF.Sin(phi) * MathF.Cos(theta), radius * MathF.Cos(phi), radius * MathF.Sin(phi) * MathF.Sin(theta));

    public void Dispose() => _effect.Dispose();
}
