using System.Buffers.Binary;
using Microsoft.Xna.Framework.Audio;
using NLayer;

namespace _3DLight.Gameplay;

internal static class Mp3Footstep
{
    private const double MaxSeconds = 0.4;

    public static SoundEffect Load(string path)
    {
        using var mp3 = new MpegFile(path);
        int channels = mp3.Channels;
        int maxFrames = (int)(mp3.SampleRate * MaxSeconds);
        var samples = new float[maxFrames * channels];
        int count = mp3.ReadSamples(samples, 0, samples.Length);
        if (count == 0)
            throw new InvalidDataException($"В MP3 шагов нет звуковых данных: {path}");

        if (mp3.Duration.TotalSeconds > MaxSeconds)
            FadeOut(samples.AsSpan(0, count), mp3.SampleRate / 100 * channels);

        var pcm = new byte[count * sizeof(short)];
        for (int index = 0; index < count; index++)
        {
            short sample = (short)MathF.Round(Math.Clamp(samples[index], -1f, 1f) * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(index * sizeof(short)), sample);
        }

        AudioChannels layout = channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo;
        return new SoundEffect(pcm, mp3.SampleRate, layout);
    }

    private static void FadeOut(Span<float> samples, int fadeSamples)
    {
        Span<float> tail = samples[^Math.Min(fadeSamples, samples.Length)..];
        for (int index = 0; index < tail.Length; index++)
            tail[index] *= (tail.Length - 1 - index) / (float)tail.Length;
    }
}
