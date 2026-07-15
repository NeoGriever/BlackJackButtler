using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rx = System.Text.RegularExpressions.Regex;
using RxOptions = System.Text.RegularExpressions.RegexOptions;
using EA = ECommons.Automation;
using ECommons.DalamudServices;
using BlackJackButtler.Windows;

namespace BlackJackButtler.Chat;

public sealed record GeneratedGroupChatLine(
    DateTime Timestamp,
    string Channel,
    string Message,
    string RoutedCommandText,
    string Context);

public enum ChatCommandSendResult
{
    Sent,
    AntiDoubleSkipped,
    Failed,
}

public static class ChatCommandRouter
{
    private static readonly object GroupChatHistoryGate = new();
    private static readonly List<GeneratedGroupChatLine> GroupChatHistory = new();
    private static readonly Rx SoundEffectTokenRegex = new(
        @"<se\.[^>]*>",
        RxOptions.Compiled | RxOptions.IgnoreCase);

    private static readonly (string prefix, string channel)[] ChatPrefixes =
    {
        ("/party ", "Party"), ("/p ", "Party"),
        ("/alliance ", "Alliance"), ("/a ", "Alliance"),
        ("/yell ", "Yell"), ("/y ", "Yell"),
        ("/shout ", "Shout"), ("/sh ", "Shout"),
        ("/tell ", "Tell"),
        ("/say ", "Say"), ("/s ", "Say"),
    };

    public static void Send(
        string commandText,
        Configuration cfg,
        string? context = null,
        string? commandEntryText = null,
        bool? allianceMode = null,
        bool avoidDuplicateGroupMessage = false)
        => _ = SendAsync(
            commandText,
            cfg,
            context,
            commandEntryText,
            allianceMode,
            avoidDuplicateGroupMessage);

    public static Task<ChatCommandSendResult> SendAsync(
        string commandText,
        Configuration cfg,
        string? context = null,
        string? commandEntryText = null,
        bool? allianceMode = null,
        bool avoidDuplicateGroupMessage = false)
    {
        var window = Plugin.Instance.GetMainWindow();
        var requestedCommandText = commandText;
        var completion = new TaskCompletionSource<ChatCommandSendResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Svc.Framework.RunOnTick(() =>
        {
            try
            {
                if (!allianceMode.HasValue)
                    GroupContextManager.Refresh(cfg);
                var routeToAlliance = allianceMode ?? GroupContextManager.IsAllianceMode(cfg);
                var routedCommandText = NormalizeGroupCommand(requestedCommandText, routeToAlliance);

                var displayedInput = commandEntryText ?? requestedCommandText;
                window.AddDebugLog(
                    $"[Command {context ?? "(none)"} Input]  {displayedInput}",
                    false,
                    BlackJackButtlerWindow.DebugLogColor.CommandInput);
                window.AddDebugLog(
                    $"[Command {context ?? "(none)"} Output] {routedCommandText}",
                    false,
                    BlackJackButtlerWindow.DebugLogColor.CommandOutput);

                if (ViewDirectionManager.IsInternalCommand(routedCommandText))
                {
                    ViewDirectionManager.ApplyViewDirection(cfg);
                    window.AddDebugLog("[Router] /initialviewdirection intercepted - rotation applied");
                    completion.TrySetResult(ChatCommandSendResult.Sent);
                    return;
                }

                var isGroupChatMessage = TryExtractGroupChatMessage(
                    routedCommandText.TrimStart(),
                    out var groupChannel,
                    out var groupMessage);
                var antiDoubleComparisonMessage = isGroupChatMessage
                    ? NormalizeAntiDoubleMessage(groupMessage)
                    : string.Empty;

                if (isGroupChatMessage
                    && avoidDuplicateGroupMessage
                    && IsSameAsLastGroupChatMessage(antiDoubleComparisonMessage, out var lastGroupLine))
                {
                    window.AddDebugLog(
                        $"[Anti-Double] Skipped {groupChannel} message because its final result matches " +
                        $"the last generated {lastGroupLine!.Channel} chat line after normalization: " +
                        $"'{antiDoubleComparisonMessage}'");
                    completion.TrySetResult(ChatCommandSendResult.AntiDoubleSkipped);
                    return;
                }

                StatsLogManager.AppendPartyCommand(routedCommandText);

                if (Plugin.IsDebugMode)
                {
                    var trimmed = routedCommandText.TrimStart();

                    if (TryExtractChatMessage(trimmed, out var channel, out var msgPart))
                    {
                        window.AddDebugLog($"[{channel}] {msgPart}", isChat: true);
                    }
                    else if (trimmed.StartsWith("/dice", StringComparison.OrdinalIgnoreCase))
                    {
                        Plugin.Instance.InjectChatMessage(64, 0, "SYSTEM", "SYSTEM", routedCommandText);
                    }
                    else
                    {
                        window.AddDebugLog($"[Router-Debug] Skipped non-chat command: {routedCommandText}");
                    }

                    if (isGroupChatMessage)
                        RecordGroupChatMessage(
                            groupChannel,
                            antiDoubleComparisonMessage,
                            routedCommandText,
                            context);
                    completion.TrySetResult(ChatCommandSendResult.Sent);
                    return;
                }

                window.AddDebugLog($"[Router-Dispatch] Sending to Chat: {routedCommandText}");

                EA.Chat.SendMessage(routedCommandText);
                if (isGroupChatMessage)
                    RecordGroupChatMessage(
                        groupChannel,
                        antiDoubleComparisonMessage,
                        routedCommandText,
                        context);
                completion.TrySetResult(ChatCommandSendResult.Sent);
            }
            catch (Exception ex)
            {
                window.AddDebugLog($"[Router-CRITICAL] Crash during Send: {ex.GetType().Name} - {ex.Message}");
                completion.TrySetResult(ChatCommandSendResult.Failed);
            }
        });

        return completion.Task;
    }

