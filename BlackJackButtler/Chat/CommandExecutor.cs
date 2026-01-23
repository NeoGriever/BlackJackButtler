using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using RRX = System.Text.RegularExpressions;
using System.Globalization;

namespace BlackJackButtler;

public static class CommandExecutor
{
    private static readonly RRX.Regex StackTokenRegex = new(@"#\{([^}]+)\}", RRX.RegexOptions.Compiled);
    private static readonly RRX.Regex DicePartyRegex = new(@"^/dice\s+party\s+(\d+)\s*$", RRX.RegexOptions.Compiled | RRX.RegexOptions.IgnoreCase);

    private static string ReplacePlayerScoreFirst(string text)
    {
        if (!text.Contains("+{PlayerScore}", StringComparison.Ordinal))
            return text;

        if (!GameEngine.TryGetBestScoreForCurrentTarget(out var score))
            return text.Replace("+{PlayerScore}", string.Empty);

        return text.Replace("+{PlayerScore}", score.ToString(CultureInfo.InvariantCulture));
    }

    private static string ReplaceMessageStacks(string text, Configuration cfg, string targetPlayerName)
    {
        return StackTokenRegex.Replace(text, m =>
        {
            var stackName = m.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(stackName))
                return string.Empty;

            var batch = cfg.MessageBatches
                .FirstOrDefault(b => b.Name.Equals(stackName, StringComparison.OrdinalIgnoreCase));

            if (batch == null)
                return string.Empty;

            var msg = batch.GetNextMessage() ?? string.Empty;

            msg = msg.Replace("<t>", targetPlayerName);
            msg = ReplacePlayerScoreFirst(msg);

            msg = VariableManager.ProcessMessage(msg);

            return msg;
        });
    }

    public static async Task ExecuteGroup(string groupName, string targetPlayerName, Configuration cfg)
    {
        var group = cfg.CommandGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        if (group == null) return;

        foreach (var cmd in group.Commands)
        {
            if (!cmd.Enabled || string.IsNullOrWhiteSpace(cmd.Text)) continue;

            string processedText = cmd.Text.Replace("<t>", targetPlayerName);

            processedText = ReplacePlayerScoreFirst(processedText);
            processedText = ReplaceMessageStacks(processedText, cfg, targetPlayerName);
            processedText = VariableManager.ProcessMessage(processedText);

            ChatCommandRouter.Send(processedText, cfg, $"{groupName}:{targetPlayerName}");

            if (cmd.Delay > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(cmd.Delay));
            }
        }
    }

    private static string ResolveCommandText(string text, string targetPlayerName, Configuration cfg)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = text.Replace("<t>", targetPlayerName);

        if (text.Contains("+{PlayerScore}", StringComparison.Ordinal))
        {
            if (GameEngine.TryGetBestScoreForCurrentTarget(out var best))
                text = text.Replace("+{PlayerScore}", best.ToString(CultureInfo.InvariantCulture));
            else
                text = text.Replace("+{PlayerScore}", string.Empty);
        }

        text = StackTokenRegex.Replace(text, m =>
        {
            var stackName = m.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(stackName))
                return string.Empty;

            var batch = cfg.MessageBatches
                .FirstOrDefault(b => b.Name.Equals(stackName, StringComparison.OrdinalIgnoreCase));

            if (batch == null)
                return string.Empty;

            var msg = batch.GetNextMessage() ?? string.Empty;

            msg = msg.Replace("<t>", targetPlayerName);

            if (msg.Contains("+{PlayerScore}", StringComparison.Ordinal))
            {
                if (GameEngine.TryGetBestScoreForCurrentTarget(out var best2))
                    msg = msg.Replace("+{PlayerScore}", best2.ToString(CultureInfo.InvariantCulture));
                else
                    msg = msg.Replace("+{PlayerScore}", string.Empty);
            }

            msg = VariableManager.ProcessMessage(msg);
            return msg;
        });

        text = VariableManager.ProcessMessage(text);

        return text;
    }

    private static bool TryHandleDebugDice(string processedText)
    {
        var m = DicePartyRegex.Match(processedText.Trim());
        if (!m.Success) return false;

        if (!int.TryParse(m.Groups[1].Value, out var sides) || sides <= 0)
            return true;

        var rolled = Random.Shared.Next(1, sides + 1);
        var card = (sides == 13) ? GameEngine.MapDice13ToCardValue(rolled) : rolled;

        Plugin.Log.Information($"[BJB][DebugOutput] {processedText} -> rolled={rolled}, cardValue={card}");

        GameEngine.TryApplyCardToCurrentTargetFromRuntime(card);
        return true;
    }
}
