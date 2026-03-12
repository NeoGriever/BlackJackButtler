using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;
using NAudio.Vorbis;

namespace BlackJackButtler;

public static class NearbyAlertManager
{
    private static HashSet<string> _previousInRange = new();
    private static DateTime _lastSoundPlayed = DateTime.MinValue;
    private static WaveOutEvent? _activePlayer;
    private static IWaveProvider? _activeReader;
    private static readonly Random _rng = new();

    public static void Update(List<NearbyPlayerInfo> current, Configuration config)
    {
        if (!config.NearbyAlertEnabled || config.NearbyAlertSoundFiles.Count == 0)
        {
            var currentKeys = new HashSet<string>(current
                .Where(p => p.Distance <= config.NearbyDistanceCap)
                .Select(p => p.FullKey));
            _previousInRange = currentKeys;
            return;
        }

        var inRange = new HashSet<string>(current
            .Where(p => p.Distance <= config.NearbyDistanceCap)
            .Select(p => p.FullKey));

        bool hasNew = false;
        foreach (var key in inRange)
        {
            if (!_previousInRange.Contains(key))
            {
                hasNew = true;
                break;
            }
        }

        if (hasNew && (DateTime.Now - _lastSoundPlayed).TotalSeconds >= config.NearbyAlertCooldown)
            PlayRandomSound(config);

        _previousInRange = inRange;
    }

    public static void PlayRandomSound(Configuration config)
    {
        if (config.NearbyAlertSoundFiles.Count == 0) return;

        var valid = config.NearbyAlertSoundFiles.Where(File.Exists).ToList();
        if (valid.Count == 0) return;

        var path = valid[_rng.Next(valid.Count)];
        PlayFile(path, config.NearbyAlertVolume);
    }

    public static void PlayTestSound(Configuration config)
    {
        PlayRandomSound(config);
    }

    private static void PlayFile(string path, float volumePercent)
    {
        try
        {
            StopActive();

            var ext = Path.GetExtension(path).ToLowerInvariant();
            WaveStream stream;

            if (ext == ".ogg")
                stream = new VorbisWaveReader(path);
            else
                stream = new MediaFoundationReader(path);

            _activeReader = stream;
            var volumeProvider = new VolumeWaveProvider(stream, volumePercent / 100f);

            var player = new WaveOutEvent();
            player.Init(volumeProvider);
            player.PlaybackStopped += (_, _) =>
            {
                player.Dispose();
                if (_activeReader is IDisposable d) d.Dispose();
            };
            _activePlayer = player;
            player.Play();
            _lastSoundPlayed = DateTime.Now;
        }
        catch { }
    }

    public static void Reset()
    {
        _previousInRange.Clear();
    }

    public static void Dispose()
    {
        StopActive();
    }

    private static void StopActive()
    {
        try
        {
            if (_activePlayer != null)
            {
                _activePlayer.Stop();
                _activePlayer.Dispose();
                _activePlayer = null;
            }
            if (_activeReader is IDisposable d)
            {
                d.Dispose();
                _activeReader = null;
            }
        }
        catch { }
    }
}

internal sealed class VolumeWaveProvider : IWaveProvider
{
    private readonly IWaveProvider _source;
    private float _volume;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public VolumeWaveProvider(IWaveProvider source, float volume)
    {
        _source = source;
        _volume = Math.Clamp(volume, 0f, 1f);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (Math.Abs(_volume - 1f) < 0.001f) return read;

        int bytesPerSample = WaveFormat.BitsPerSample / 8;
        if (bytesPerSample == 4)
        {
            for (int i = offset; i < offset + read; i += 4)
            {
                float sample = BitConverter.ToSingle(buffer, i);
                sample *= _volume;
                BitConverter.TryWriteBytes(buffer.AsSpan(i), sample);
            }
        }
        else if (bytesPerSample == 2)
        {
            for (int i = offset; i < offset + read; i += 2)
            {
                short sample = BitConverter.ToInt16(buffer, i);
                sample = (short)(sample * _volume);
                BitConverter.TryWriteBytes(buffer.AsSpan(i), sample);
            }
        }

        return read;
    }
}
