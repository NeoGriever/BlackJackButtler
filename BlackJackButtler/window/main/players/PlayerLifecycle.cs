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
    private void ClearPlayer(PlayerState p, bool bankToTip)
    {
        if (bankToTip && p.Bank > 0)
            StatsManager.AddTip(p.Bank);
        p.Bank = 0;
        p.CurrentBet = 0;
        if (TryRemoveEmptyInactiveGeneratedPlayer(p))
            return;
        CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
    }

    private void ActivatePlayer(PlayerState player)
    {
        player.HighlightJoin = false;
        player.IsActivePlayer = true;
        var joinPhase = GameEngine.CurrentPhase;
        if (joinPhase == GamePhase.InitialDeal || joinPhase == GamePhase.PlayersTurn || joinPhase == GamePhase.DealerTurn)
            player.JoinedMidRound = true;
        ActivityLogManager.LogPlayerJoin(player.DisplayName);
        RoundLogManager.RecordPlayerJoin(player.DisplayName);
        SaveSessionFromUI();
        CompanionSyncManager.SendPlayerBankBetUpdate(_config, player);
    }

    private void RemovePlayersWithCompanionErase(Predicate<PlayerState> match)
    {
        for (var i = _players.Count - 1; i >= 0; i--)
        {
            var player = _players[i];
            if (!match(player))
                continue;

            CompanionSyncManager.ClearPlayer(_config, player);
            _players.RemoveAt(i);
        }
    }

    private void ClearPlayersWithCompanionErase()
    {
        foreach (var player in _players)
            CompanionSyncManager.ClearPlayer(_config, player);

        _players.Clear();
    }

    private void AddImaginaryPlayer(PlayerState referencedPlayer)
    {
        if (referencedPlayer.IsImaginaryPlayer || referencedPlayer.IsDebugPlayer
            || _players.Any(player => PlayerIdentityManager.References(player, referencedPlayer)))
            return;

        var phase = GameEngine.CurrentPhase;
        var joinedMidRound = phase == GamePhase.InitialDeal
            || phase == GamePhase.PlayersTurn
            || phase == GamePhase.DealerTurn;
        var ghost = new PlayerState
        {
            IsImaginaryPlayer = true,
            ReferencedPlayerName = referencedPlayer.Name,
            ReferencedPlayerWorldId = referencedPlayer.WorldId,
            Name = $"{PlayerIdentityManager.GetFirstName(referencedPlayer.Name)} Ghost",
            WorldId = referencedPlayer.WorldId,
            IsActivePlayer = true,
            IsInParty = false,
            JoinedMidRound = joinedMidRound,
            Bank = referencedPlayer.Bank,
            CurrentBet = referencedPlayer.CurrentBet,
            BankAtRoundStart = referencedPlayer.Bank,
        };
        ghost.Hands.Add(new HandState(ghost.CurrentBet));

        var referenceIndex = _players.IndexOf(referencedPlayer);
        _players.Insert(referenceIndex >= 0 ? referenceIndex + 1 : _players.Count, ghost);
        PlayerIdentityManager.Refresh(_players, _dealer);
        GameEngine.SetRuntimeContext(_players, _dealer);
        ActivityLogManager.LogPlayerJoin(ghost.DisplayName);
        RoundLogManager.RecordPlayerJoin(ghost.DisplayName);
        AddDebugLog($"[ImaginaryPlayer] Added {ghost.DisplayName}, controlled by {referencedPlayer.DisplayName}", false);
        SaveSessionFromUI();
    }

    private bool TryRemoveEmptyInactiveGeneratedPlayer(PlayerState player)
    {
        bool isRemovableDebugPlayer = Plugin.IsDebugMode && player.IsDebugPlayer;
        if ((!isRemovableDebugPlayer && !player.IsImaginaryPlayer)
            || player.IsActivePlayer || player.Bank != 0)
            return false;

        CompanionSyncManager.ClearPlayer(_config, player);
        _players.Remove(player);
        var kind = player.IsImaginaryPlayer ? "imaginary" : "debug";
        AddDebugLog($"[Player] Removed inactive zero-bank {kind} player: {player.DisplayName}", false);
        _save();
        return true;
    }
}
