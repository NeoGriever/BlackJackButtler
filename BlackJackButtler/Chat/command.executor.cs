using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using RRX = System.Text.RegularExpressions;
using System.Globalization;

namespace BlackJackButtler;

public static class CommandExecutor
{
    private static readonly RRX.Regex StackTokenRegex = new(@"#\{([^}]+)\}", RRX.RegexOptions.Compiled);
    private static readonly RRX.Regex DicePartyRegex = new(@"^/dice\s+party\s+(\d+)\s*$", RRX.RegexOptions.Compiled | RRX.RegexOptions.IgnoreCase);
    private const float MinCommandDelay = 0.3f;
    private const int MaxInternalDepth = 5;
    private static int _internalDepth = 0;
    private static bool _isRunning = false;
    public static bool IsRunning => _isRunning;

    private static volatile bool _followUpPending = false;
    public static bool IsFollowUpPending => _followUpPending;
    public static void SignalFollowUpPending() => _followUpPending = true;
    public static void ClearFollowUpPending() => _followUpPending = false;

    public static event Action? OnGroupCompleted;

    private static bool _wait = false;
    private static bool _cancel = false;

    private static string _currentGroupName = string.Empty;
    private static string _currentTargetPlayer = string.Empty;
    private static bool _currentGroupHasDice = false;
    private static int _preActionSnapshotIndex = -1;
    private static CancellationTokenSource? _delayCts = null;
    private static string _lastSentRawText = string.Empty;

    public static string CurrentGroupName => _currentGroupName;
    public static string CurrentTargetPlayer => _currentTargetPlayer;
    public static bool CurrentGroupHasDice => _currentGroupHasDice;
    public static int PreActionSnapshotIndex => _preActionSnapshotIndex;
    public static bool IsCancelling => _cancel;

