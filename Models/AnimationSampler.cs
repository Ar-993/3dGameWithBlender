using _3DLight.Assets;
using Microsoft.Xna.Framework;
using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsQuaternion = System.Numerics.Quaternion;

namespace _3DLight.Models;

internal sealed class AnimationSampler
{
    private readonly Dictionary<string, ClipData> clipsByName;
    private readonly BonePose[] bindLocalPoses;

    public int NodeCount { get; }
    public IEnumerable<string> ClipNames => clipsByName.Keys;

    public AnimationSampler(
        IReadOnlyList<NodeData> nodes,
        IEnumerable<ClipData> clips)
    {
        NodeCount = nodes.Count;
        clipsByName = clips.ToDictionary(
            clip => clip.Name,
            StringComparer.OrdinalIgnoreCase);

        // Static models may have bind matrices that cannot be decomposed into TRS.
        bindLocalPoses = clipsByName.Count == 0
            ? []
            : nodes.Select(node => BonePose.FromMatrix(
                ToXnaMatrix(node.Bind),
                node.Name)).ToArray();
    }

    public float GetClipDuration(string clipName)
    {
        if (!clipsByName.TryGetValue(clipName, out ClipData? clip))
            throw new InvalidOperationException($"Анимация '{clipName}' отсутствует.");

        return (float)(clip.Duration / clip.TicksPerSecond);
    }

    public void SamplePose(
        string clipName,
        float elapsedSeconds,
        bool loop,
        BonePose[] destination)
    {
        if (!clipsByName.TryGetValue(clipName, out ClipData? clip))
            throw new InvalidOperationException($"Анимация '{clipName}' отсутствует.");

        ValidatePoseLength(destination);
        Array.Copy(bindLocalPoses, destination, bindLocalPoses.Length);

        double animationTick = CalculateAnimationTick(
            clip,
            elapsedSeconds,
            loop);

        foreach (ChannelData channel in clip.Channels)
        {
            BonePose bindPose = bindLocalPoses[channel.Node];

            Vector3 position = InterpolateVectorKeys(
                channel.Positions,
                animationTick,
                bindPose.Position);

            Quaternion rotation = InterpolateQuaternionKeys(
                channel.Rotations,
                animationTick,
                bindPose.Rotation);

            Vector3 scale = InterpolateVectorKeys(
                channel.Scales,
                animationTick,
                bindPose.Scale);

            destination[channel.Node] = new BonePose(position, rotation, scale);
        }
    }

    private void ValidatePoseLength(BonePose[] pose)
    {
        if (pose.Length != NodeCount)
        {
            throw new ArgumentException(
                $"Поза содержит {pose.Length} узлов вместо {NodeCount}.",
                nameof(pose));
        }
    }

    private static double CalculateAnimationTick(
        ClipData clip,
        float elapsedSeconds,
        bool loop)
    {
        double animationTick = elapsedSeconds * clip.TicksPerSecond;

        if (loop && clip.Duration > 0)
            return animationTick % clip.Duration;

        return Math.Min(animationTick, clip.Duration);
    }

    private static Vector3 InterpolateVectorKeys(
        VectorKey[] keys,
        double animationTick,
        Vector3 fallbackValue)
    {
        if (keys.Length == 0)
            return fallbackValue;

        int firstKeyIndex = FindKeyIndex(keys, animationTick, key => key.Time);

        if (firstKeyIndex == keys.Length - 1)
            return ToXnaVector3(keys[firstKeyIndex].Value);

        VectorKey firstKey = keys[firstKeyIndex];
        VectorKey secondKey = keys[firstKeyIndex + 1];
        float interpolationAmount = CalculateInterpolationAmount(
            animationTick,
            firstKey.Time,
            secondKey.Time);

        return Vector3.Lerp(
            ToXnaVector3(firstKey.Value),
            ToXnaVector3(secondKey.Value),
            interpolationAmount);
    }

    private static Quaternion InterpolateQuaternionKeys(
        QuaternionKey[] keys,
        double animationTick,
        Quaternion fallbackValue)
    {
        if (keys.Length == 0)
            return fallbackValue;

        int firstKeyIndex = FindKeyIndex(keys, animationTick, key => key.Time);

        if (firstKeyIndex == keys.Length - 1)
            return ToXnaQuaternion(keys[firstKeyIndex].Value);

        QuaternionKey firstKey = keys[firstKeyIndex];
        QuaternionKey secondKey = keys[firstKeyIndex + 1];
        float interpolationAmount = CalculateInterpolationAmount(
            animationTick,
            firstKey.Time,
            secondKey.Time);

        Quaternion interpolatedRotation = Quaternion.Slerp(
            ToXnaQuaternion(firstKey.Value),
            ToXnaQuaternion(secondKey.Value),
            interpolationAmount);

        return Quaternion.Normalize(interpolatedRotation);
    }

    private static float CalculateInterpolationAmount(
        double currentTime,
        double firstKeyTime,
        double secondKeyTime)
    {
        double interval = Math.Max(secondKeyTime - firstKeyTime, double.Epsilon);
        double amount = (currentTime - firstKeyTime) / interval;
        return MathHelper.Clamp((float)amount, 0f, 1f);
    }

    private static int FindKeyIndex<T>(
        T[] keys,
        double animationTick,
        Func<T, double> getKeyTime)
    {
        int lowerIndex = 0;
        int upperIndex = keys.Length - 1;

        while (lowerIndex < upperIndex)
        {
            int middleIndex = (lowerIndex + upperIndex + 1) / 2;

            if (getKeyTime(keys[middleIndex]) <= animationTick)
                lowerIndex = middleIndex;
            else
                upperIndex = middleIndex - 1;
        }

        return lowerIndex;
    }

    private static Matrix ToXnaMatrix(NumericsMatrix matrix) => new(
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44);

    private static Vector3 ToXnaVector3(System.Numerics.Vector3 vector) =>
        new(vector.X, vector.Y, vector.Z);

    private static Quaternion ToXnaQuaternion(NumericsQuaternion quaternion) =>
        new(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);

}
