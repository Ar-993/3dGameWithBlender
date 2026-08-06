using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;

public class Skeleton
{
    public Vector3 Position { get; set; }
    public float RotationY { get; private set; }
    public float Speed { get; set; } = 3.5f;

    private readonly CharacterAnimator animator = new();

    public Skeleton(Vector3 startPosition) => Position = startPosition;

    public void LoadContent(GraphicsDevice graphicsDevice, ContentManager content, string animsFolder)
    {
        string skeletonFolder = Path.Combine(animsFolder, "Skeleton");
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
        dir.Y = 0; // Игнорируем разницу по высоте при расчете направления
        float distance = dir.Length();

        if (distance < 60f && distance > 1.2f) // Увеличили дистанцию обнаружения до 60м
        {
            dir.Normalize();

            Vector3 newPos = Position + dir * Speed * deltaTime;
            Position = new Vector3(newPos.X, playerPosition.Y, newPos.Z);

            RotationY = MathF.Atan2(dir.X, dir.Z);
            animator.Play("Run", loop: true);
        }
        else
        {
            animator.Play("Idle", loop: true);
        }

        animator.Update(deltaTime);
    }

    public void Draw(Matrix view, Matrix projection)
    {
        Matrix world = Matrix.CreateScale(1f) * Matrix.CreateRotationY(RotationY) * Matrix.CreateTranslation(Position);
        animator.Draw(world, view, projection);
    }
}