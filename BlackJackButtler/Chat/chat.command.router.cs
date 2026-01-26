using System;
using EA = ECommons.Automation;
using ECommons.DalamudServices;

namespace BlackJackButtler.Chat;

public static class ChatCommandRouter
{
    public static void Send(string commandText, Configuration cfg, string? context = null)
    {
        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog($"[Router-Request] Context: {context} | Cmd: {commandText}");

        if (Plugin.IsDebugMode)
        {
            Plugin.Instance.InjectChatMessage(64, 0, "SYSTEM", "SYSTEM", commandText);
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
}
