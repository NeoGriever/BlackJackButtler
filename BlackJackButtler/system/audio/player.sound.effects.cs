using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BlackJackButtler.Chat;
using BlackJackButtler.Regex;

namespace BlackJackButtler;

internal static class PlayerSoundEffects
{
    public const int MaxSoundId = 16;

    public static bool IsAllowed(Configuration config, int soundId)
    {
        if (soundId is < 1 or > MaxSoundId)
            return false;

        var allowed = config.AllowedPlayerSoundEffects;
        return allowed == null || soundId > allowed.Length || allowed[soundId - 1];
    }

    public static int NextAllowedSound(Configuration config, int current, int direction)
    {
        var options = Enumerable.Range(1, MaxSoundId)
            .Where(sound => IsAllowed(config, sound))
            .Prepend(0)
            .ToArray();
        var index = Array.IndexOf(options, current);
        if (index < 0)
        {
            var next = direction > 0 ? options.FirstOrDefault(sound => sound > current, -1) : options.LastOrDefault(sound => sound < current, -1);
            if (next >= 0)
                return next;
            return direction > 0 ? options[0] : options[^1];
        }
        return options[(index + (direction > 0 ? 1 : options.Length - 1)) % options.Length];
    }

    public static string GetTag(Configuration config, PlayerState? player)
    {
        if (player != null)
            ApplySavedPreference(config, player);
        if (!config.EnablePlayerSoundEffects || player == null || !IsAllowed(config, player.SoundEffectId))
            return string.Empty;
        return $" <se.{player.SoundEffectId}>";
    }

    public static void ApplySavedPreference(Configuration config, PlayerState player)
    {
        if (config.PlayerSoundPreferences != null
            && config.PlayerSoundPreferences.TryGetValue(GetPreferenceKey(player), out var soundId)
            && soundId is >= 0 and <= MaxSoundId)
            player.SoundEffectId = soundId;
    }

    public static void SetPreference(Configuration config, PlayerState player, int soundId)
    {
        player.SoundEffectId = soundId;
        config.PlayerSoundPreferences ??= new(StringComparer.OrdinalIgnoreCase);
        config.PlayerSoundPreferences[GetPreferenceKey(player)] = soundId;
    }

    private static string GetPreferenceKey(PlayerState player)
        => $"{player.Name}@{player.WorldId.ToString(CultureInfo.InvariantCulture)}";

    public static void Preview(int soundId)
    {
        if (soundId is < 1 or > MaxSoundId)
            return;

        ECommons.DalamudServices.Svc.Framework.RunOnTick(
            () => ECommons.Automation.Chat.SendMessage($"/echo <se.{soundId}>")
        );
    }

    public static void ExecuteSetSound(
        string requestedSound,
        ParsedChatMessage message,
        Configuration config,
        List<PlayerState> players,
        PlayerState? sourcePlayer
    )
    {
        var displayValue = string.IsNullOrWhiteSpace(requestedSound) ? "?" : requestedSound.Trim();
        var succeeds = false;
        var soundId = -1;

        if (config.EnablePlayerSoundEffects
            && int.TryParse(requestedSound, NumberStyles.None, CultureInfo.InvariantCulture, out soundId)
            && soundId is >= 0 and <= MaxSoundId
            && (soundId == 0 || IsAllowed(config, soundId)))
        {
            displayValue = soundId.ToString(CultureInfo.InvariantCulture);
            var target = sourcePlayer == null ? null : PlayerIdentityManager.ResolveMessageActionPlayer(players, sourcePlayer);
            if (target != null)
            {
                SetPreference(config, target, soundId);
                config.Save();
                Plugin.Instance.GetMainWindow().SaveSessionFromUI();
                succeeds = true;
            }
        }

        var batchName = succeeds ? "Sound confirm" : "Sound failed";
        var batch = config.MessageBatches.FirstOrDefault(
            candidate =>
                candidate.Name.Equals(batchName, StringComparison.OrdinalIgnoreCase)
        );
        var reply = batch?.GetNextMessage();
        if (string.IsNullOrWhiteSpace(reply))
            reply = succeeds ? "Your sound is set to ${snd}." : "Sound ${snd} is invalid or unavailable.";
        reply = reply.Replace("${snd}", displayValue, StringComparison.OrdinalIgnoreCase);

        var tellTarget = sourcePlayer != null ? GetPlayerTellTarget(sourcePlayer) : GetMessageTellTarget(message);
        if (string.IsNullOrWhiteSpace(tellTarget))
            return;

        ChatCommandRouter.Send($"/tell {tellTarget} {reply}", config, $"SetSound:{message.Name}:{displayValue}");
    }

    private static string GetMessageTellTarget(ParsedChatMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Name))
            return string.Empty;
        if (message.WorldId <= 0)
            return message.Name;
        var world = VipManager.ResolveWorldName((uint)message.WorldId);
        return BuildTellTarget(message.Name, world);
    }

    private static string GetPlayerTellTarget(PlayerState player)
        => BuildTellTarget(
            player.Name,
            VipManager.ResolveWorldName(player.WorldId) is { Length: > 0 } world ? world : PlayerIdentityManager.GetQualifiedName(player).Contains('@', StringComparison.Ordinal) ? PlayerIdentityManager.GetQualifiedName(player).Split('@', 2)[1] : string.Empty
        );

    private static string BuildTellTarget(string name, string world)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('@', StringComparison.Ordinal))
            return name;
        return string.IsNullOrWhiteSpace(world) ? name : $"{name}@{world}";
    }
}
