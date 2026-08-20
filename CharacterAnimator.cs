using _3DLight;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;

public sealed class CharacterAnimator
{
    private const float DefaultBlendDuration = 0.15f;

    private CompiledModel model = null!;
    private BonePose[] currentPose = [];
    private BonePose[] transitionSourcePose = [];
    private BonePose[] blendedPose = [];

    private float currentTime;
    private bool currentLooping = true;
    private float currentRangeStartNormalized;
    private float currentRangeEndNormalized = 1f;

    private string transitionSourceClip = "";
    private float transitionSourceTime;
    private bool transitionSourceLooping;
    private float transitionSourceRangeStartNormalized;
    private float transitionSourceRangeEndNormalized = 1f;
    private bool transitionSourceIsSnapshot;
    private float blendTime;
    private float blendDuration;
    private bool isBlending;

    public string CurrentState { get; private set; } = "";
    public string CurrentClip { get; private set; } = "";
    public float CurrentClipDuration => string.IsNullOrEmpty(CurrentClip)
        ? 0f
        : model.GetClipDuration(CurrentClip);

    public void LoadContent(GraphicsDevice graphicsDevice, string directoryPath,
        Dictionary<string, string> animations, Texture2D texture, Effect toonEffect)
    {
        string assetName = new DirectoryInfo(directoryPath).Name.Equals("Skeleton", StringComparison.OrdinalIgnoreCase)
            ? "skeleton"
            : "player";
        model = CompiledModel.Load(graphicsDevice, assetName, texture, toonEffect);

        currentPose = new BonePose[model.NodeCount];
        transitionSourcePose = new BonePose[model.NodeCount];
        blendedPose = new BonePose[model.NodeCount];

        string initialClip = animations.Keys.FirstOrDefault(
            clipName => clipName.Equals("Idle", StringComparison.OrdinalIgnoreCase))
            ?? animations.Keys.FirstOrDefault()
            ?? throw new InvalidOperationException("Для модели не заданы анимации.");

        Play(initialClip, blendDuration: 0f);
    }

    public void Play(
        string clipName,
        bool loop = true,
        float? blendDuration = null,
        bool restart = false)
    {
        PlayAnimation(
            clipName,
            rangeStartNormalized: 0f,
            rangeEndNormalized: 1f,
            loop,
            blendDuration,
            restart);
    }

    public void PlaySegment(
        string stateName,
        string clipName,
        float rangeStartNormalized,
        float rangeEndNormalized,
        bool loop = true,
        float? blendDuration = null,
        bool restart = false) =>
        PlayAnimation(
            clipName,
            rangeStartNormalized,
            rangeEndNormalized,
            loop,
            blendDuration,
            restart,
            stateName);

