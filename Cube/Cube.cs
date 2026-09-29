using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight;

public sealed class ShootingCube : IDisposable
{
    private readonly GraphicsDevice graphicsDevice;
    private readonly BasicEffect effect;
    private readonly VertexPositionColor[] vertices;

    // Встроенный компонент атаки: 15 урона, радиус 20м, выстрел на 30% времени цикла
    private readonly AttackComponent attack = new(
        damage: 15,
        range: 20f,
        hitTimeNormalized: 0.3f);

    public Vector3 Position { get; }
    public Vector3 Size { get; }
    public BoundingBox Bounds { get; }

    public bool ShouldSpawnBullet => attack.ShouldDealDamage;

    public Vector3 AimDirection { get; private set; } = Vector3.Forward;

    public ShootingCube(GraphicsDevice graphicsDevice, Vector3 position, Vector3 size, float fireRateSeconds = 2.0f)
    {
        this.graphicsDevice = graphicsDevice;
        Position = position;
        Size = size;

        attack.SetDuration(fireRateSeconds);

        Vector3 halfSize = size / 2f;
        Bounds = new BoundingBox(position - halfSize, position + halfSize);
        vertices = CreateCubeMesh(halfSize, Color.Red);

        effect = new BasicEffect(graphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            World = Matrix.CreateTranslation(Position)
        };
    }

    public void Update(float deltaTime, Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - Position;
        float distance = toTarget.Length();

        if (distance > 0.001f)
        {
            AimDirection = Vector3.Normalize(toTarget);
        }

        if (distance <= attack.Range)
        {
            if (!attack.IsAttacking)
            {
                attack.TryStart();
            }
        }

        attack.Update(deltaTime);
    }

    public void Draw(Matrix view, Matrix projection)
    {
        effect.View = view;
        effect.Projection = projection;

        graphicsDevice.BlendState = BlendState.Opaque;
        graphicsDevice.DepthStencilState = DepthStencilState.Default;

        effect.CurrentTechnique.Passes[0].Apply();
        graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length / 3);
    }

    private static VertexPositionColor[] CreateCubeMesh(Vector3 halfSize, Color color)
    {
        float x = halfSize.X;
        float y = halfSize.Y; 
        float z = halfSize.Z;

        Vector3[] p = new Vector3[]
        {
            new(-x, -y,  z), new( x, -y,  z), new( x,  y,  z), new(-x,  y,  z),
            new(-x, -y, -z), new( x, -y, -z), new( x,  y, -z), new(-x,  y, -z)
        };

        int[] indices = new int[]
        {
            0,1,2, 0,2,3, // Front
            1,5,6, 1,6,2, // Right
            5,4,7, 5,7,6, // Back
            4,0,3, 4,3,7, // Left
            3,2,6, 3,6,7, // Top
            4,5,1, 4,1,0  // Bottom
        };

        VertexPositionColor[] result = new VertexPositionColor[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            result[i] = new VertexPositionColor(p[indices[i]], color);
        }
        return result;
    }

    public void Dispose() => effect.Dispose();
}