    public static string NormalizeGroupCommand(string commandText, Configuration cfg)
        => NormalizeGroupCommand(commandText, GroupContextManager.IsAllianceMode(cfg));

    public static string NormalizeGroupCommand(string commandText, bool alliance)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            return commandText;

        var leadingLength = commandText.Length - commandText.TrimStart().Length;
        var leading = commandText[..leadingLength];
        var trimmed = commandText[leadingLength..];
        var targetChat = alliance ? "/alliance" : "/party";
        var targetDice = alliance ? "alliance" : "party";

        var chatMatch = Rx.Match(trimmed, @"^/(?:p|party|a|alliance)(?=\s|$)", RxOptions.IgnoreCase);
        if (chatMatch.Success)
            return leading + targetChat + trimmed[chatMatch.Length..];

        if (!Rx.IsMatch(trimmed, @"^/dice(?=\s|$)", RxOptions.IgnoreCase))
            return commandText;

        var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (tokens.Count == 1)
        {
            tokens.Add(targetDice);
        }
        else
        {
            var channel = tokens[1];
            if (channel.Equals("party", StringComparison.OrdinalIgnoreCase)
                || channel.Equals("p", StringComparison.OrdinalIgnoreCase)
                || channel.Equals("alliance", StringComparison.OrdinalIgnoreCase)
                || channel.Equals("al", StringComparison.OrdinalIgnoreCase))
            {
                tokens[1] = targetDice;
            }
            else if (int.TryParse(channel, out _))
            {
                tokens.Insert(1, targetDice);
            }
        }

        return leading + string.Join(' ', tokens);
    }

    private static bool TryExtractChatMessage(string trimmed, out string channel, out string message)
    {
        foreach (var (prefix, ch) in ChatPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                channel = ch;
                message = trimmed[prefix.Length..];
                return true;
            }
        }
        channel = string.Empty;
        message = string.Empty;
        return false;
    }

    private static bool TryExtractGroupChatMessage(string trimmed, out string channel, out string message)
    {
        if (TryExtractChatMessage(trimmed, out channel, out message)
            && (channel.Equals("Party", StringComparison.Ordinal)
                || channel.Equals("Alliance", StringComparison.Ordinal)))
            return true;

        channel = string.Empty;
        message = string.Empty;
        return false;
    }

    public static bool IsGroupChatMessageCommand(string commandText)
        => !string.IsNullOrWhiteSpace(commandText)
            && TryExtractGroupChatMessage(commandText.TrimStart(), out _, out _);

    public static bool TryGetAntiDoubleComparisonKey(string commandText, out string comparisonKey)
    {
        comparisonKey = string.Empty;
        if (string.IsNullOrWhiteSpace(commandText)
            || !TryExtractGroupChatMessage(commandText.TrimStart(), out _, out var message))
            return false;

        comparisonKey = NormalizeAntiDoubleMessage(message);
        return true;
    }

    public static string NormalizeAntiDoubleMessage(string message)
        => SoundEffectTokenRegex.Replace(message ?? string.Empty, string.Empty).Trim();

    private static bool IsSameAsLastGroupChatMessage(
        string message,
        out GeneratedGroupChatLine? lastGroupLine)
    {
        lock (GroupChatHistoryGate)
        {
            lastGroupLine = GroupChatHistory.Count > 0 ? GroupChatHistory[^1] : null;
            return lastGroupLine != null
                && message.Equals(lastGroupLine.Message, StringComparison.Ordinal);
        }
    }

    private static void RecordGroupChatMessage(
        string channel,
        string message,
        string routedCommandText,
        string? context)
    {
        lock (GroupChatHistoryGate)
        {
            GroupChatHistory.Add(new GeneratedGroupChatLine(
                DateTime.Now,
                channel,
                message,
                routedCommandText,
                context ?? string.Empty));
        }
    }

    public static IReadOnlyList<GeneratedGroupChatLine> GetGeneratedGroupChatHistory()
    {
        lock (GroupChatHistoryGate)
            return GroupChatHistory.ToList();
    }
}
