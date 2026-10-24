using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    private static bool IsPlayerFinished(PlayerState p)
    {
        return p.Hands.Count > 0 && p.Hands.All(h => h.IsStand || h.IsBust || h.IsNaturalBlackJack || h.IsCharlie);
    }

    private static List<PlayerState> GetActivePlayers(List<PlayerState> players)
    {
        return players.Where(p => p.IsActivePlayer && !p.IsOnHold && !p.IsOnBench && !p.JoinedMidRound).ToList();
    }

    private static List<PlayerState> GetBenchPlayers(List<PlayerState> players)
    {
        return players.Where(p => p.IsActivePlayer && p.IsOnBench && !p.IsOnHold).ToList();
    }

    public static bool CanMovePlayerToBench(PlayerState player, List<PlayerState> allPlayers)
    {
        if (player.IsOnBench) return false;

        var activePlayers = GetActivePlayers(allPlayers);
        var benchPlayers = GetBenchPlayers(allPlayers);

        int remainingActive = activePlayers.Count(p => p != player);
        if (remainingActive == 0 && benchPlayers.Count == 0)
            return false;

        return true;
    }

    public static void MovePlayerToBench(PlayerState player, List<PlayerState> allPlayers)
    {
        if (!CanMovePlayerToBench(player, allPlayers)) return;

        player.IsOnBench = true;
        player.IsOnHold = false;
        player.BenchedAt = DateTime.UtcNow;
        player.WasOnHoldThisRound = true;
        if (player.IsCurrentTurn)
            player.IsCurrentTurn = false;

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Bench] {player.DisplayName} moved to bench.", false);
    }

    public static void MovePlayerFromBench(PlayerState player)
    {
        if (!player.IsOnBench && !player.IsOnHold) return;

        player.IsOnBench = false;
        player.IsOnHold = false;
        player.BenchedAt = DateTime.MinValue;

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Bench] {player.DisplayName} returned from bench.", false);
    }

    private static PlayerState? ActivateFirstBenchPlayer(List<PlayerState> players)
    {
        var benchPlayers = GetBenchPlayers(players);
        if (benchPlayers.Count == 0) return null;

        var first = benchPlayers
            .OrderBy(p => p.BenchedAt == DateTime.MinValue ? DateTime.MaxValue : p.BenchedAt)
            .First();

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Bench] Activating first FIFO bench player: {first.DisplayName}.", false);
        MovePlayerFromBench(first);
        return first;
    }

    public static void DeactivatePlayerMidRound(PlayerState player, List<PlayerState> allPlayers, Configuration cfg)
    {
        bool wasCurrentTurn = player.IsCurrentTurn;

        if (player.HasInitialHandDealt)
        {
            foreach (var hand in player.Hands)
                player.Bank += hand.Bet;
        }

        RoundLogManager.RecordPlayerLeave(player.DisplayName);

        player.Hands.Clear();
        player.Hands.Add(new HandState(player.CurrentBet));
        player.IsActivePlayer = false;
        player.IsCurrentTurn = false;
        player.IsOnBench = false;
        player.HasInitialHandDealt = false;
        player.ReadySkip = false;

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Engine] {player.DisplayName} deactivated mid-round (bet refunded: {player.HasInitialHandDealt}).", false);

        if (wasCurrentTurn && CurrentPhase != GamePhase.Waiting && CurrentPhase != GamePhase.Payout)
            NextTurn(allPlayers, cfg);

        SaveSessionIfNeeded(allPlayers);
        CompanionSyncManager.ClearPlayer(cfg, player);
    }
}
