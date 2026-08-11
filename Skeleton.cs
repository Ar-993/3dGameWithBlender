using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using _3DLight;

public class Skeleton
{
    private readonly CharacterFacade character;

    public Vector3 Position => character.Position;
    public float RotationY => character.RotationY;
    public float Speed { get; set; } = 3.5f;

    public Level.Platform? CurrentPlatform => character.CurrentPlatform;

    internal Skeleton(CharacterFacade character) => this.character = character;

    public void LoadContent(GraphicsDevice graphicsDevice, ContentManager content, string animsFolder)
    {
        string skeletonFolder = Path.Combine(animsFolder, "Skeleton");
        Texture2D skeletonTexture = content.Load<Texture2D>("Assets/Skeleton/skeleton_texture");

        var monsterAnims = new Dictionary<string, string>
        {
            { "Idle", "SkeletonIdle.fbx" },
            { "Run",  "SkeletonRun.fbx"  }
        };

        character.LoadContent(graphicsDevice, skeletonFolder, monsterAnims, skeletonTexture);
    }

    public void Update(Player player, Level level, float deltaTime)
    {
        Vector3 dir = player.Position - Position;
        dir.Y = 0; // Игнорируем разницу по высоте при расчете направления
        float distance = dir.Length();
        bool samePlatform = CurrentPlatform is not null &&
                            player.CurrentPlatform is not null &&
                            CurrentPlatform.Id == player.CurrentPlatform.Id;
        Vector3 horizontalMovement = Vector3.Zero;

        if (samePlatform && distance < 60f && distance > 1.2f)
        {
            dir.Normalize();

            Vector3 newPos = Position + dir * Speed * deltaTime;
            if (CurrentPlatform!.ContainsHorizontal(newPos, character.CollisionRadius))
                horizontalMovement = dir * Speed * deltaTime;

            character.RotationY = MathF.Atan2(dir.X, dir.Z);
        }

        Vector3 oldPosition = Position;
        character.Move(level, horizontalMovement, deltaTime);

        bool isRunning = horizontalMovement != Vector3.Zero &&
                         (Position.X != oldPosition.X || Position.Z != oldPosition.Z);

        character.Play(isRunning ? "Run" : "Idle", loop: true, deltaTime);
    }

    public void Draw(Matrix view, Matrix projection)
    {
        character.Draw(view, projection);
    }
}
