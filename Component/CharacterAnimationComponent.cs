using _3DLight;
using _3DLight.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

internal sealed class CharacterAnimationComponent : IGameComponent
{
    private CompiledModel model = null!;
    private AnimationSampler sampler = null!;
    private CharacterAnimator animator = null!;
    private BonePose[] renderPose = [];
    private readonly float modelScale;

    public float CurrentClipDuration => animator?.CurrentClipDuration ?? 0f;
    public float RotationY { get; set; }

    public CharacterAnimationComponent(float modelScale) => this.modelScale = modelScale;

    public void LoadContent(
        GraphicsDevice graphicsDevice,
        string animationsFolder,
        Dictionary<string, string> animations,
        Texture2D texture,
        Effect toonEffect)
    {
        string initialClip = animations.Keys.FirstOrDefault(
            clipName => clipName.Equals("Idle", StringComparison.OrdinalIgnoreCase))
            ?? animations.Keys.FirstOrDefault()
            ?? throw new InvalidOperationException("Для модели не заданы анимации.");
        string assetName = new DirectoryInfo(animationsFolder).Name.Equals(
            "Skeleton", StringComparison.OrdinalIgnoreCase)
            ? "skeleton"
            : "player";

        CompiledModel replacement = ModelAssetLoader.Load(
            graphicsDevice, assetName, texture, toonEffect);
        AnimationSampler replacementSampler;
        CharacterAnimator replacementAnimator;
        BonePose[] replacementPose;
        try
        {
            replacementSampler = new AnimationSampler(
                replacement.Data.Nodes, replacement.Data.Clips);
            replacementAnimator = new CharacterAnimator(
                replacementSampler, replacement.Hierarchy, initialClip);
            replacementPose = new BonePose[replacementSampler.NodeCount];
        }
        catch
        {
            replacement.Dispose();
            throw;
        }

        CompiledModel? previous = model;
        model = replacement;
        sampler = replacementSampler;
        animator = replacementAnimator;
        renderPose = replacementPose;
        previous?.Dispose();
    }

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

        animator.CopyPoseTo(renderPose);
        model.DrawPose(renderPose, world, view, projection, lighting);
    }

    public float GetClipDuration(string clipName)
    {
        return sampler.GetClipDuration(clipName);
    }
}
