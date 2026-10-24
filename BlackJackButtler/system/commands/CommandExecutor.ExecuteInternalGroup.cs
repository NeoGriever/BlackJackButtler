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
    public static async Task ExecuteInternalGroup(
        string groupName,
        string targetPlayerName,
        Configuration cfg,
        bool allianceMode
    )
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

                    float refDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));
                    if (refDelay > 0)
                    {
                        window.AddDebugLog($"[Executor-Internal] Post-ref delay {refDelay}s...");
                        await Task.Delay(TimeSpan.FromSeconds(refDelay));
                    }
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
                    out var skipCondition
                ))
                {
                    LogFullDebug(
                        window,
                        $"Internal {groupName}:{step} [/skip] condition='{skipCondition}' | result=SKIP"
                    );
                    window.AddDebugLog($"[Executor-Internal] Step {step} skipped (/skip value empty)");
                    continue;
                }
                LogFullDebug(
                    window,
                    $"Internal {groupName}:{step} [/skip] condition='{skipCondition}' | command='{commandText}'"
                );

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
                    $"skipDelay={skipDelayInt} | command='{resolvedCommandInt}'"
                );
                if (!shouldExecuteInt)
                {
                    window.AddDebugLog($"[Executor-Internal] Step {step} skipped (condition empty or false)");
                    if (!skipDelayInt)
                    {
                        float skipEffDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));
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
                    $"mode={(allianceMode ? "Alliance" : "Party")} | '{processedText}'"
                );

                var sendResult = await ChatCommandRouter.SendAsync(
                    processedText,
                    cfg,
                    $"{groupName}:internal:{step}",
                    effectiveCmd.Text,
                    allianceMode,
                    effectiveCmd.NonDoubled
                );
                if (sendResult == ChatCommandSendResult.AntiDoubleSkipped)
                {
                    window.AddDebugLog(
                        $"[Anti-Double] Internal step {step} final Party/Alliance message matched the last " +
                        "generated chat line; command and associated delay skipped"
                    );
                    continue;
                }

                float effectiveDelay = (Plugin.IsDebugMode && Plugin.IsSpeedMode) ? 0.2f : Math.Max(MinCommandDelay, effectiveCmd.Delay * (effectiveCmd.FixedDelay ? 1f : cfg.CommandSpeedMultiplier));

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
