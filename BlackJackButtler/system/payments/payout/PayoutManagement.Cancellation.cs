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
    public static void Cancel()
    {
        _cancelRequested = true;
    }

    public static void Reset(string reason = "Reset")
    {
#if DEBUG
        if (_isResetting)
            return;
        _isResetting = true;
#endif
        try
        {
        if (IsActive)
            DebugLog($"[PayoutManagement] {reason}");

        Trace($"Reset: reason='{reason}', executor={_executor}, remaining={_remainingAmount}, chunk={_currentChunk}");

        if (_executor == TradeExecutor.ModularTrade && global::ModularTrade.TradeManager.IsBusy)
            global::ModularTrade.TradeManager.Cancel();

        var startNextQueuedPayout = reason.Equals("Payout complete", StringComparison.OrdinalIgnoreCase)
            && QueuedPayouts.Count > 0;

        _state = PayoutState.Idle;
        _sourceName = string.Empty;
        _sourceWorldId = 0;
        _targetName = string.Empty;
        _targetWorldId = 0;
        _startAmount = 0;
        _remainingAmount = 0;
        _lastKnownBank = 0;
        _currentChunk = 0;
        _cancelRequested = false;
        _openedDropboxUiForPayout = false;
        _bankBeforeCurrentTrade = 0;
        _executor = TradeExecutor.None;
        _modularTradeSessionId = null;
        _stateEnteredAt = DateTime.UtcNow;

        if (startNextQueuedPayout)
        {
            StartNextQueuedPayout();
        }
        else if (!reason.Equals("Payout complete", StringComparison.OrdinalIgnoreCase))
        {
            QueuedPayouts.Clear();
        }
        }
        finally
        {
#if DEBUG
            _isResetting = false;
#endif
        }
    }

    public static void NotifyTradeCancelled()
    {
        if (IsActive)
        {
            Trace($"TradeCancel system signal received: executor={_executor}, state={_state}, chunk={_currentChunk:N0}, remaining={_remainingAmount:N0}");
            Reset("Recipient cancelled trade");
        }
    }
}
