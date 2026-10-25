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
        if (player.Bank <= 0 && !settlementPending
            && !(_executor == TradeExecutor.ModularTrade
                && global::ModularTrade.TradeManager.IsBusy))
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
                _lastReconciliationDelta = long.MinValue;

                var dropbox = GetDropboxStatus();
                var useDropbox = ShouldUseDropbox(dropbox);
                Trace($"Executor selection: Dropbox installed={dropbox.Installed}, active={dropbox.Active}, selected={(useDropbox ? "Dropbox" : "ModularTrade")}, chunk={_currentChunk:N0}, remaining={_remainingAmount:N0}, bank={player.Bank:N0}");
                if (useDropbox)
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

                var modularAmount = Math.Min(_remainingAmount, player.Bank);
                if (TryStartModularTradePayout(recipient, modularAmount))
                {
                    _currentChunk = modularAmount;
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
                    SetState(PayoutState.TradeClosedSettle, "Dropbox trade closed; awaiting ledger reconciliation");
                break;

            case PayoutState.TradeClosedSettle:
                if (StateElapsed < TradeClosedSettleSeconds) return;
                var transferred = _bankBeforeCurrentTrade - player.Bank;
                // V4: reconciliation observation. A closed trade window is not proof of payment.
                if (transferred != _lastReconciliationDelta)
                {
                    _lastReconciliationDelta = transferred;
                    Trace($"Ledger reconciliation: executor={_executor}, expected={_currentChunk:N0}, recorded={transferred:N0}, remainingBefore={_remainingAmount:N0}, elapsed={StateElapsed:F1}s");
                }
                if (transferred > _currentChunk)
                {
                    Reset($"Ledger overpayment/overlap: executor={_executor}, expected={_currentChunk:N0}, recorded={transferred:N0}; stopped without automatic retry");
                    return;
                }
                if (transferred < _currentChunk)
                {
                    if (StateElapsed < TradeLedgerReconcileTimeoutSeconds)
                        return;
                    Reset($"Ledger reconciliation timeout: executor={_executor}, expected={_currentChunk:N0}, recorded={transferred:N0}, remaining={_remainingAmount:N0}; stopped without automatic retry");
                    return;
                }

                Trace($"Chunk reconciled: executor={_executor}, paid={_currentChunk:N0}, remainingAfter={_remainingAmount - _currentChunk:N0}, playerBank={player.Bank:N0}");
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
