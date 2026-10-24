using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using BlackJackButtler.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.ChatMethods;
using ECommons.DalamudServices;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static ECommons.GenericHelpers;

namespace BlackJackButtler;

public static partial class PayoutManagement
{
    private static PlayerState? GetPayoutSource(IEnumerable<PlayerState> players)
    {
        return players.FirstOrDefault(
            x =>
                x.Name.Equals(_sourceName, StringComparison.OrdinalIgnoreCase)
                && (_sourceWorldId == 0 || x.WorldId == _sourceWorldId)
        );
    }

    private static PlayerState? GetPayoutRecipient()
    {
        return GetPayoutRecipient(Plugin.Instance.GetMainWindow().GetPlayers());
    }

    private static PlayerState? GetPayoutRecipient(IEnumerable<PlayerState> players)
    {
        return players.FirstOrDefault(
            x =>
                x.Name.Equals(_targetName, StringComparison.OrdinalIgnoreCase)
                && (_targetWorldId == 0 || x.WorldId == _targetWorldId)
        );
    }

    public static PlayerState? ResolveActivePayoutLedgerPlayer(string partnerName, List<PlayerState> players)
    {
        if (!IsActive || string.IsNullOrWhiteSpace(partnerName))
            return null;

        var recipient = GetPayoutRecipient(players);
        var detectedPartner = TradeManager.ResolvePlayer(partnerName, players);
        return recipient != null && ReferenceEquals(detectedPartner, recipient) ? GetPayoutSource(players) : null;
    }
}
