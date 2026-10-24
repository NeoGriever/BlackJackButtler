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
        LogFlow(
            window,
            $"Captured group route for '{groupName}': {(allianceMode ? "Alliance" : "Party")} | " +
                $"Elapsed={chainStopwatch.ElapsedMilliseconds}ms"
        );

        _currentGroupName = groupName;
        _currentTargetPlayer = targetPlayerName;
        _currentGroupHasDice = group.Commands.Any(
            c =>
                c.Enabled && !string.IsNullOrWhiteSpace(c.Text) &&
                c.Text.TrimStart().StartsWith("/dice", StringComparison.OrdinalIgnoreCase)
        );
        var commandSnapshot = group.Commands.ToList();
        _delayCts = new CancellationTokenSource();

        var isStatePromptGroup = StateGroupNames.Contains(groupName);

        _isRunning = true;
        _cancel = false;
        LogFlow(window, $"Started chain '{groupName}' | Steps={commandSnapshot.Count} | HasDice={_currentGroupHasDice}");
        int step = 0;
        var processedGroups = new HashSet<int>();

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
            LogFlow(
                window,
                $"Step {step}/{commandSnapshot.Count} entered | Enabled={cmd.Enabled} | " +
                    $"GroupId={cmd.GroupId} | Ref={cmd.IsCommandRef} | Raw='{cmd.Text}'"
            );

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

                    float refDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));
                    if (refDelay > 0)
                    {
                        LogFlow(window, $"Step {step} post-reference delay begin ({refDelay:0.###}s)");
                        try { await Task.Delay(TimeSpan.FromSeconds(refDelay), _delayCts!.Token); } catch (OperationCanceledException) { }
                        LogFlow(window, $"Step {step} post-reference delay end");
                    }
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
                    out var skipCondition
                ))
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
                    $"Step {step} [/if] execute={shouldExecute} | skipDelay={skipDelay} | command='{resolvedCommand}'"
                );
                if (!shouldExecute)
                {
                    LogFlow(window, $"Step {step} skipped: condition empty or false | SkipDelay={skipDelay}");
                    if (!skipDelay)
                    {
                        float skipEffDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));
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
                    $"Step {step} [Final output] mode={(allianceMode ? "Alliance" : "Party")} | '{processedText}'"
                );
                LogFlow(window, $"Step {step} routed='{processedText}'");

                bool isDiceCommand = processedText.Trim().StartsWith("/dice", StringComparison.OrdinalIgnoreCase);

                bool waitForPlayerRoll = false;
                if (isDiceCommand)
                {
                    _wait = true;
                    var diceMatch = DicePartyRegex.Match(processedText.Trim());
                    var parsedSides = diceMatch.Success && int.TryParse(diceMatch.Groups[1].Value, out var sides) ? sides : 0;
                    Volatile.Write(
                        ref _expectedDiceSides,
                        parsedSides
                    );
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
                }
                else if (isDiceCommand && TryHandleDebugDice(processedText))
                {
                    LogFlow(window, $"Step {step} handled by debug dice");
                }
                else
                {
                    LogFlow(window, $"Step {step} dispatch requested");
                    var sendResult = await ChatCommandRouter.SendAsync(
                        processedText,
                        cfg,
                        $"{groupName}:{step}",
                        effectiveCmd.Text,
                        allianceMode,
                        effectiveCmd.NonDoubled
                    );
                    if (sendResult == ChatCommandSendResult.AntiDoubleSkipped)
                    {
                        LogFlow(
                            window,
                            $"[Anti-Double] Step {step} final Party/Alliance message matched the last " +
                            "generated chat line; command and associated delay skipped"
                        );
                        continue;
                    }
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

                float effectiveDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));

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
            $"[Executor T{Environment.CurrentManagedThreadId}/Task{Task.CurrentId?.ToString() ?? "-"}] {message}"
        );
    }

    private static void LogFullDebug(BlackJackButtler.Windows.BlackJackButtlerWindow window, string message)
    {
        window.AddFullDebugLog(
            $"[Executor-Full T{Environment.CurrentManagedThreadId}/Task{Task.CurrentId?.ToString() ?? "-"}] {message}"
        );
    }
}
