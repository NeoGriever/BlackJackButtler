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
    public static void Tick()
    {
        if (_modularTradeInitialized)
            global::ModularTrade.TradeManager.Update();
        if (!IsActive) return;

        if (_cancelRequested)
        {
            Reset("Cancelled");
            return;
        }

        var player = GetCurrentPlayer();
        var recipient = GetPayoutRecipient();
        if (player == null || recipient == null)
        {
            Reset($"Payout player not found: {_sourceName}");
            return;
        }

        if (_remainingAmount <= 0)
        {
            Reset("Payout complete");
            return;
        }

        var settlementPending = _state is PayoutState.WaitingTradeClose
            or PayoutState.TradeClosedSettle;
        if (player.Bank <= 0 && !settlementPending)
        {
            Reset("Bank reached 0 before requested payout completed");
            return;
        }

        _lastKnownBank = player.Bank;

        switch (_state)
        {
            case PayoutState.Targeting:
                TargetPayoutPlayer(recipient);
                SetState(PayoutState.OpeningTrade, "Target/focus target set");
                break;

            case PayoutState.OpeningTrade:
                if (StateElapsed < TargetSettleSeconds) return;
                if (!IsPayoutTargetSelected())
                {
                    TargetPayoutPlayer(recipient);
                    return;
                }

                _currentChunk = Math.Min(_remainingAmount, Math.Min(player.Bank, MaxGilPerTrade));
                if (_currentChunk <= 0)
                {
                    Reset("No Gil available for remaining payout");
                    return;
                }
                _bankBeforeCurrentTrade = player.Bank;

                var dropbox = GetDropboxStatus();
                if (ShouldUseDropbox(dropbox))
                {
                    if (TryStartDropboxPayout(recipient, (int)_currentChunk))
                    {
                        _executor = TradeExecutor.Dropbox;
                        SetState(PayoutState.WaitingTradeOpen, "Dropbox payout task queued");
                        return;
                    }

                    Reset("Dropbox is active but its trade task could not be started");
                    return;
                }

                if (TryStartModularTradePayout(recipient, (int)_currentChunk))
                {
                    _executor = TradeExecutor.ModularTrade;
                    SetState(PayoutState.WaitingTradeOpen, "Integrated ModularTrade payout started");
                    return;
                }
                Reset("Integrated ModularTrade executor could not start");
                break;

            case PayoutState.WaitingTradeOpen:
                if (_executor == TradeExecutor.ModularTrade)
                    return;

                if (IsTradeOpen())
                {
                    SetState(PayoutState.WaitingTradeClose, "Dropbox opened trade");
                    return;
                }
                if (StateElapsed >= DropboxTradeOpenTimeoutSeconds)
                    Reset("Timed out waiting for Trade addon");
                break;

            case PayoutState.WaitingTradeClose:
                if (!IsTradeOpen())
                    SetState(PayoutState.TradeClosedSettle, "Trade closed");
                break;

            case PayoutState.TradeClosedSettle:
                if (StateElapsed < TradeClosedSettleSeconds) return;
                var transferred = _bankBeforeCurrentTrade - player.Bank;
                if (transferred <= 0)
                {
                    Reset("Trade cancelled or closed without payout");
                    return;
                }
                if (transferred != _currentChunk)
                {
                    Reset($"Unexpected payout amount: expected {_currentChunk:N0}, received {transferred:N0}");
                    return;
                }

                _remainingAmount -= _currentChunk;
                if (_remainingAmount <= 0)
                {
                    Reset("Payout complete");
                    return;
                }
                if (player.Bank <= 0)
                {
                    Reset("Bank reached 0 before requested payout completed");
                    return;
                }
                _executor = TradeExecutor.None;
                SetState(PayoutState.Targeting, $"Remaining payout={_remainingAmount:N0}, bank={player.Bank:N0}, continuing");
                break;
        }
    }
}
