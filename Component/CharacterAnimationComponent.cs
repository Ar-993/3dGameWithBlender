using _3DLight;
using _3DLight.Models;
using Ars3D.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal sealed class CharacterAnimationComponent : IDisposable
{
    private CompiledModel model = null!;
    private AnimationSampler sampler = null!;
    private CharacterAnimator animator = null!;
    private BonePose[] renderPose = [];
    private readonly float modelScale;
    private Matrix modelTransform;

    public float CurrentClipDuration => animator?.CurrentClipDuration ?? 0f;
    public float RotationY { get; set; }

    public CharacterAnimationComponent(float modelScale)
    {
        this.modelScale = modelScale;
        modelTransform = Matrix.CreateScale(modelScale);
    }

    public void LoadContent(
    GraphicsDevice graphicsDevice,
    string modelAssetName,
    string initialClip,
    Texture2D texture,
    Effect toonEffect,
    float? targetHeight = null)
    {
        CompiledModel replacement = ModelAssetLoader.Load(
            graphicsDevice, ModelPaths.ModelFile(modelAssetName), texture, toonEffect,
            ModelPaths.TextureDirectory(modelAssetName));
        CompiledModel? previous = model;
        try
        {
            var nextSampler = new AnimationSampler(
                replacement.Data.Nodes, replacement.Data.Clips);
            var nextAnimator = new CharacterAnimator(nextSampler, replacement.Hierarchy, initialClip);
            var nextPose = new BonePose[nextSampler.NodeCount];
            Matrix nextTransform = targetHeight is { } height
                ? ItemModelTransform.FitToHeight(replacement.Data, replacement.Hierarchy, height)
                : Matrix.CreateScale(modelScale);

            model = replacement;
            sampler = nextSampler;
            animator = nextAnimator;
            renderPose = nextPose;
            modelTransform = nextTransform;
        }
        catch
        {
            replacement.Dispose();
            throw;
        }

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
    SceneLighting lighting,
    SceneRenderQueue? renderQueue = null)
    {
        Matrix world =
            modelTransform *
            Matrix.CreateRotationY(RotationY) *
            Matrix.CreateTranslation(position);

        animator.CopyPoseTo(renderPose);
        model.DrawPose(renderPose, world, view, projection, lighting, renderQueue);
    }

    public float GetClipDuration(string clipName)
    {
        return sampler.GetClipDuration(clipName);
    }

    public void Dispose()
    {
        model?.Dispose();
        model = null!;
        sampler = null!;
        animator = null!;
        renderPose = [];
    }
}
