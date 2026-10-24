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
    private static bool TryStartModularTradePayout(PlayerState player, int gil)
    {
        var isBusy = global::ModularTrade.TradeManager.IsBusy;
        if (!_modularTradeInitialized || isBusy)
        {
            Trace($"Integrated ModularTrade unavailable: initialized={_modularTradeInitialized}, busy={isBusy}");
            return false;
        }

        if (FindPlayerObject(player.Name, player.WorldId) is not IPlayerCharacter character)
        {
            Trace($"Integrated ModularTrade could not resolve recipient {player.DisplayName}.");
            return false;
        }

        try
        {
            var recipient = new Sender(character).ToString();
            Trace($"Starting integrated ModularTrade recipient={recipient}, gil={gil}.");
            global::ModularTrade.TradeManager.Trade(recipient, gil);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[PayoutManagement] Integrated ModularTrade could not start.");
            return false;
        }
    }

    private static void OnModularTradeChanged(
        global::ModularTrade.TradeEventType type,
        global::ModularTrade.TradeSnapshot snapshot
    )
    {
        Trace($"ModularTrade event={type}, state={snapshot.State}, step={snapshot.CurrentStep}/{snapshot.TotalSteps}, confirmed={snapshot.ConfirmedAmount}/{snapshot.TotalAmount}, reason={snapshot.FailureReason}");
        if (!IsActive || _executor != TradeExecutor.ModularTrade)
            return;

        switch (type)
        {
            case global::ModularTrade.TradeEventType.Success:
                SetState(PayoutState.TradeClosedSettle, $"Integrated ModularTrade verified {_currentChunk:N0} Gil");
                break;

            case global::ModularTrade.TradeEventType.Cancel:
                Reset($"Integrated ModularTrade stopped: {snapshot.FailureReason}; verified={snapshot.ConfirmedAmount:N0}");
                break;
        }
    }
}