    private static readonly HashSet<string> StateGroupNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "StateHSDS", "StateHSD", "StateHS",
    };

    public static string LastStateGroupName { get; private set; } = string.Empty;
    public static string LastStateTargetName { get; private set; } = string.Empty;
    public static DateTime LastStateFiredAt { get; private set; } = DateTime.MinValue;

    public static void ClearLastState()
    {
        LastStateGroupName = string.Empty;
        LastStateTargetName = string.Empty;
        LastStateFiredAt = DateTime.MinValue;
    }

    public static void NotifyDiceResult()
    {
        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog("[Executor] NotifyDiceResult called - releasing wait");
        _wait = false;
    }

    public static void SetPreActionSnapshot(int snapshotIndex)
    {
        _preActionSnapshotIndex = snapshotIndex;
    }

    public static void CancelCurrentGroup()
    {
        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog("[Executor] CancelCurrentGroup called - setting cancel flag");
        _cancel = true;
        _wait = false;
        _delayCts?.Cancel();
    }

    private static string FormatGilAmount(long amount, bool shortFormat)
    {
        if (!shortFormat)
            return amount.ToString("N0", CultureInfo.GetCultureInfo("en-US")) + " Gil";

        if (amount >= 1_000_000)
        {
            double millions = amount / 1_000_000.0;
            return (millions == Math.Floor(millions))
                ? $"{(long)millions}m" : $"{millions:0.#}m";
        }
        if (amount >= 1_000)
        {
            double thousands = amount / 1_000.0;
            return (thousands == Math.Floor(thousands))
                ? $"{(long)thousands}k" : $"{thousands:0.#}k";
        }
        return amount.ToString();
    }

    private static string ProcessContextTokens(string text, PlayerState? pState, string targetName, Configuration cfg)
    {
        if (string.IsNullOrEmpty(text)) return text;

        string[] resVars = { "winners", "pushed", "loosers", "busted", "results" };
        foreach (var varName in resVars)
        {
            var v = VariableManager.Variables.FirstOrDefault(x => x.Name.Equals(varName, StringComparison.OrdinalIgnoreCase));
            if (v != null) text = text.Replace($"<{varName}>", v.Value);
        }

        bool hasAlias = pState != null && !string.IsNullOrWhiteSpace(pState.Alias);
        string aliasOrT = hasAlias ? pState!.Alias! : targetName;

        var trimmed = text.TrimStart();
        bool isTellCommand = trimmed.StartsWith("/tell ", StringComparison.OrdinalIgnoreCase)
                          || trimmed.StartsWith("/t ", StringComparison.OrdinalIgnoreCase);

        if (isTellCommand && text.Contains("<t>"))
        {
            string currentTarget = Plugin.IsDebugMode
                ? GameEngine.GetCurrentTargetName()
                : (Plugin.TargetManager.Target?.Name.TextValue ?? string.Empty);

            bool isCorrectlyTargeted = pState != null
                && !string.IsNullOrWhiteSpace(currentTarget)
                && currentTarget.Equals(pState.Name, StringComparison.OrdinalIgnoreCase);

            var idx = text.IndexOf("<t>", StringComparison.Ordinal);
            string firstReplacement;
            if (isCorrectlyTargeted)
            {
                firstReplacement = "<t>";
            }
            else
            {
                string realName = pState?.Name ?? targetName;
                string worldName = pState != null ? VipManager.ResolveWorldName(pState.WorldId) : string.Empty;
                firstReplacement = !string.IsNullOrWhiteSpace(worldName)
                    ? $"{realName}@{worldName}" : realName;
            }
            var after = text[(idx + 3)..].Replace("<t>", aliasOrT);
            text = string.Concat(text.AsSpan(0, idx), firstReplacement, after);
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

        text = text.Replace("<minbet>", cfg.MinBet.ToString("N0", CultureInfo.GetCultureInfo("en-US")) + " Gil");
        text = text.Replace("<maxbet>", cfg.MaxBet.ToString("N0", CultureInfo.GetCultureInfo("en-US")) + " Gil");

        if (text.Contains("<betrange>"))
        {
            string min = FormatGilAmount(cfg.MinBet, cfg.ShortBetFormat);
            string max = FormatGilAmount(cfg.MaxBet, cfg.ShortBetFormat);
            string range = $"Min: {min} - Max: {max}";
            if (cfg.VipBetTiers.Count > 0)
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
        return StackTokenRegex.Replace(text, m =>
        {
            var stackName = m.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(stackName))
                return string.Empty;

            var batch = cfg.MessageBatches
                .FirstOrDefault(b => b.Name.Equals(stackName, StringComparison.OrdinalIgnoreCase));

            if (batch == null)
                return string.Empty;

            return batch.GetNextMessage(cfg.EnableAntiDouble) ?? string.Empty;
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

        // Set HandIndex variable for split hand identification
        if (pState != null && pState.Hands.Count > 1)
            VariableManager.SetVariable("HandIndex", $"[Hand {pState.CurrentHandIndex + 1}] ");
        else
            VariableManager.SetVariable("HandIndex", "");

        var group = cfg.CommandGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                 ?? cfg.CustomCommandGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));

        if (group == null) return;

        if (cfg.CustomCommandGroups.Contains(group) && !group.IsActive)
        {
            window.AddDebugLog($"[Executor] Group '{groupName}' is inactive, skipping");
            return;
        }

        _currentGroupName = groupName;
        _currentTargetPlayer = targetPlayerName;
        _currentGroupHasDice = group.Commands.Any(c =>
            c.Enabled && !string.IsNullOrWhiteSpace(c.Text) &&
            c.Text.TrimStart().StartsWith("/dice", StringComparison.OrdinalIgnoreCase));
        _delayCts = new CancellationTokenSource();

        if (StateGroupNames.Contains(groupName))
        {
            LastStateGroupName = groupName;
            LastStateTargetName = targetPlayerName;
            LastStateFiredAt = DateTime.Now;
        }

        _isRunning = true;
        _cancel = false;
        int step = 0;
        var processedGroups = new HashSet<int>();

        // Wait for target focus to settle before executing commands
        if (!Plugin.IsDebugMode || !Plugin.IsSpeedMode)
            try { await Task.Delay(300, _delayCts.Token); } catch (OperationCanceledException) { }

        foreach (var cmd in group.Commands)
        {
            step++;

            if (_cancel)
            {
                window.AddDebugLog($"[Executor] Group '{groupName}' canceled at step {step}");
                break;
            }

            PluginCommand effectiveCmd;

            if (cmd.GroupId == 0)
            {
                bool hasContent = !string.IsNullOrWhiteSpace(cmd.Text) ||
                    (cmd.IsCommandRef && !string.IsNullOrWhiteSpace(cmd.CommandRefName));
                if (!cmd.Enabled || !hasContent)
                {
                    window.AddDebugLog($"[Executor] Skip Step {step} (Disabled or Empty)");
                    continue;
                }
                effectiveCmd = cmd;
            }
            else
            {
                if (processedGroups.Contains(cmd.GroupId))
                {
                    window.AddDebugLog($"[Executor] Skip Step {step} (Group {cmd.GroupId} already handled)");
                    continue;
                }
                processedGroups.Add(cmd.GroupId);

                if (!group.LineGroups.TryGetValue(cmd.GroupId, out var lineGroup))
                {
                    lineGroup = new CommandLineGroup();
                    group.LineGroups[cmd.GroupId] = lineGroup;
                }

                var groupCmds = group.Commands.Where(c => c.GroupId == cmd.GroupId).ToList();
                var selected = lineGroup.PickNext(groupCmds);
                if (selected == null)
                {
                    window.AddDebugLog($"[Executor] Skip Group {cmd.GroupId} (No enabled commands)");
                    continue;
                }
                effectiveCmd = selected;
            }

            try
            {
                if (effectiveCmd.IsCommandRef && !string.IsNullOrWhiteSpace(effectiveCmd.CommandRefName))
                {
                    if (_cancel) break;
                    window.AddDebugLog($"[Executor] Step {step}: Executing command ref '{effectiveCmd.CommandRefName}'");
                    await ExecuteInternalGroup(effectiveCmd.CommandRefName, targetPlayerName, cfg);

                    float refDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f
                        : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));
                    if (refDelay > 0)
                    {
                        window.AddDebugLog($"[Executor] Post-ref delay {refDelay}s...");
                        try { await Task.Delay(TimeSpan.FromSeconds(refDelay), _delayCts!.Token); } catch (OperationCanceledException) { }
                    }
                    continue;
                }

                if (cfg.EnableAntiDouble && effectiveCmd.NonDoubled && effectiveCmd.Text == _lastSentRawText)
                {
                    window.AddDebugLog($"[Executor] Step {step} skipped (Anti-Double: same as last sent)");
                    continue;
                }

                window.AddDebugLog($"[Executor] Processing Step {step}: {effectiveCmd.Text}");

                string processedText = ReplaceMessageStacks(effectiveCmd.Text, cfg);
                processedText = ProcessContextTokens(processedText, pState, targetPlayerName, cfg);
                processedText = ReplacePlayerScoreFirst(processedText);
                processedText = VariableManager.ProcessMessage(processedText);

                window.AddDebugLog($"[Executor] Final Text Step {step}: {processedText}");

                var (shouldExecute, skipDelay, resolvedCommand) = EvaluateIfCondition(processedText);
                if (!shouldExecute)
                {
                    window.AddDebugLog($"[Executor] Step {step} skipped (/if condition false)");
                    if (!skipDelay)
                    {
                        float skipEffDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f
                            : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));
                        if (skipEffDelay > 0)
                        {
                            window.AddDebugLog($"[Executor] Waiting delay {skipEffDelay}s despite skip...");
                            try { await Task.Delay(TimeSpan.FromSeconds(skipEffDelay), _delayCts!.Token); } catch (OperationCanceledException) { }
                        }
                    }
                    continue;
                }
                processedText = resolvedCommand;
                processedText = processedText.Replace("<.>", "<t>");

                bool isDiceCommand = processedText.Trim().StartsWith("/dice", StringComparison.OrdinalIgnoreCase);

                if (isDiceCommand)
                {
                    window.AddDebugLog($"[Executor] Dice command detected, setting wait flag");
                    _wait = true;
                }

                ChatCommandRouter.Send(processedText, cfg, $"{groupName}:{step}");
                _lastSentRawText = effectiveCmd.Text;

                if (isDiceCommand)
                {
                    window.AddDebugLog($"[Executor] Waiting for dice result...");

                    int waitCount = 0;
                    while (_wait && !_cancel)
                    {
                        await Task.Delay(50);
                        waitCount++;

                        if (waitCount > 600)
                        {
                            window.AddDebugLog($"[Executor] Dice wait timeout - continuing anyway");
                            _wait = false;
                        }
                    }

                    window.AddDebugLog($"[Executor] Dice result received or canceled");

                    if (_cancel)
                    {
                        window.AddDebugLog($"[Executor] Group '{groupName}' was canceled during dice wait");
                        break;
                    }
                }

                float effectiveDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f
                    : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));

                if (effectiveDelay > 0)
                {
                    window.AddDebugLog($"[Executor] Delaying {effectiveDelay}s...");
                    try { await Task.Delay(TimeSpan.FromSeconds(effectiveDelay), _delayCts!.Token); } catch (OperationCanceledException) { }
                }
            }
            catch (Exception ex)
            {
                window.AddDebugLog($"[Executor-Step-Error] Step {step} failed: {ex.Message}");
            }
        }

        _isRunning = false;
        _cancel = false;
        _internalDepth = 0;
        _currentGroupName = string.Empty;
        _currentTargetPlayer = string.Empty;
        _currentGroupHasDice = false;
        _preActionSnapshotIndex = -1;
        _delayCts?.Dispose();
        _delayCts = null;
        OnGroupCompleted?.Invoke();
        window.AddDebugLog($"[Executor] Chain End: {groupName}");
    }

    private static string ResolveCommandText(string text, string targetPlayerName, Configuration cfg, PlayerState? pState)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = ReplaceMessageStacks(text, cfg);
        text = ProcessContextTokens(text, pState, targetPlayerName, cfg);
        text = ReplacePlayerScoreFirst(text);
        text = VariableManager.ProcessMessage(text);
        text = text.Replace("<.>", "<t>");

        return text;
    }

    private static (bool execute, bool skipDelay, string command) EvaluateIfCondition(string text)
    {
        if (!text.TrimStart().StartsWith("/if ", StringComparison.OrdinalIgnoreCase))
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

    public static async Task ExecuteInternalGroup(string groupName, string targetPlayerName, Configuration cfg)
    {
        if (_cancel) return;
        var window = Plugin.Instance.GetMainWindow();
        if (_internalDepth >= MaxInternalDepth)
        {
            window.AddDebugLog($"[Executor-Internal] Max nesting depth reached for '{groupName}', skipping");
            return;
        }
        _internalDepth++;
        window.AddDebugLog($"[Executor-Internal] Start Chain: {groupName} -> {targetPlayerName} (depth {_internalDepth})");
        var players = window.GetPlayers();
        var dealer = window.GetDealer();

        var pState = targetPlayerName.Equals(dealer.Name, StringComparison.OrdinalIgnoreCase)
            ? dealer
            : players.FirstOrDefault(p => p.DisplayName.Equals(targetPlayerName, StringComparison.OrdinalIgnoreCase) || p.Name.Equals(targetPlayerName, StringComparison.OrdinalIgnoreCase));

        // Set HandIndex variable for split hand identification
        if (pState != null && pState.Hands.Count > 1)
            VariableManager.SetVariable("HandIndex", $"[Hand {pState.CurrentHandIndex + 1}] ");
        else
            VariableManager.SetVariable("HandIndex", "");

        var group = cfg.CommandGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                 ?? cfg.CustomCommandGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));

        if (group == null)
        {
            window.AddDebugLog($"[Executor-Internal] Group '{groupName}' not found");
            return;
        }

        if (cfg.CustomCommandGroups.Contains(group) && !group.IsActive)
        {
            window.AddDebugLog($"[Executor-Internal] Group '{groupName}' is inactive, skipping");
            return;
        }

        int step = 0;
        var processedGroups = new HashSet<int>();

        // Wait for target focus to settle before executing commands
        if (!Plugin.IsDebugMode || !Plugin.IsSpeedMode)
            await Task.Delay(300);

        foreach (var cmd in group.Commands)
        {
            step++;

            PluginCommand effectiveCmd;

            if (cmd.GroupId == 0)
            {
                bool hasContent = !string.IsNullOrWhiteSpace(cmd.Text) ||
                    (cmd.IsCommandRef && !string.IsNullOrWhiteSpace(cmd.CommandRefName));
                if (!cmd.Enabled || !hasContent)
                {
                    window.AddDebugLog($"[Executor-Internal] Skip Step {step} (Disabled or Empty)");
                    continue;
                }
                effectiveCmd = cmd;
            }
            else
            {
                if (processedGroups.Contains(cmd.GroupId))
                {
                    window.AddDebugLog($"[Executor-Internal] Skip Step {step} (Group {cmd.GroupId} already handled)");
                    continue;
                }
                processedGroups.Add(cmd.GroupId);

                if (!group.LineGroups.TryGetValue(cmd.GroupId, out var lineGroup))
                {
                    lineGroup = new CommandLineGroup();
                    group.LineGroups[cmd.GroupId] = lineGroup;
                }

                var groupCmds = group.Commands.Where(c => c.GroupId == cmd.GroupId).ToList();
                var selected = lineGroup.PickNext(groupCmds);
                if (selected == null)
                {
                    window.AddDebugLog($"[Executor-Internal] Skip Group {cmd.GroupId} (No enabled commands)");
                    continue;
                }
                effectiveCmd = selected;
            }

            try
            {
                if (effectiveCmd.IsCommandRef && !string.IsNullOrWhiteSpace(effectiveCmd.CommandRefName))
                {
                    if (_cancel) break;
                    window.AddDebugLog($"[Executor-Internal] Step {step}: Executing nested command ref '{effectiveCmd.CommandRefName}'");
                    await ExecuteInternalGroup(effectiveCmd.CommandRefName, targetPlayerName, cfg);

                    float refDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f
                        : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));
                    if (refDelay > 0)
                    {
                        window.AddDebugLog($"[Executor-Internal] Post-ref delay {refDelay}s...");
                        await Task.Delay(TimeSpan.FromSeconds(refDelay));
                    }
                    continue;
                }

                if (cfg.EnableAntiDouble && effectiveCmd.NonDoubled && effectiveCmd.Text == _lastSentRawText)
                {
                    window.AddDebugLog($"[Executor-Internal] Step {step} skipped (Anti-Double: same as last sent)");
                    continue;
                }

                window.AddDebugLog($"[Executor-Internal] Processing Step {step}: {effectiveCmd.Text}");

                string processedText = ReplaceMessageStacks(effectiveCmd.Text, cfg);
                processedText = ProcessContextTokens(processedText, pState, targetPlayerName, cfg);
                processedText = ReplacePlayerScoreFirst(processedText);
                processedText = VariableManager.ProcessMessage(processedText);

                window.AddDebugLog($"[Executor-Internal] Final Text Step {step}: {processedText}");

                var (shouldExecuteInt, skipDelayInt, resolvedCommandInt) = EvaluateIfCondition(processedText);
                if (!shouldExecuteInt)
                {
                    window.AddDebugLog($"[Executor-Internal] Step {step} skipped (/if condition false)");
                    if (!skipDelayInt)
                    {
                        float skipEffDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f
                            : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));
                        if (skipEffDelay > 0)
                        {
                            window.AddDebugLog($"[Executor-Internal] Waiting delay {skipEffDelay}s despite skip...");
                            await Task.Delay(TimeSpan.FromSeconds(skipEffDelay));
                        }
                    }
                    continue;
                }
                processedText = resolvedCommandInt;
                processedText = processedText.Replace("<.>", "<t>");

                ChatCommandRouter.Send(processedText, cfg, $"{groupName}:internal:{step}");
                _lastSentRawText = effectiveCmd.Text;

                float effectiveDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f
                    : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));

                if (effectiveDelay > 0)
                {
                    window.AddDebugLog($"[Executor-Internal] Delaying {effectiveDelay}s...");
                    await Task.Delay(TimeSpan.FromSeconds(effectiveDelay));
                }
            }
            catch (Exception ex)
            {
                window.AddDebugLog($"[Executor-Internal-Step-Error] Step {step} failed: {ex.Message}");
            }
        }

        _internalDepth--;
        window.AddDebugLog($"[Executor-Internal] Chain End: {groupName}");
    }
}
