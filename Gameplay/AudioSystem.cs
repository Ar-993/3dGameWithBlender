using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace _3DLight.Gameplay;

public sealed class AudioSystem : IDisposable
{
    private readonly Dictionary<string, SoundEffect> effects =
        new(StringComparer.OrdinalIgnoreCase);

    private Song? music;

    [JsonInclude]
    public float MasterVolume { get; private set; } = 0.8f;

    [JsonInclude]
    public float MusicVolume { get; private set; } = 0.25f;

    [JsonInclude]
    public float EffectsVolume { get; private set; } = 0.65f;

    public static AudioSystem Load(string directory)
    {
        AudioSystem audio = ReadSettings(directory);

        try
        {
            audio.LoadEffects(directory);
            audio.StartMusic(directory);
            return audio;
        }
        catch (NoAudioHardwareException)
        {
            audio.Dispose();
            return audio;
        }
        catch
        {
            audio.Dispose();
            throw;
        }
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Math.Clamp(value, 0f, 1f);
        ApplyMusicVolume();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Math.Clamp(value, 0f, 1f);
        ApplyMusicVolume();
    }

    public void SetEffectsVolume(float value)
    {
        EffectsVolume = Math.Clamp(value, 0f, 1f);
    }

    public void Play(string name)
        => Play(effects.GetValueOrDefault(name));

    internal void PlayFootstep(string prefix, int variant)
        => Play(FindFootstep(prefix, variant) ??
            FindFootstep("step_default", variant) ??
            FindFootstep("step_asphalt", variant));

    private SoundEffect? FindFootstep(string prefix, int variant)
    {
        if (effects.TryGetValue($"{prefix}_{variant}", out SoundEffect? selected))
            return selected;

        int alternateVariant = variant == 1 ? 2 : 1;
        if (effects.TryGetValue($"{prefix}_{alternateVariant}", out SoundEffect? alternate))
            return alternate;

        return effects.GetValueOrDefault(prefix);
    }

    private void Play(SoundEffect? effect)
    {
        float volume = MasterVolume * EffectsVolume;
        if (volume > 0f)
            effect?.Play(volume, pitch: 0f, pan: 0f);
    }

    private void LoadEffects(string directory)
    {
        foreach (string path in Directory.EnumerateFiles(directory, "step_*.mp3"))
            effects.Add(Path.GetFileNameWithoutExtension(path), Mp3Footstep.Load(path));

        foreach (string path in Directory.EnumerateFiles(directory, "*.wav"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.Equals("music", StringComparison.OrdinalIgnoreCase) || effects.ContainsKey(name))
                continue;

            effects.Add(name, LoadWave(path));
        }
    }

    private void StartMusic(string directory)
    {
        string path = Path.GetFullPath(Path.Combine(directory, "Surveillance.ogg"));

        music = Song.FromUri("Surveillance", new Uri(path));
        MediaPlayer.IsRepeating = true;
        ApplyMusicVolume();
        MediaPlayer.Play(music);
    }

    private void ApplyMusicVolume()
    {
        MediaPlayer.Volume = MasterVolume * MusicVolume;
    }


    private static SoundEffect LoadWave(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SoundEffect.FromStream(stream);
    }

    private static AudioSystem ReadSettings(string directory)
    {
        string path = Path.Combine(directory, "audio.json");
        if (!File.Exists(path))
            return new AudioSystem();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<AudioSystem>(
            File.ReadAllText(path), options) ?? new AudioSystem();
    }

    public void Dispose()
    {
        if (music is not null)
        {
            MediaPlayer.Stop();
            music.Dispose();
            music = null;
        }

        foreach (SoundEffect effect in effects.Values)
            effect.Dispose();

        effects.Clear();
    }
}
