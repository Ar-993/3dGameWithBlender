using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using _3DLight;

public class Skeleton
{
    public Vector3 Position { get; set; }
    public float RotationY { get; private set; }
    public float Speed { get; set; } = 3.5f;

    private const float Gravity = -28f;
    private const float EdgeMargin = 0.35f;
    private float verticalVelocity;

    public Level.Platform? CurrentPlatform { get; private set; }

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

    public void Update(Player player, Level level, float deltaTime)
    {
        Vector3 previousPosition = Position;
        Vector3 nextPosition = Position;

        verticalVelocity += Gravity * deltaTime;
        nextPosition.Y += verticalVelocity * deltaTime;

        if (verticalVelocity <= 0f &&
            level.TryFindLanding(previousPosition, nextPosition, out Level.Platform? platform, out float surfaceY))
        {
            nextPosition.Y = surfaceY;
            verticalVelocity = 0f;
            CurrentPlatform = platform;
        }
        else
        {
            CurrentPlatform = null;
        }

        Position = nextPosition;

        Vector3 dir = player.Position - Position;
        dir.Y = 0; // Игнорируем разницу по высоте при расчете направления
        float distance = dir.Length();
        bool samePlatform = CurrentPlatform is not null &&
                            player.CurrentPlatform is not null &&
                            CurrentPlatform.Id == player.CurrentPlatform.Id;
        bool isRunning = false;

        if (samePlatform && distance < 60f && distance > 1.2f)
        {
            dir.Normalize();

            Vector3 newPos = Position + dir * Speed * deltaTime;
            if (CurrentPlatform!.ContainsHorizontal(newPos, EdgeMargin))
            {
                Position = newPos;
                isRunning = true;
            }

            RotationY = MathF.Atan2(dir.X, dir.Z);
        }

        animator.Play(isRunning ? "Run" : "Idle", loop: true);

        animator.Update(deltaTime);
    }

    public void Draw(Matrix view, Matrix projection)
    {
        Matrix world = Matrix.CreateScale(1f) * Matrix.CreateRotationY(RotationY) * Matrix.CreateTranslation(Position);
        animator.Draw(world, view, projection);
    }
}
