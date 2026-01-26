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
    private static bool _isRunning = false;
    public static bool IsRunning => _isRunning;

    private static string ProcessContextTokens(string text, PlayerState? pState, string targetName)
    {
        if (string.IsNullOrEmpty(text)) return text;

        string[] resVars = { "winners", "pushed", "loosers", "busted", "results" };
        foreach (var varName in resVars)
        {
            var v = VariableManager.Variables.FirstOrDefault(x => x.Name.Equals(varName, StringComparison.OrdinalIgnoreCase));
            if (v != null) text = text.Replace($"<{varName}>", v.Value);
        }

        text = text.Replace("<t>", pState?.DisplayName ?? targetName);

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

    private static string ReplaceMessageStacks(string text, Configuration cfg, string targetPlayerName, PlayerState? pState)
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

            msg = ProcessContextTokens(msg, pState, targetPlayerName);
            msg = ReplacePlayerScoreFirst(msg);
            msg = VariableManager.ProcessMessage(msg);

            return msg;
        });
    }

    public static async Task ExecuteGroup(string groupName, string targetPlayerName, Configuration cfg)
    {
        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog($"[Executor] Start Chain: {groupName} -> {targetPlayerName}");
        var players = window.GetPlayers();
        var dealer = window.GetDealer();

        var pState = targetPlayerName.Equals(dealer.Name, StringComparison.OrdinalIgnoreCase)
            ? dealer
            : players.FirstOrDefault(p => p.DisplayName.Equals(targetPlayerName, StringComparison.OrdinalIgnoreCase) || p.Name.Equals(targetPlayerName, StringComparison.OrdinalIgnoreCase));

        var group = cfg.CommandGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));

        if (group == null) return;

        _isRunning = true;
        int step = 0;

        foreach (var cmd in group.Commands)
        {
            step++;
            if (!cmd.Enabled || string.IsNullOrWhiteSpace(cmd.Text))
            {
                window.AddDebugLog($"[Executor] Skip Step {step} (Disabled or Empty)");
                continue;
            }

            try
            {
                window.AddDebugLog($"[Executor] Processing Step {step}: {cmd.Text}");

                string processedText = ProcessContextTokens(cmd.Text, pState, targetPlayerName);

                processedText = ReplacePlayerScoreFirst(processedText);
                processedText = ReplaceMessageStacks(processedText, cfg, targetPlayerName, pState);

                processedText = VariableManager.ProcessMessage(processedText);

                window.AddDebugLog($"[Executor] Final Text Step {step}: {processedText}");

                ChatCommandRouter.Send(processedText, cfg, $"{groupName}:{step}");

                float effectiveDelay = Plugin.IsDebugMode ? 0.2f : cmd.Delay;

                if (effectiveDelay > 0)
                {
                    window.AddDebugLog($"[Executor] Delaying {effectiveDelay}s...");
                    await Task.Delay(TimeSpan.FromSeconds(effectiveDelay));
                }
            }
            catch (Exception ex)
            {
                window.AddDebugLog($"[Executor-Step-Error] Step {step} failed: {ex.Message}");
            }
        }

        _isRunning = false;
        window.AddDebugLog($"[Executor] Chain End: {groupName}");
    }

    private static string ResolveCommandText(string text, string targetPlayerName, Configuration cfg, PlayerState? pState)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = ProcessContextTokens(text, pState, targetPlayerName);
        text = ReplacePlayerScoreFirst(text);
        text = ReplaceMessageStacks(text, cfg, targetPlayerName, pState);
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
