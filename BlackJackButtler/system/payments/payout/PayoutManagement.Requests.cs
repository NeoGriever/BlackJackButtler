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
    public static void StartPayout(PlayerState p)
    {
        _ = TryStartPayout(p, p.Bank);
    }

    public static PayoutStartResult TryStartPayout(PlayerState p, long amount)
    {
        if (amount <= 0)
            return PayoutStartResult.InvalidAmount;
        if (amount > p.Bank)
            return PayoutStartResult.InsufficientFunds;
        if (IsActive)
            return PayoutStartResult.AlreadyActive;

        var players = Plugin.Instance.GetMainWindow().GetPlayers();
        var recipient = PlayerIdentityManager.GetReferencedPlayer(players, p) ?? p;
        _sourceName = p.Name;
        _sourceWorldId = p.WorldId;
        _targetName = recipient.Name;
        _targetWorldId = recipient.WorldId;
        _startAmount = amount;
        _remainingAmount = amount;
        _lastKnownBank = p.Bank;
        _currentChunk = Math.Min(amount, MaxGilPerTrade);
        _cancelRequested = false;
        _openedDropboxUiForPayout = false;
        _bankBeforeCurrentTrade = p.Bank;
        _executor = TradeExecutor.None;

        SetState(PayoutState.Targeting, $"Started for {p.DisplayName}, payout={amount:N0}, bank={p.Bank:N0}, chunk={_currentChunk:N0}");
        return PayoutStartResult.Started;
    }

    public static PayoutStartResult TryStartPayoutSequence(IEnumerable<PlayerState> players)
    {
        if (IsActive)
            return PayoutStartResult.AlreadyActive;

        var requests = players
            .Where(player => player.Bank > 0)
            .Select(player => new QueuedPayout(player.Name, player.WorldId, player.Bank))
            .ToList();
        if (requests.Count == 0)
            return PayoutStartResult.InvalidAmount;

        QueuedPayouts.Clear();
        var firstRequest = requests[0];
        var firstPlayer = Plugin.Instance.GetMainWindow().GetPlayers().FirstOrDefault(
            player =>
                player.Name.Equals(firstRequest.PlayerName, StringComparison.OrdinalIgnoreCase)
                && (firstRequest.WorldId == 0 || player.WorldId == firstRequest.WorldId)
        );
        if (firstPlayer == null)
            return PayoutStartResult.InvalidAmount;

        var startResult = TryStartPayout(firstPlayer, firstRequest.Amount);
        if (startResult != PayoutStartResult.Started)
            return startResult;

        foreach (var request in requests.Skip(1))
            QueuedPayouts.Enqueue(request);

        return PayoutStartResult.Started;
    }
}
