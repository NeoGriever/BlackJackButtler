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
    private static WaveStream? _activeStream;
    private static readonly Random _rng = new();
    private static readonly object _lock = new();
    private static int _iterativeIndex;

    public static void Update(List<NearbyPlayerInfo> current, Configuration config)
    {
        if (!config.NearbyAlertEnabled || !GetEnabledEntries(config).Any())
        {
            var currentKeys = new HashSet<string>(current
                .Where(p => p.IsInRange)
                .Select(p => p.FullKey));
            _previousInRange = currentKeys;
            return;
        }

        var inRange = new HashSet<string>(current
            .Where(p => p.IsInRange)
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
        var valid = GetEnabledEntries(config).Where(entry => File.Exists(entry.Path)).ToList();
        if (valid.Count == 0) return;

        var sound = config.NearbyAlertSoundMode switch
        {
            NearbyAlertSoundMode.FirstOnly => valid[0],
            NearbyAlertSoundMode.Iterative => valid[_iterativeIndex++ % valid.Count],
            _ => valid[_rng.Next(valid.Count)]
        };
        PlayFile(sound.Path, config.NearbyAlertVolume * sound.Volume / 100f, out _);
    }

    public static bool PlayTestSound(Configuration config, out string status)
    {
        var enabled = GetEnabledEntries(config).ToList();
        if (enabled.Count == 0)
        {
            status = "No enabled sound file is configured.";
            return false;
        }

        var valid = enabled.Where(entry => File.Exists(entry.Path)).ToList();
        if (valid.Count == 0)
        {
            status = "No enabled sound file exists at its configured path.";
            return false;
        }

        var sound = config.NearbyAlertSoundMode switch
        {
            NearbyAlertSoundMode.FirstOnly => valid[0],
            NearbyAlertSoundMode.Iterative => valid[_iterativeIndex++ % valid.Count],
            _ => valid[_rng.Next(valid.Count)]
        };
        var volume = config.NearbyAlertVolume * sound.Volume / 100f;
        return PlayFile(sound.Path, volume, out status);
    }

    private static IEnumerable<NearbyAlertSoundEntry> GetEnabledEntries(Configuration config)
    {
        if (config.NearbyAlertSoundEntriesMigrated)
            return config.NearbyAlertSoundEntries.Where(entry => entry.Enabled && !string.IsNullOrWhiteSpace(entry.Path));

        // Defensive fallback while a configuration is being loaded or imported.
        return config.NearbyAlertSoundFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => new NearbyAlertSoundEntry { Path = path });
    }

    private static bool PlayFile(string path, float volumePercent, out string status)
    {
        try
        {
            StopActive();

            var ext = Path.GetExtension(path).ToLowerInvariant();
            WaveStream stream;

            if (ext == ".ogg")
                stream = new VorbisWaveReader(path);
            else if (ext == ".mp3")
                stream = new Mp3FileReader(path);
            else
                stream = new MediaFoundationReader(path);

            _activeStream = stream;
            var volumeProvider = new VolumeWaveProvider(stream, volumePercent / 100f);

            var player = new WaveOutEvent();
            player.Init(volumeProvider);
            player.PlaybackStopped += (_, _) =>
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_activePlayer, player))
                    {
                        _activePlayer = null;
                        _activeStream?.Dispose();
                        _activeStream = null;
                        player.Dispose();
                    }
                }
            };
            _activePlayer = player;
            player.Play();
            _lastSoundPlayed = DateTime.Now;
            status = $"Playing {Path.GetFileName(path)} at {volumePercent:0}% volume.";
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"[NearbyAlert] Failed to play sound '{path}': {ex.Message}");
            StopActive();
            status = $"Could not play {Path.GetFileName(path)}: {ex.Message}";
            return false;
        }
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
        lock (_lock)
        {
            try
            {
                var player = _activePlayer;
                _activePlayer = null;

                if (player != null)
                {
                    player.Stop();
                    player.Dispose();
                }

                _activeStream?.Dispose();
                _activeStream = null;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning($"[NearbyAlert] Failed to stop active sound: {ex.Message}");
            }
        }
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
