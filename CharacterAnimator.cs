using _3DLight;
using Microsoft.Xna.Framework;
using _3DLight.Models;

internal sealed class CharacterAnimator
{
    private const float DefaultBlendDuration = 0.15f;
    private const float UpperBodyBlendDuration = 0.08f;

    private readonly AnimationSampler sampler;
    private readonly ModelHierarchy hierarchy;
    private readonly BonePose[] currentPose;
    private readonly BonePose[] transitionSourcePose;
    private readonly BonePose[] upperBodyPose;
    private bool[] upperBodyMask = [];

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

    private string upperBodyClip = "";
    private string upperBodyRoot = "";
    private float upperBodyTime;
    private float upperBodyWeight;
    private float upperBodyTargetWeight;

    public string CurrentState { get; private set; } = "";
    public string CurrentClip { get; private set; } = "";
    public float CurrentClipDuration => string.IsNullOrEmpty(CurrentClip)
        ? 0f
        : sampler.GetClipDuration(CurrentClip);

    public CharacterAnimator(
        AnimationSampler sampler,
        ModelHierarchy hierarchy,
        string initialClip)
    {
        this.sampler = sampler;
        this.hierarchy = hierarchy;
        sampler.GetClipDuration(initialClip);

        currentPose = new BonePose[sampler.NodeCount];
        transitionSourcePose = new BonePose[sampler.NodeCount];
        upperBodyPose = new BonePose[sampler.NodeCount];

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

    public void SetUpperBodyOverlay(
        string clipName,
        string rootNodeName,
        float elapsedSeconds)
    {
        if (!string.Equals(
                upperBodyRoot,
                rootNodeName,
                StringComparison.OrdinalIgnoreCase))
        {
            upperBodyMask = hierarchy.CreateNodeHierarchyMask(rootNodeName);
            upperBodyRoot = rootNodeName;
        }

        upperBodyClip = clipName;
        upperBodyTime = Math.Max(0f, elapsedSeconds);
        upperBodyTargetWeight = 1f;
    }

    public void ClearUpperBodyOverlay()
    {
        upperBodyTargetWeight = 0f;
    }

    public void Update(float deltaTime)
    {
        currentTime += deltaTime;

        float overlayStep = UpperBodyBlendDuration > 0f
            ? deltaTime / UpperBodyBlendDuration
            : 1f;

        upperBodyWeight = upperBodyTargetWeight > upperBodyWeight
            ? Math.Min(upperBodyTargetWeight, upperBodyWeight + overlayStep)
            : Math.Max(upperBodyTargetWeight, upperBodyWeight - overlayStep);

        if (upperBodyTargetWeight <= 0f && upperBodyWeight <= 0f)
            upperBodyClip = "";

        if (!isBlending)
            return;

        if (!transitionSourceIsSnapshot)
            transitionSourceTime += deltaTime;

        blendTime += deltaTime;

        if (blendTime >= blendDuration)
            StopBlending();
    }

    public void CopyPoseTo(BonePose[] destination)
    {
        if (destination.Length != sampler.NodeCount)
        {
            throw new ArgumentException(
                $"Поза содержит {destination.Length} узлов вместо {sampler.NodeCount}.",
                nameof(destination));
        }

        EvaluateCurrentPose(destination);
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
        }
        else
        {
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

            float linearAmount = MathHelper.Clamp(
                blendTime / blendDuration,
                0f,
                1f);
            float smoothAmount =
                linearAmount * linearAmount * (3f - 2f * linearAmount);

            for (int nodeIndex = 0; nodeIndex < destination.Length; nodeIndex++)
            {
                destination[nodeIndex] = BonePose.Blend(
                    transitionSourcePose[nodeIndex],
                    currentPose[nodeIndex],
                    smoothAmount);
            }
        }

        ApplyUpperBodyOverlay(destination);
    }

    private void ApplyUpperBodyOverlay(BonePose[] destination)
    {
        if (string.IsNullOrEmpty(upperBodyClip) || upperBodyWeight <= 0f)
            return;

        SamplePlaybackPose(
            upperBodyClip,
            upperBodyTime,
            loop: false,
            rangeStartNormalized: 0f,
            rangeEndNormalized: 1f,
            upperBodyPose);

        float smoothWeight =
            upperBodyWeight * upperBodyWeight * (3f - 2f * upperBodyWeight);

        for (int nodeIndex = 0; nodeIndex < destination.Length; nodeIndex++)
        {
            if (!upperBodyMask[nodeIndex])
                continue;

            destination[nodeIndex] = BonePose.Blend(
                destination[nodeIndex],
                upperBodyPose[nodeIndex],
                smoothWeight);
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
        float clipDuration = sampler.GetClipDuration(clipName);
        float rangeStart = clipDuration * rangeStartNormalized;
        float rangeDuration = clipDuration *
            (rangeEndNormalized - rangeStartNormalized);

        float rangeTime = loop && rangeDuration > 0f
            ? elapsedSeconds % rangeDuration
            : Math.Min(elapsedSeconds, rangeDuration);

        sampler.SamplePose(
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

        bool isAttackLocomotionTransition =
            sourceState.Equals("Attack", StringComparison.OrdinalIgnoreCase) &&
            IsLocomotionState(targetState) ||
            targetState.Equals("Attack", StringComparison.OrdinalIgnoreCase) &&
            IsLocomotionState(sourceState);

        if (isAttackLocomotionTransition)
            return 0.08f;

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
