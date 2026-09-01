using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

internal sealed class CharacterAnimationComponent : IGameComponent
{
    private readonly CharacterAnimator animator = new();
    private readonly float modelScale;

    public float CurrentClipDuration => animator.CurrentClipDuration;
    public float RotationY { get; set; }

    public CharacterAnimationComponent(float modelScale) => this.modelScale = modelScale;

    public void LoadContent(
        GraphicsDevice graphicsDevice,
        string animationsFolder,
        Dictionary<string, string> animations,
        Texture2D texture,
        Effect toonEffect) =>
        animator.LoadContent(graphicsDevice, animationsFolder, animations, texture, toonEffect);

    public void Play(string clipName, bool loop) =>
        animator.Play(clipName, loop);

    public void PlaySegment(
        string stateName,
        string clipName,
        float rangeStartNormalized,
        float rangeEndNormalized,
        bool loop) =>
        animator.PlaySegment(
            stateName,
            clipName,
            rangeStartNormalized,
            rangeEndNormalized,
            loop);

    public void Restart(string clipName, bool loop) =>
        animator.Play(clipName, loop, restart: true);

    public void Update(float deltaTime) =>
        animator.Update(deltaTime);

    public void SetUpperBodyOverlay(
        string clipName,
        string rootNodeName,
        float elapsedSeconds) =>
        animator.SetUpperBodyOverlay(
            clipName,
            rootNodeName,
            elapsedSeconds);

    public void ClearUpperBodyOverlay() =>
        animator.ClearUpperBodyOverlay();

    public void Draw(
        Vector3 position,
        Matrix view,
        Matrix projection,
        SceneLighting lighting)
    {
        Matrix world =
            Matrix.CreateScale(modelScale) *
            Matrix.CreateRotationY(RotationY) *
            Matrix.CreateTranslation(position);

        animator.Draw(world, view, projection, lighting);
    }

    public float GetClipDuration(string clipName)
    {
        return animator.GetClipDuration(clipName);
    }
}
