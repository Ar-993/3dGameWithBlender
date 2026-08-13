using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;

public class CharacterAnimator
{
    private AnimatedMixamoModel model = null!;
    public string CurrentClip { get; private set; } = "";
    private float stateTime;
    private bool isLooping = true;

    public float CurrentTime => stateTime;
    public float CurrentClipDuration => string.IsNullOrEmpty(CurrentClip)
        ? 0f
        : model.GetClipDuration(CurrentClip);

    // Загрузка любого набора анимаций
    public void LoadContent(GraphicsDevice graphicsDevice, string directoryPath, Dictionary<string, string> animations, Texture2D? texture = null)
    {
        model = new AnimatedMixamoModel(graphicsDevice);

        var fullPaths = new Dictionary<string, string>();
        foreach (var (clipName, fileName) in animations)
        {
            fullPaths[clipName] = Path.Combine(directoryPath, fileName);
        }

        // Передаем текстуру в модель 4-м аргументом
        model.Load(fullPaths, texture);

        foreach (var clipName in animations.Keys)
        {
            Play(clipName);
            break;
        }
    }

    // Переключение анимации (например: Play("Run"), Play("Attack"))
    public void Play(string clipName, bool loop = true)
    {
        if (CurrentClip != clipName)
        {
            CurrentClip = clipName;
            stateTime = 0f; // Сбрасываем время при смене клипа
        }
        isLooping = loop;
    }

    public void Update(float deltaTime)
    {
        stateTime += deltaTime;
    }

    public float GetClipDuration(string clipName) =>
        model.GetClipDuration(clipName);

    public void Draw(Matrix world, Matrix view, Matrix projection)
    {
        model.Draw(CurrentClip, stateTime, isLooping, world, view, projection);
    }
}
