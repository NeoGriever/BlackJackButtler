using System;
using System.Numerics;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void SendPaymentTell(PlayerState p, long amount, string action)
    {
        var batch = _config.MessageBatches.FirstOrDefault(b => b.Name == "Payment Reminder");
        string raw = batch?.GetNextMessage() ?? "Please pay ${missingGil} gil for your ${action}.";

        string tReplacement = !string.IsNullOrWhiteSpace(p.Alias) ? p.Alias : "<t>";

        string processed = raw
            .Replace("<t>", tReplacement)
            .Replace("${missingGil}", amount.ToString("N0"))
            .Replace("${action}", action);

        ChatCommandRouter.Send($"/t <t> {processed}", _config, "PaymentTell");
    }
}
