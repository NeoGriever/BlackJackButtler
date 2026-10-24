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
    private static (int index, int length) FindFirstTargetToken(string text)
    {
        var tIndex = text.IndexOf("<t>", StringComparison.Ordinal);
        var protectedIndex = text.IndexOf("<.>", StringComparison.Ordinal);

        if (tIndex < 0)
            return protectedIndex < 0 ? (-1, 0) : (protectedIndex, 3);
        if (protectedIndex < 0 || tIndex < protectedIndex)
            return (tIndex, 3);
        return (protectedIndex, 3);
    }

    private static bool IsGroupChatCommand(string text)
        => text.StartsWith("/p ", StringComparison.OrdinalIgnoreCase)
           || text.StartsWith("/party ", StringComparison.OrdinalIgnoreCase)
           || text.StartsWith("/a ", StringComparison.OrdinalIgnoreCase)
           || text.StartsWith("/alliance ", StringComparison.OrdinalIgnoreCase);

    private static (bool execute, bool skipDelay, string command) EvaluateConditionalCommand(string text)
    {
        var trimmed = text.TrimStart();
        if (!trimmed.StartsWith("/if ", StringComparison.OrdinalIgnoreCase))
            return (true, false, text);

        var parts = text.Substring(text.IndexOf("/if ", StringComparison.OrdinalIgnoreCase) + 4)
                        .Split('|');

        if (parts.Length < 2)
            return (true, false, text);

        string conditionPart = parts[0].Trim();
        string commandPart = parts[1].Trim();
        bool hasSkip = parts.Length >= 3 && parts[2].Trim().Equals("skip", StringComparison.OrdinalIgnoreCase);

        var condTokens = conditionPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (condTokens.Length == 0)
            return (false, hasSkip, commandPart);

        string varValue = condTokens[0];

        if (condTokens.Length == 1)
        {
            bool notEmpty = !string.IsNullOrWhiteSpace(varValue);
            return (notEmpty, hasSkip && !notEmpty, commandPart);
        }

        string targetStr = condTokens[condTokens.Length - 1];

        string numStr = varValue.Contains('/') ? varValue.Split('/')[^1] : varValue;
        numStr = RRX.Regex.Replace(numStr, @"[^\d\.\-]", "");

        if (int.TryParse(numStr, out int actual) && int.TryParse(targetStr, out int target))
        {
            bool match = actual == target;
            return (match, hasSkip && !match, commandPart);
        }

        bool strMatch = varValue.Equals(targetStr, StringComparison.OrdinalIgnoreCase);
        return (strMatch, hasSkip && !strMatch, commandPart);
    }

    private static bool TryResolveSkipCommand(
        string text,
        PlayerState? pState,
        string targetPlayerName,
        Configuration cfg,
        out string command,
        out string resolvedCondition
    )
    {
        command = text;
        resolvedCondition = "(not a /skip command)";
        var trimmed = text.TrimStart();
        if (!trimmed.StartsWith("/skip ", StringComparison.OrdinalIgnoreCase))
            return false;

        var body = trimmed[6..];
        var separatorIndex = body.IndexOf('|');
        resolvedCondition = "(invalid /skip syntax)";
        if (separatorIndex < 0)
            return false;

        var condition = body[..separatorIndex].Trim();
        command = body[(separatorIndex + 1)..].Trim();

        condition = ProcessContextTokens(condition, pState, targetPlayerName, cfg);
        condition = ReplacePlayerScoreFirst(condition);
        condition = VariableManager.ProcessMessage(condition);
        resolvedCondition = condition;

        return string.IsNullOrWhiteSpace(condition);
    }

    private static bool TryHandleDebugDice(string processedText)
    {
        if (!Plugin.IsDebugMode)
            return false;

        var m = DicePartyRegex.Match(processedText.Trim());
        if (!m.Success) return false;

        if (!int.TryParse(m.Groups[1].Value, out var sides) || sides <= 0)
            return true;

        var sequenceIndexBefore = Plugin.DebugDiceSequenceIndex;
        var usedSequence = Plugin.TryGetNextDebugDiceRoll(sides, out var rolled);
        if (!usedSequence)
            rolled = Random.Shared.Next(1, sides + 1);
        var card = (sides == 13) ? GameEngine.MapDice13ToCardValue(rolled) : rolled;

        var resultText = $"Random! (1-{sides}) {rolled}";
        if (usedSequence)
            Plugin.Instance.GetMainWindow().AddDebugLog($"[DebugDice] Sequence roll #{sequenceIndexBefore + 1}: {rolled}");
        Plugin.Instance.GetMainWindow().AddDebugLog($"SYSTEM: {resultText}", isChat: true);
        StatsLogManager.AppendDiceResult(resultText);
        var window = Plugin.Instance.GetMainWindow();
        DiceResultHandler.HandleDiceResult(card, Plugin.Instance.Configuration, window.GetPlayers(), window.GetDealer());
        return true;
    }
}
