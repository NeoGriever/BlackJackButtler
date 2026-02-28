using System;
using EA = ECommons.Automation;
using ECommons.DalamudServices;

namespace BlackJackButtler.Chat;

public static class ChatCommandRouter
{
    private static readonly (string prefix, string channel)[] ChatPrefixes =
    {
        ("/party ", "Party"), ("/p ", "Party"),
        ("/yell ", "Yell"), ("/y ", "Yell"),
        ("/shout ", "Shout"), ("/sh ", "Shout"),
        ("/tell ", "Tell"),
        ("/say ", "Say"), ("/s ", "Say"),
    };

    public static void Send(string commandText, Configuration cfg, string? context = null)
    {
        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog($"[Router-Request] Context: {context} | Cmd: {commandText}");

        if (ViewDirectionManager.IsInternalCommand(commandText))
        {
            ViewDirectionManager.ApplyViewDirection(cfg);
            window.AddDebugLog("[Router] /initialviewdirection intercepted - rotation applied");
            return;
        }

        if (Plugin.IsDebugMode)
        {
            var trimmed = commandText.TrimStart();

            if (TryExtractChatMessage(trimmed, out var channel, out var msgPart))
            {
                window.AddDebugLog($"[{channel}] {msgPart}", isChat: true);
            }
            else if (trimmed.StartsWith("/dice", StringComparison.OrdinalIgnoreCase))
            {
                Plugin.Instance.InjectChatMessage(64, 0, "SYSTEM", "SYSTEM", commandText);
            }
            else
            {
                window.AddDebugLog($"[Router-Debug] Skipped non-chat command: {commandText}");
            }
            return;
        }

        Svc.Framework.RunOnTick(() =>
        {
            try
            {
                window.AddDebugLog($"[Router-Dispatch] Sending to Chat: {commandText}");

                EA.Chat.SendMessage(commandText);
            }
            catch (Exception ex)
            {
                window.AddDebugLog($"[Router-CRITICAL] Crash during Send: {ex.GetType().Name} - {ex.Message}");
            }
        });
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
}
