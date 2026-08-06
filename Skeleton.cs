using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

public class Skeleton
{
    public Vector3 Position { get; set; }
    public float RotationY { get; private set; }
    public float Speed { get; set; } = 4f;

    private readonly CharacterAnimator animator = new();

    public Skeleton(Vector3 startPosition) => Position = startPosition;

    public void LoadContent(GraphicsDevice graphicsDevice, ContentManager content, string animsFolder)
    {
        string skeletonFolder = Path.Combine(animsFolder, "Skeleton");

        // Загружаем скомпилированную MGCB текстуру
        Texture2D skeletonTexture = content.Load<Texture2D>("Assets/Skeleton/skeleton_texture");

        var monsterAnims = new Dictionary<string, string>
        {
            { "Idle", "SkeletonIdle.fbx" },
            { "Run",  "SkeletonRun.fbx"  }
        };

        animator.LoadContent(graphicsDevice, skeletonFolder, monsterAnims, skeletonTexture);
    }

    public void Update(Vector3 playerPosition, float deltaTime)
    {
        Vector3 dir = playerPosition - Position;
        dir.Y = 0;
        float distance = dir.Length();

        if (distance < 30f && distance > 1.5f) // Преследуем игрока
        {
            dir.Normalize();
            Position += dir * Speed * deltaTime;
            RotationY = MathF.Atan2(dir.X, dir.Z);

            animator.Play("Run", loop: true);
        }
        else // Стоим
        {
            animator.Play("Idle", loop: true);
        }

        animator.Update(deltaTime);
    }

    public void Draw(Matrix view, Matrix projection)
    {
        Matrix world = Matrix.CreateRotationY(RotationY) * Matrix.CreateTranslation(Position);
        animator.Draw(world, view, projection);
    }
}