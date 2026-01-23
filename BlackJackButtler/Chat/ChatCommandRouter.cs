using System;

namespace BlackJackButtler.Chat;

public static class ChatCommandRouter
{
    public static void Send(string commandText, Configuration cfg, string? context = null)
    {
        if (Plugin.IsDebugMode)
        {
            Plugin.Instance.InjectChatMessage(0, 0, "SYSTEM", "SYSTEM", commandText);
            return;
        }

        Plugin.CommandManager.ProcessCommand(commandText);
    }

}
