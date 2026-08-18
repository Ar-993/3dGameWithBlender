using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;

public class CharacterAnimator
{
    private CompiledModel model = null!;
    public string CurrentClip { get; private set; } = "";
    private bool isLooping = true;

    public float CurrentClipDuration => string.IsNullOrEmpty(CurrentClip)
        ? 0f
        : model.GetClipDuration(CurrentClip);

    // Загрузка любого набора анимаций
    public void LoadContent(GraphicsDevice graphicsDevice, string directoryPath, Dictionary<string, string> animations, Texture2D texture, Effect toonEffect)
    {
        string assetName = new DirectoryInfo(directoryPath).Name.Equals("Skeleton", StringComparison.OrdinalIgnoreCase)
            ? "skeleton"
            : "player";
        model = CompiledModel.Load(graphicsDevice, assetName, texture, toonEffect);

        foreach (var clipName in animations.Keys)
        {
            Play(clipName);
            break;
        }
    }

    public void Play(string clipName, bool loop = true)
    {
        CurrentClip = clipName;
        isLooping = loop;
    }

    public void Update(float deltaTime)
    {
        if (!string.IsNullOrEmpty(CurrentClip))
        {
            model.Update(CurrentClip, deltaTime);
        }
    }

    public float GetClipDuration(string clipName) =>
        model.GetClipDuration(clipName);

    public void Draw(
    Matrix world,
    Matrix view,
    Matrix projection,
    Effect? customEffect = null)
    {
        model.Draw(
            world,
            view,
            projection,
            isLooping,
            customEffect);
    }
}
