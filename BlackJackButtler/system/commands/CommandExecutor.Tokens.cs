using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using RRX = System.Text.RegularExpressions;
using System.Globalization;

namespace BlackJackButtler;

public static partial class CommandExecutor
{
    private static string FormatGilAmount(long amount, bool shortFormat)
    {
        if (!shortFormat)
            return amount.ToString("N0", CultureInfo.GetCultureInfo("en-US")) + " Gil";

        if (amount >= 1_000_000)
        {
            double millions = amount / 1_000_000.0;
            return (millions == Math.Floor(millions)) ? $"{(long)millions}m" : $"{millions:0.#}m";
        }
        if (amount >= 1_000)
        {
            double thousands = amount / 1_000.0;
            return (thousands == Math.Floor(thousands)) ? $"{(long)thousands}k" : $"{thousands:0.#}k";
        }
        return amount.ToString();
    }

    private static string ProcessContextTokens(string text, PlayerState? pState, string targetName, Configuration cfg)
    {
        if (string.IsNullOrEmpty(text)) return text;

        string[] resVars = { "winners", "pushed", "loosers", "busted", "results" };
        var variableSnapshot = VariableManager.SnapshotForUi();
        foreach (var varName in resVars)
        {
            var v = variableSnapshot.FirstOrDefault(x => x.Name.Equals(varName, StringComparison.OrdinalIgnoreCase));
            if (v != null) text = text.Replace($"<{varName}>", v.Value);
        }

        var physicalPlayer = pState == null ? null : PlayerIdentityManager.GetReferencedPlayer(Plugin.Instance.GetMainWindow().GetPlayers(), pState) ?? pState;
        bool hasAlias = pState != null && !string.IsNullOrWhiteSpace(pState.Alias);
        string aliasOrT = hasAlias ? pState!.Alias! : targetName;
        string playerName = pState?.DisplayName ?? targetName;
        var trimmed = text.TrimStart();
        var soundTag = IsGroupChatCommand(trimmed) ? PlayerSoundEffects.GetTag(cfg, pState) : string.Empty;
        if (soundTag.Length > 0)
        {
            aliasOrT += soundTag;
            playerName += soundTag;
        }
        text = text.Replace("<n>", playerName);

        bool isTellCommand = trimmed.StartsWith("/tell ", StringComparison.OrdinalIgnoreCase)
                          || trimmed.StartsWith("/t ", StringComparison.OrdinalIgnoreCase);

        var targetToken = FindFirstTargetToken(text);
        if (isTellCommand && targetToken.index >= 0)
        {
            string currentTarget = GameEngine.GetCurrentTargetName();

            bool isCorrectlyTargeted = pState != null
                && !string.IsNullOrWhiteSpace(currentTarget)
                && (currentTarget.Equals(pState.Name, StringComparison.OrdinalIgnoreCase) || currentTarget.Equals(PlayerIdentityManager.GetQualifiedName(pState), StringComparison.OrdinalIgnoreCase));

            string firstReplacement;
            if (isCorrectlyTargeted)
            {
                firstReplacement = "<t>";
            }
            else
            {
                string realName = physicalPlayer?.Name ?? targetName;
                var qualifiedName = physicalPlayer != null ? PlayerIdentityManager.GetQualifiedName(physicalPlayer) : realName;
                firstReplacement = qualifiedName.Contains('@', StringComparison.Ordinal) ? qualifiedName : realName;
            }
            var after = text[(targetToken.index + targetToken.length)..].Replace("<t>", aliasOrT);
            text = string.Concat(text.AsSpan(0, targetToken.index), firstReplacement, after);
        }
        else
        {
            text = text.Replace("<t>", aliasOrT);
        }

        if (pState != null)
        {
            if (text.Contains("<points>"))
            {
                var (min, max) = pState.CalculatePoints(pState.CurrentHandIndex);
                text = text.Replace("<points>", max.HasValue ? $"{min}/{max}" : $"{min}");
            }

            string cardString = pState.GetCardsString(pState.CurrentHandIndex);
            text = text.Replace("<cards>", cardString);
            text = text.Replace("${playerCards}", cardString);
        }

        if (text.Contains("<dealerHand>"))
        {
            var dealer = Plugin.Instance.GetMainWindow().GetDealer();
            var dealerCards = dealer.Hands.Count > 0 ? dealer.GetCardsString(0) : string.Empty;
            text = text.Replace("<dealerHand>", dealerCards);
        }

        text = text.Replace("<minbet>", cfg.MinBet.ToString("N0", CultureInfo.GetCultureInfo("en-US")) + " Gil");
        text = text.Replace("<maxbet>", cfg.MaxBet.ToString("N0", CultureInfo.GetCultureInfo("en-US")) + " Gil");

        if (text.Contains("<betrange>"))
        {
            string min = FormatGilAmount(cfg.MinBet, cfg.ShortBetFormat);
            string max = FormatGilAmount(cfg.MaxBet, cfg.ShortBetFormat);
            string range = $"Min: {min} - Max: {max}";
            if (cfg.BetLimitEntries.Count > 0)
            {
                var tierParts = cfg.BetLimitEntries
                    .Where(e => e.Active && e.Kind == BetLimitEntryKind.Vip && e.VipLevel > 0)
                    .GroupBy(e => e.VipLevel)
                    .OrderBy(g => g.Key)
                    .Select(
                        g =>
                        {
                            var entry = g.OrderByDescending(e => e.Amount).First();
                            string name = string.IsNullOrWhiteSpace(entry.Name) ? $"VIP {entry.VipLevel}" : entry.Name;
                            return $"{name}: {FormatGilAmount(entry.Amount, cfg.ShortBetFormat)}";
                        }
                    );
                var parts = tierParts.ToList();
                if (parts.Count > 0)
                    range += $" ({string.Join(", ", parts)})";
            }
            else if (cfg.VipBetTiers.Count > 0)
            {
                var tierParts = cfg.VipBetTiers
                    .Select(t => $"{t.Name}: {FormatGilAmount(t.MaxBet, cfg.ShortBetFormat)}");
                range += $" ({string.Join(", ", tierParts)})";
            }
            text = text.Replace("<betrange>", range);
        }

        return text;
    }

    private static string ReplacePlayerScoreFirst(string text)
    {
        if (!text.Contains("+{PlayerScore}", StringComparison.Ordinal))
            return text;

        if (!GameEngine.TryGetBestScoreForCurrentTarget(out var score))
            return text.Replace("+{PlayerScore}", string.Empty);

        return text.Replace("+{PlayerScore}", score.ToString(CultureInfo.InvariantCulture));
    }

    private static string ReplaceMessageStacks(string text, Configuration cfg)
    {
        return StackTokenRegex.Replace(
            text,
            m =>
            {
                var stackName = m.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(stackName))
                    return string.Empty;

                var batch = cfg.MessageBatches
                    .FirstOrDefault(b => b.Name.Equals(stackName, StringComparison.OrdinalIgnoreCase));

                if (batch == null)
                    return string.Empty;

                return batch.GetNextMessage() ?? string.Empty;
            }
        );
    }
}
