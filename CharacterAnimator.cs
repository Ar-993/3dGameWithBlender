using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;

public sealed class CharacterAnimator
{
    private CompiledModel model = null!;
    private float stateTime;
    private bool isLooping = true;
    public string CurrentClip { get; private set; } = "";
    public float CurrentClipDuration => string.IsNullOrEmpty(CurrentClip) ? 0f : model.GetClipDuration(CurrentClip);

    public void LoadContent(GraphicsDevice graphicsDevice, string directoryPath,
        Dictionary<string, string> animations, Texture2D texture, Effect toonEffect)
    {
        string assetName = new DirectoryInfo(directoryPath).Name.Equals("Skeleton", StringComparison.OrdinalIgnoreCase)
            ? "skeleton"
            : "player";
        model = CompiledModel.Load(graphicsDevice, assetName, texture, toonEffect);
        foreach (string clipName in animations.Keys) { Play(clipName); break; }
    }

    public void Play(string clipName, bool loop = true)
    {
        if (CurrentClip != clipName) { CurrentClip = clipName; stateTime = 0f; }
        isLooping = loop;
    }

    public void Update(float deltaTime) => stateTime += deltaTime;
    public float GetClipDuration(string clipName) => model.GetClipDuration(clipName);
    public void Draw(Matrix world, Matrix view, Matrix projection) =>
        model.Draw(CurrentClip, stateTime, isLooping, world, view, projection);
}
