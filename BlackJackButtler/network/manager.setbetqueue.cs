using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static class SetBetQueueManager
{
    private static int _pendingCount;

    public static bool IsProcessing => Volatile.Read(ref _pendingCount) > 0;
    public static int Count => Math.Max(0, Volatile.Read(ref _pendingCount));

    public static void Enqueue(PlayerState player, string rawBetAmount, Configuration config, string source)
    {
        var playerName = player.Name;
        var displayName = player.DisplayName;
        Interlocked.Increment(ref _pendingCount);
        GameActionQueueManager.Enqueue(
            $"SetBet:{displayName}",
            () => Execute(playerName, displayName, rawBetAmount, config, source),
            null,
            null,
            () => Interlocked.Decrement(ref _pendingCount));
    }

    private static async Task Execute(
        string playerName,
        string displayName,
        string rawBetAmount,
        Configuration config,
        string source)
    {
        var window = Plugin.Instance.GetMainWindow();
        var player = window.GetPlayers().FirstOrDefault(p =>
            p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase)
            || p.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));

        if (player == null)
        {
            window.AddDebugLog($"[SetBetQueue] Skipped missing player '{displayName}' from {source}");
            return;
        }

        var normalized = await TryNormalizeBetAmountAsync(rawBetAmount, player, config);
        if (!normalized.Success)
        {
            window.AddDebugLog($"[SetBetQueue] Skipped invalid bet '{rawBetAmount}' for {player.DisplayName}");
            return;
        }

                var normalizedBet = normalized.Amount;
                var normalizeReason = normalized.Reason;
                if (!string.IsNullOrWhiteSpace(normalizeReason))
                    window.AddDebugLog($"[SetBetQueue] Normalized bet for {player.DisplayName}: {normalizeReason}");

                var oldBet = player.CurrentBet;
                var changed = oldBet != normalizedBet;
                if (oldBet == normalizedBet)
                {
                    window.AddDebugLog($"[SetBetQueue] Bet unchanged for {player.DisplayName}: {normalizedBet:N0}");
                }
                else
                {
                    player.CurrentBet = normalizedBet;
                    player.HighlightBet = false;
                    ActivityLogManager.LogBetSet(player.DisplayName, player.CurrentBet);
                    CompanionSyncManager.SendPlayerBankBetUpdate(config, player);
                    SessionManager.SaveSession(window.GetPlayers(), window.GetDealer(), GameEngine.CurrentPhase, window.IsRecognitionActive);
                    window.AddDebugLog($"[SetBetQueue] Set bet for {player.DisplayName}: {oldBet:N0} -> {player.CurrentBet:N0}");
                }

        if (!changed)
            return;

        var postCommandName = ResolvePostCommandName(config);
        if (postCommandName == null)
            return;

        await Task.Delay(TimeSpan.FromSeconds(0.2));

        GameEngine.TargetPlayer(player);
        VariableManager.SetPlayerVariables(player);
        await CommandExecutor.ExecuteGroup(postCommandName, player.DisplayName, config);

        var dealerName = window.GetDealer().Name;
        if (!string.IsNullOrWhiteSpace(dealerName))
            GameEngine.TargetPlayer(dealerName);
    }

    private static string? ResolvePostCommandName(Configuration config)
    {
        if (string.IsNullOrWhiteSpace(config.AutoBetPostCommandName))
            return null;

        if (config.AutoBetPostCommandName.Equals("Payout", StringComparison.OrdinalIgnoreCase))
            return "Payout";

        return config.CommandGroups
            .Concat(config.CustomCommandGroups)
            .FirstOrDefault(g => g.Name.Equals(config.AutoBetPostCommandName, StringComparison.OrdinalIgnoreCase)
                && g.IsActive
                && !IsSetBetLoopbackName(g.Name))
            ?.Name;
    }

    public static bool IsSetBetLoopbackName(string name)
    {
        return name.Equals("SetBet", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Set Bet", StringComparison.OrdinalIgnoreCase)
            || name.Equals("BetChange", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Bet Change", StringComparison.OrdinalIgnoreCase);
    }

    private static Task<(bool Success, long Amount, string Reason)> TryNormalizeBetAmountAsync(string raw, PlayerState player, Configuration config)
    {
        var tcs = new TaskCompletionSource<(bool Success, long Amount, string Reason)>(TaskCreationOptions.RunContinuationsAsynchronously);

        Plugin.Framework.RunOnTick(() =>
        {
            try
            {
                var success = TryNormalizeBetAmount(raw, player, config, out var amount, out var reason);
                tcs.TrySetResult((success, amount, reason));
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[SetBetQueue] Normalize failed: {ex}");
                tcs.TrySetResult((false, 0, string.Empty));
            }
        });

        return tcs.Task;
    }

    public static bool TryNormalizeBetAmount(string raw, PlayerState player, Configuration config, out long amount, out string reason)
    {
        amount = 0;
        reason = string.Empty;

        var text = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var maxAllowed = Math.Max(0, Math.Min(player.GetEffectiveMaxBet(config), player.Bank));
        var minAllowed = config.MinBet;
        // effectiveUpper >= minAllowed so Math.Clamp never throws when min > max
        var effectiveUpper = Math.Max(maxAllowed, minAllowed);

        if (text is "max" or "all" or "full")
        {
            amount = effectiveUpper;
            reason = $"{raw} -> max allowed {amount:N0}";
            return true;
        }

        if (text == "min")
        {
            amount = minAllowed;
            reason = $"{raw} -> min bet {amount:N0}";
            return true;
        }

        var multiplier = 1L;
        var hasSuffix = false;
        if (text.EndsWith("k", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1_000L;
            hasSuffix = true;
            text = text[..^1].Trim();
        }
        else if (text.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1_000_000L;
            hasSuffix = true;
            text = text[..^1].Trim();
        }

        if (!TryParseBetNumber(text, hasSuffix, out var parsed))
            return false;

        var original = parsed;
        amount = SafeMultiply(parsed, multiplier);

        if (multiplier == 1L && amount < minAllowed)
        {
            amount = SafeMultiply(original, 1_000L);
            reason = $"{raw} below min, interpreted as {amount:N0}";
        }

        var unclamped = amount;
        // minAllowed is always the lower bound — regex bet changes never go below MinBet
        amount = Math.Clamp(amount, minAllowed, effectiveUpper);
        if (amount != unclamped)
        {
            reason = string.IsNullOrWhiteSpace(reason)
                ? $"{raw} clamped to allowed range: {amount:N0}"
                : $"{reason}, clamped to {amount:N0}";
        }

        return true;
    }

    private static bool TryParseBetNumber(string text, bool hasSuffix, out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!hasSuffix)
        {
            var digits = new string(text.Where(char.IsDigit).ToArray());
            return !string.IsNullOrWhiteSpace(digits)
                && decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        var normalized = NormalizeSuffixedNumber(text);
        return !string.IsNullOrWhiteSpace(normalized)
            && decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static string NormalizeSuffixedNumber(string text)
    {
        var token = new string(text.Where(ch => char.IsDigit(ch) || ch == '.' || ch == ',').ToArray());
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        var lastDot = token.LastIndexOf('.');
        var lastComma = token.LastIndexOf(',');
        var separator = lastDot > lastComma ? '.' : ',';
        var separatorIndex = Math.Max(lastDot, lastComma);

        if (separatorIndex < 0)
            return new string(token.Where(char.IsDigit).ToArray());

        var hasMixedSeparators = lastDot >= 0 && lastComma >= 0;
        var fractionLength = token.Length - separatorIndex - 1;
        var treatAsDecimal = hasMixedSeparators || fractionLength is > 0 and <= 2;

        if (!treatAsDecimal)
            return new string(token.Where(char.IsDigit).ToArray());

        var whole = new string(token[..separatorIndex].Where(char.IsDigit).ToArray());
        var fraction = new string(token[(separatorIndex + 1)..].Where(char.IsDigit).ToArray());

        return string.IsNullOrEmpty(whole)
            ? $"0.{fraction}"
            : $"{whole}.{fraction}";
    }

    private static long SafeMultiply(decimal value, long multiplier)
    {
        try
        {
            var result = value * multiplier;
            return result > long.MaxValue
                ? long.MaxValue
                : decimal.ToInt64(decimal.Truncate(result));
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }
}