    private void PlayAnimation(
        string clipName,
        float rangeStartNormalized,
        float rangeEndNormalized,
        bool loop,
        float? blendDuration,
        bool restart,
        string? stateName = null)
    {
        stateName ??= clipName;
        ValidatePlaybackRange(rangeStartNormalized, rangeEndNormalized);

        if (!restart &&
            string.Equals(CurrentState, stateName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(CurrentClip, clipName, StringComparison.OrdinalIgnoreCase) &&
            currentRangeStartNormalized == rangeStartNormalized &&
            currentRangeEndNormalized == rangeEndNormalized)
        {
            currentLooping = loop;
            return;
        }

        float requestedBlendDuration = Math.Max(
            0f,
            blendDuration ?? ResolveBlendDuration(CurrentState, stateName));

        if (string.IsNullOrEmpty(CurrentClip))
        {
            CurrentState = stateName;
            CurrentClip = clipName;
            currentTime = 0f;
            currentLooping = loop;
            currentRangeStartNormalized = rangeStartNormalized;
            currentRangeEndNormalized = rangeEndNormalized;
            StopBlending();
            return;
        }

        if (isBlending)
        {
            EvaluateCurrentPose(transitionSourcePose);
            transitionSourceClip = "";
            transitionSourceIsSnapshot = true;
        }
        else
        {
            transitionSourceClip = CurrentClip;
            transitionSourceTime = currentTime;
            transitionSourceLooping = currentLooping;
            transitionSourceRangeStartNormalized = currentRangeStartNormalized;
            transitionSourceRangeEndNormalized = currentRangeEndNormalized;
            transitionSourceIsSnapshot = false;
        }

        CurrentState = stateName;
        CurrentClip = clipName;
        currentTime = 0f;
        currentLooping = loop;
        currentRangeStartNormalized = rangeStartNormalized;
        currentRangeEndNormalized = rangeEndNormalized;
        blendTime = 0f;
        this.blendDuration = requestedBlendDuration;
        isBlending = requestedBlendDuration > 0f;

        if (!isBlending)
            StopBlending();
    }

    public void Update(float deltaTime)
    {
        currentTime += deltaTime;

        if (!isBlending)
            return;

        if (!transitionSourceIsSnapshot)
            transitionSourceTime += deltaTime;

        blendTime += deltaTime;

        if (blendTime >= blendDuration)
            StopBlending();
    }

    public float GetClipDuration(string clipName) => model.GetClipDuration(clipName);

    public void Draw(Matrix world, Matrix view, Matrix projection)
    {
        EvaluateCurrentPose(blendedPose);
        model.DrawPose(blendedPose, world, view, projection);
    }

    private void EvaluateCurrentPose(BonePose[] destination)
    {
        SamplePlaybackPose(
            CurrentClip,
            currentTime,
            currentLooping,
            currentRangeStartNormalized,
            currentRangeEndNormalized,
            currentPose);

        if (!isBlending)
        {
            Array.Copy(currentPose, destination, currentPose.Length);
            return;
        }

        if (!transitionSourceIsSnapshot)
        {
            SamplePlaybackPose(
                transitionSourceClip,
                transitionSourceTime,
                transitionSourceLooping,
                transitionSourceRangeStartNormalized,
                transitionSourceRangeEndNormalized,
                transitionSourcePose);
        }

        float linearAmount = MathHelper.Clamp(blendTime / blendDuration, 0f, 1f);
        float smoothAmount = linearAmount * linearAmount * (3f - 2f * linearAmount);

        for (int nodeIndex = 0; nodeIndex < destination.Length; nodeIndex++)
        {
            destination[nodeIndex] = BonePose.Blend(
                transitionSourcePose[nodeIndex],
                currentPose[nodeIndex],
                smoothAmount);
        }
    }

    private void SamplePlaybackPose(
        string clipName,
        float elapsedSeconds,
        bool loop,
        float rangeStartNormalized,
        float rangeEndNormalized,
        BonePose[] destination)
    {
        float clipDuration = model.GetClipDuration(clipName);
        float rangeStart = clipDuration * rangeStartNormalized;
        float rangeDuration = clipDuration *
            (rangeEndNormalized - rangeStartNormalized);

        float rangeTime = loop && rangeDuration > 0f
            ? elapsedSeconds % rangeDuration
            : Math.Min(elapsedSeconds, rangeDuration);

        model.SamplePose(
            clipName,
            rangeStart + rangeTime,
            loop: false,
            destination);
    }

    private void StopBlending()
    {
        transitionSourceClip = "";
        transitionSourceTime = 0f;
        transitionSourceLooping = false;
        transitionSourceRangeStartNormalized = 0f;
        transitionSourceRangeEndNormalized = 1f;
        transitionSourceIsSnapshot = false;
        blendTime = 0f;
        blendDuration = 0f;
        isBlending = false;
    }

    private static float ResolveBlendDuration(string sourceState, string targetState)
    {
        if (string.IsNullOrEmpty(sourceState))
            return 0f;

        if (targetState.Equals("Die", StringComparison.OrdinalIgnoreCase) ||
            targetState.Equals("Hurt", StringComparison.OrdinalIgnoreCase))
        {
            return 0.08f;
        }

        if (sourceState.Equals("Landing", StringComparison.OrdinalIgnoreCase) &&
            targetState.Equals("Run", StringComparison.OrdinalIgnoreCase))
        {
            return 0.06f;
        }

        if (targetState.Equals("Jump", StringComparison.OrdinalIgnoreCase) ||
            targetState.Equals("Falling", StringComparison.OrdinalIgnoreCase) ||
            targetState.Equals("FallingBad", StringComparison.OrdinalIgnoreCase) ||
            targetState.Equals("Landing", StringComparison.OrdinalIgnoreCase))
            return 0.1f;

        bool isLocomotionTransition =
            IsLocomotionState(sourceState) && IsLocomotionState(targetState);

        return isLocomotionTransition ? 0.2f : DefaultBlendDuration;
    }

    private static bool IsLocomotionState(string stateName) =>
        stateName.Equals("Idle", StringComparison.OrdinalIgnoreCase) ||
        stateName.Equals("Run", StringComparison.OrdinalIgnoreCase);

    private static void ValidatePlaybackRange(
        float rangeStartNormalized,
        float rangeEndNormalized)
    {
        bool isValid =
            float.IsFinite(rangeStartNormalized) &&
            float.IsFinite(rangeEndNormalized) &&
            rangeStartNormalized >= 0f &&
            rangeStartNormalized < rangeEndNormalized &&
            rangeEndNormalized <= 1f;

        if (!isValid)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rangeEndNormalized),
                "Диапазон анимации должен находиться внутри 0..1, а начало должно быть меньше конца.");
        }
    }
}
