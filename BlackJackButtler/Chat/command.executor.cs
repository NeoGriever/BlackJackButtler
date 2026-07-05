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

public static class CommandExecutor
{
    private static readonly RRX.Regex StackTokenRegex = new(@"#\{([^}]+)\}", RRX.RegexOptions.Compiled);
    private static readonly RRX.Regex DicePartyRegex = new(@"^/dice\s+(?:party|p|alliance|al)\s+(\d+)\s*$", RRX.RegexOptions.Compiled | RRX.RegexOptions.IgnoreCase);
    private const float MinCommandDelay = 0.05f;
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
    private static int _expectedDiceSides;
    private static bool _expectsPlayerDice;
    private static string _expectedDicePlayerName = string.Empty;
    private static uint _expectedDicePlayerWorldId;

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
        Volatile.Write(ref _expectedDiceSides, 0);
        ClearExpectedPlayerDice();
    }

    public static bool IsWaitingForDiceValue(int value)
    {
        var sides = Volatile.Read(ref _expectedDiceSides);
        return _wait && sides > 0 && value >= 1 && value <= sides;
    }

    public static bool TryAcceptPlayerDice(
        ParsedChatMessage message,
        List<PlayerState> players,
        PlayerState dealer)
    {
        if (!_wait || !_expectsPlayerDice || Volatile.Read(ref _expectedDiceSides) != 13
            || !message.DiceValue.HasValue)
            return false;

        // Group /dice 13 reports its range. Native /random is delivered through the
        // RandomNumber chat kind without range metadata, so it is accepted only while
        // a 13-roll from this exact player is pending and only for values 1..13.
        if (message.DiceSides.HasValue && message.DiceSides.Value != 13)
            return false;
        if (!message.DiceSides.HasValue && !ChatLogBuffer.IsDiceChatType(message.ChatType))
            return false;
        if (message.DiceValue.Value is < 1 or > 13)
            return false;

        var expected = players.FirstOrDefault(p =>
            p.Name.Equals(_expectedDicePlayerName, StringComparison.OrdinalIgnoreCase)
            && (_expectedDicePlayerWorldId == 0 || p.WorldId == _expectedDicePlayerWorldId));
        if (expected == null || !SenderMatchesExpectedPlayer(message, expected))
        {
            Plugin.Instance.GetMainWindow().AddDebugLog(
                $"[PlayerDice] Ignored roll from '{message.Name}': waiting for '{_expectedDicePlayerName}'");
            return false;
        }

        var card = GameEngine.MapDice13ToCardValue(message.DiceValue.Value);
        Plugin.Instance.GetMainWindow().AddDebugLog(
            $"[PlayerDice] Accepted {message.DiceValue.Value}/13 from {expected.DisplayName} as card {card}");
        DiceResultHandler.HandleDiceResult(card, Plugin.Instance.Configuration, players, dealer);
        return true;
    }

    private static bool SenderMatchesExpectedPlayer(ParsedChatMessage message, PlayerState expected)
    {
        if (message.WorldId > 0 && expected.WorldId != 0 && (uint)message.WorldId != expected.WorldId)
            return false;

        var sender = message.Name.Trim();
        if (sender.Equals(expected.Name, StringComparison.OrdinalIgnoreCase)
            || sender.Equals(expected.ResolvedName, StringComparison.OrdinalIgnoreCase))
            return true;

        var parts = expected.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return false;
        var surname = string.Join(' ', parts.Skip(1));
        return sender.Equals($"{parts[0]} {surname[0]}.", StringComparison.OrdinalIgnoreCase)
            || sender.Equals($"{parts[0][0]}. {surname}", StringComparison.OrdinalIgnoreCase)
            || sender.Equals($"{parts[0][0]}. {surname[0]}.", StringComparison.OrdinalIgnoreCase);
    }

    private static void ExpectPlayerDice(PlayerState player)
    {
        _expectedDicePlayerName = player.Name;
        _expectedDicePlayerWorldId = player.WorldId;
        _expectsPlayerDice = true;
    }

    private static void ClearExpectedPlayerDice()
    {
        _expectsPlayerDice = false;
        _expectedDicePlayerName = string.Empty;
        _expectedDicePlayerWorldId = 0;
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
        Volatile.Write(ref _expectedDiceSides, 0);
        ClearExpectedPlayerDice();
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
        var variableSnapshot = VariableManager.SnapshotForUi();
        foreach (var varName in resVars)
        {
            var v = variableSnapshot.FirstOrDefault(x => x.Name.Equals(varName, StringComparison.OrdinalIgnoreCase));
            if (v != null) text = text.Replace($"<{varName}>", v.Value);
        }

        bool hasAlias = pState != null && !string.IsNullOrWhiteSpace(pState.Alias);
        string aliasOrT = hasAlias ? pState!.Alias! : targetName;

        var trimmed = text.TrimStart();
        bool isTellCommand = trimmed.StartsWith("/tell ", StringComparison.OrdinalIgnoreCase)
                          || trimmed.StartsWith("/t ", StringComparison.OrdinalIgnoreCase);

        var targetToken = FindFirstTargetToken(text);
        if (isTellCommand && targetToken.index >= 0)
        {
            string currentTarget = GameEngine.GetCurrentTargetName();

            bool isCorrectlyTargeted = pState != null
                && !string.IsNullOrWhiteSpace(currentTarget)
                && (currentTarget.Equals(pState.Name, StringComparison.OrdinalIgnoreCase)
                    || currentTarget.Equals(PlayerIdentityManager.GetQualifiedName(pState), StringComparison.OrdinalIgnoreCase));

            string firstReplacement;
            if (isCorrectlyTargeted)
            {
                firstReplacement = "<t>";
            }
            else
            {
                string realName = pState?.Name ?? targetName;
                var qualifiedName = pState != null
                    ? PlayerIdentityManager.GetQualifiedName(pState)
                    : realName;
                firstReplacement = qualifiedName.Contains('@', StringComparison.Ordinal)
                    ? qualifiedName
                    : realName;
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
                    .Select(g =>
                    {
                        var entry = g.OrderByDescending(e => e.Amount).First();
                        string name = string.IsNullOrWhiteSpace(entry.Name) ? $"VIP {entry.VipLevel}" : entry.Name;
                        return $"{name}: {FormatGilAmount(entry.Amount, cfg.ShortBetFormat)}";
                    });
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
        var chainStopwatch = Stopwatch.StartNew();
        LogFlow(window, $"Requested chain '{groupName}' -> '{targetPlayerName}'");
        var players = window.GetPlayers();
        var dealer = window.GetDealer();

        var pState = PlayerIdentityManager.Find(players, dealer, targetPlayerName);

        if (groupName.Equals("Payout", StringComparison.OrdinalIgnoreCase))
        {
            if (pState == null)
            {
                LogFlow(window, $"Payout skipped: target '{targetPlayerName}' not found");
                return;
            }

            PayoutManagement.StartPayout(pState);
            LogFlow(window, $"Payout requested for '{pState.DisplayName}'");
            return;
        }

        // Set HandIndex variable for split hand identification
        if (pState != null && pState.Hands.Count > 1)
            VariableManager.SetVariable("HandIndex", $"[Hand {pState.CurrentHandIndex + 1}] ");
        else
            VariableManager.SetVariable("HandIndex", "");

        var group = cfg.CommandGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                 ?? cfg.CustomCommandGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));

        if (group == null)
        {
            LogFlow(window, $"Chain '{groupName}' not found");
            return;
        }

        if (cfg.CustomCommandGroups.Contains(group) && !group.IsActive)
        {
            LogFlow(window, $"Chain '{groupName}' inactive, skipping");
            return;
        }

        LogFlow(window, $"Capturing group route for '{groupName}'");
        var allianceMode = await GroupContextManager.CaptureAllianceModeAsync(cfg, groupName);
        LogFlow(window, $"Captured group route for '{groupName}': {(allianceMode ? "Alliance" : "Party")} | " +
            $"Elapsed={chainStopwatch.ElapsedMilliseconds}ms");

        _currentGroupName = groupName;
        _currentTargetPlayer = targetPlayerName;
        _currentGroupHasDice = group.Commands.Any(c =>
            c.Enabled && !string.IsNullOrWhiteSpace(c.Text) &&
            c.Text.TrimStart().StartsWith("/dice", StringComparison.OrdinalIgnoreCase));
        var commandSnapshot = group.Commands.ToList();
        _delayCts = new CancellationTokenSource();

        var isStatePromptGroup = StateGroupNames.Contains(groupName);

        _isRunning = true;
        _cancel = false;
        LogFlow(window, $"Started chain '{groupName}' | Steps={commandSnapshot.Count} | HasDice={_currentGroupHasDice}");
        int step = 0;
        var processedGroups = new HashSet<int>();

        // Wait for target focus to settle before executing commands
        if (!Plugin.IsDebugMode || !Plugin.IsSpeedMode)
        {
            LogFlow(window, $"Initial target-settle delay begin for '{groupName}' (300ms)");
            try { await Task.Delay(300, _delayCts.Token); } catch (OperationCanceledException) { }
            LogFlow(window, $"Initial target-settle delay end for '{groupName}'");
        }

        foreach (var cmd in commandSnapshot)
        {
            step++;
            var stepStopwatch = Stopwatch.StartNew();
            LogFlow(window, $"Step {step}/{commandSnapshot.Count} entered | Enabled={cmd.Enabled} | " +
                $"GroupId={cmd.GroupId} | Ref={cmd.IsCommandRef} | Raw='{cmd.Text}'");

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
                    LogFlow(window, $"Step {step} skipped: disabled or empty");
                    continue;
                }
                effectiveCmd = cmd;
            }
            else
            {
                if (processedGroups.Contains(cmd.GroupId))
                {
                    LogFlow(window, $"Step {step} skipped: group {cmd.GroupId} already handled");
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
                    LogFlow(window, $"Step {step} skipped: group {cmd.GroupId} has no enabled command");
                    continue;
                }
                effectiveCmd = selected;
                LogFlow(window, $"Step {step} selected grouped command '{effectiveCmd.Text}'");
            }

            try
            {
                if (effectiveCmd.IsCommandRef && !string.IsNullOrWhiteSpace(effectiveCmd.CommandRefName))
                {
                    if (_cancel) break;
                    LogFlow(window, $"Step {step} executing reference '{effectiveCmd.CommandRefName}'");
                    await ExecuteInternalGroup(effectiveCmd.CommandRefName, targetPlayerName, cfg, allianceMode);
                    LogFlow(window, $"Step {step} reference returned | Elapsed={stepStopwatch.ElapsedMilliseconds}ms");

                    float refDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f
                        : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));
                    if (refDelay > 0)
                    {
                        LogFlow(window, $"Step {step} post-reference delay begin ({refDelay:0.###}s)");
                        try { await Task.Delay(TimeSpan.FromSeconds(refDelay), _delayCts!.Token); } catch (OperationCanceledException) { }
                        LogFlow(window, $"Step {step} post-reference delay end");
                    }
                    continue;
                }

                if (cfg.EnableAntiDouble && effectiveCmd.NonDoubled && effectiveCmd.Text == _lastSentRawText)
                {
                    LogFlow(window, $"Step {step} skipped: anti-double");
                    continue;
                }

                var commandText = effectiveCmd.Text;
                LogFullDebug(window, $"Step {step} [Input] '{commandText}'");
                if (TryResolveSkipCommand(
                        commandText,
                        pState,
                        targetPlayerName,
                        cfg,
                        out commandText,
                        out var skipCondition))
                {
                    LogFullDebug(window, $"Step {step} [/skip] condition='{skipCondition}' | result=SKIP");
                    LogFlow(window, $"Step {step} skipped: /skip value empty");
                    continue;
                }
                LogFullDebug(window, $"Step {step} [/skip] condition='{skipCondition}' | command='{commandText}'");

                string processedText = ReplaceMessageStacks(commandText, cfg);
                LogFullDebug(window, $"Step {step} [Message stacks] '{processedText}'");
                processedText = ProcessContextTokens(processedText, pState, targetPlayerName, cfg);
                LogFullDebug(window, $"Step {step} [Context tokens] '{processedText}'");
                processedText = ReplacePlayerScoreFirst(processedText);
                LogFullDebug(window, $"Step {step} [Player score] '{processedText}'");
                processedText = VariableManager.ProcessMessage(processedText);
                LogFullDebug(window, $"Step {step} [Variables] '{processedText}'");
                LogFlow(window, $"Step {step} resolved='{processedText}'");

                var (shouldExecute, skipDelay, resolvedCommand) = EvaluateConditionalCommand(processedText);
                LogFullDebug(
                    window,
                    $"Step {step} [/if] execute={shouldExecute} | skipDelay={skipDelay} | command='{resolvedCommand}'");
                if (!shouldExecute)
                {
                    LogFlow(window, $"Step {step} skipped: condition empty or false | SkipDelay={skipDelay}");
                    if (!skipDelay)
                    {
                        float skipEffDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f
                            : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));
                        if (skipEffDelay > 0)
                        {
                            LogFlow(window, $"Step {step} skipped-command delay begin ({skipEffDelay:0.###}s)");
                            try { await Task.Delay(TimeSpan.FromSeconds(skipEffDelay), _delayCts!.Token); } catch (OperationCanceledException) { }
                            LogFlow(window, $"Step {step} skipped-command delay end");
                        }
                    }
                    continue;
                }
                processedText = resolvedCommand;
                processedText = processedText.Replace("<.>", "<t>");
                LogFullDebug(window, $"Step {step} [Target token] '{processedText}'");
                processedText = ChatCommandRouter.NormalizeGroupCommand(processedText, allianceMode);
                LogFullDebug(
                    window,
                    $"Step {step} [Final output] mode={(allianceMode ? "Alliance" : "Party")} | '{processedText}'");
                LogFlow(window, $"Step {step} routed='{processedText}'");

                bool isDiceCommand = processedText.Trim().StartsWith("/dice", StringComparison.OrdinalIgnoreCase);

                bool waitForPlayerRoll = false;
                if (isDiceCommand)
                {
                    _wait = true;
                    var diceMatch = DicePartyRegex.Match(processedText.Trim());
                    var parsedSides = diceMatch.Success && int.TryParse(diceMatch.Groups[1].Value, out var sides)
                        ? sides
                        : 0;
                    Volatile.Write(
                        ref _expectedDiceSides,
                        parsedSides);
                    waitForPlayerRoll = pState != null
                        && !pState.IsDealer
                        && parsedSides == 13
                        && PlayerRollPreferenceManager.ShouldPlayerRoll(cfg, pState);
                    if (waitForPlayerRoll)
                    {
                        ExpectPlayerDice(pState!);
                        LogFlow(window, $"Step {step} waiting for {pState!.DisplayName} to roll /dice 13");
                    }
                }

                if (isDiceCommand && waitForPlayerRoll)
                {
                    LogFlow(window, $"Step {step} dealer dice dispatch suppressed for player self-roll");
                    _lastSentRawText = effectiveCmd.Text;
                }
                else if (isDiceCommand && TryHandleDebugDice(processedText))
                {
                    LogFlow(window, $"Step {step} handled by debug dice");
                    _lastSentRawText = effectiveCmd.Text;
                }
                else
                {
                    LogFlow(window, $"Step {step} dispatch requested");
                    ChatCommandRouter.Send(
                        processedText,
                        cfg,
                        $"{groupName}:{step}",
                        effectiveCmd.Text,
                        allianceMode);
                    _lastSentRawText = effectiveCmd.Text;
                }

                if (isDiceCommand)
                {
                    LogFlow(window, $"Step {step} dice wait begin");

                    int waitCount = 0;
                    while (_wait && !_cancel)
                    {
                        await Task.Delay(50);
                        waitCount++;

                        if (waitCount > 600)
                        {
                            LogFlow(window, $"Step {step} dice wait timeout");
                            _wait = false;
                            Volatile.Write(ref _expectedDiceSides, 0);
                            ClearExpectedPlayerDice();
                        }
                    }

                    LogFlow(window, $"Step {step} dice wait end | Cancel={_cancel}");
                    Volatile.Write(ref _expectedDiceSides, 0);
                    ClearExpectedPlayerDice();

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
                    LogFlow(window, $"Step {step} delay begin ({effectiveDelay:0.###}s)");
                    try { await Task.Delay(TimeSpan.FromSeconds(effectiveDelay), _delayCts!.Token); } catch (OperationCanceledException) { }
                    LogFlow(window, $"Step {step} delay end | StepElapsed={stepStopwatch.ElapsedMilliseconds}ms");
                }
            }
            catch (Exception ex)
            {
                LogFlow(window, $"Step {step} failed | {ex.GetType().Name}: {ex.Message}");
            }
        }

        _isRunning = false;
        _cancel = false;
        _wait = false;
        Volatile.Write(ref _expectedDiceSides, 0);
        ClearExpectedPlayerDice();
        _internalDepth = 0;
        _currentGroupName = string.Empty;
        _currentTargetPlayer = string.Empty;
        _currentGroupHasDice = false;
        _preActionSnapshotIndex = -1;
        _delayCts?.Dispose();
        _delayCts = null;
        if (isStatePromptGroup)
        {
            LastStateGroupName = groupName;
            LastStateTargetName = targetPlayerName;
            LastStateFiredAt = DateTime.Now;
        }
        OnGroupCompleted?.Invoke();
        LogFlow(window, $"Finished chain '{groupName}' | Elapsed={chainStopwatch.ElapsedMilliseconds}ms");
    }

    private static void LogFlow(BlackJackButtler.Windows.BlackJackButtlerWindow window, string message)
    {
        window.AddDebugLog(
            $"[Executor T{Environment.CurrentManagedThreadId}/Task{Task.CurrentId?.ToString() ?? "-"}] {message}");
    }

    private static void LogFullDebug(BlackJackButtler.Windows.BlackJackButtlerWindow window, string message)
    {
        window.AddFullDebugLog(
            $"[Executor-Full T{Environment.CurrentManagedThreadId}/Task{Task.CurrentId?.ToString() ?? "-"}] {message}");
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
        out string resolvedCondition)
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

    public static async Task ExecuteInternalGroup(
        string groupName,
        string targetPlayerName,
        Configuration cfg,
        bool allianceMode)
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

        var pState = PlayerIdentityManager.Find(players, dealer, targetPlayerName);

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
                    await ExecuteInternalGroup(effectiveCmd.CommandRefName, targetPlayerName, cfg, allianceMode);

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

                var commandText = effectiveCmd.Text;
                LogFullDebug(window, $"Internal {groupName}:{step} [Input] '{commandText}'");
                if (TryResolveSkipCommand(
                        commandText,
                        pState,
                        targetPlayerName,
                        cfg,
                        out commandText,
                        out var skipCondition))
                {
                    LogFullDebug(
                        window,
                        $"Internal {groupName}:{step} [/skip] condition='{skipCondition}' | result=SKIP");
                    window.AddDebugLog($"[Executor-Internal] Step {step} skipped (/skip value empty)");
                    continue;
                }
                LogFullDebug(
                    window,
                    $"Internal {groupName}:{step} [/skip] condition='{skipCondition}' | command='{commandText}'");

                string processedText = ReplaceMessageStacks(commandText, cfg);
                LogFullDebug(window, $"Internal {groupName}:{step} [Message stacks] '{processedText}'");
                processedText = ProcessContextTokens(processedText, pState, targetPlayerName, cfg);
                LogFullDebug(window, $"Internal {groupName}:{step} [Context tokens] '{processedText}'");
                processedText = ReplacePlayerScoreFirst(processedText);
                LogFullDebug(window, $"Internal {groupName}:{step} [Player score] '{processedText}'");
                processedText = VariableManager.ProcessMessage(processedText);
                LogFullDebug(window, $"Internal {groupName}:{step} [Variables] '{processedText}'");

                var (shouldExecuteInt, skipDelayInt, resolvedCommandInt) = EvaluateConditionalCommand(processedText);
                LogFullDebug(
                    window,
                    $"Internal {groupName}:{step} [/if] execute={shouldExecuteInt} | " +
                    $"skipDelay={skipDelayInt} | command='{resolvedCommandInt}'");
                if (!shouldExecuteInt)
                {
                    window.AddDebugLog($"[Executor-Internal] Step {step} skipped (condition empty or false)");
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
                LogFullDebug(window, $"Internal {groupName}:{step} [Target token] '{processedText}'");
                processedText = ChatCommandRouter.NormalizeGroupCommand(processedText, allianceMode);
                LogFullDebug(
                    window,
                    $"Internal {groupName}:{step} [Final output] " +
                    $"mode={(allianceMode ? "Alliance" : "Party")} | '{processedText}'");

                ChatCommandRouter.Send(
                    processedText,
                    cfg,
                    $"{groupName}:internal:{step}",
                    effectiveCmd.Text,
                    allianceMode);
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

    public static async Task WaitForCurrentGroupToFinishAsync()
    {
        if (!IsRunning)
            return;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler() => tcs.TrySetResult(true);

        OnGroupCompleted += Handler;
        try
        {
            if (!IsRunning)
                return;

            await tcs.Task;
        }
        finally
        {
            OnGroupCompleted -= Handler;
        }
    }
}
