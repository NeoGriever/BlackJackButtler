using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static bool AllowZeroBetForSession { get; set; }

    public static bool CanStartInitialDeal(IEnumerable<PlayerState> players, out string reason)
    {
        var activePlayers = players
            .Where(p => p.IsActivePlayer && !p.IsOnHold)
            .ToList();

        if (activePlayers.Count == 0)
        {
            reason = "No active players are available.";
            return false;
        }

        if (!AllowZeroBetForSession)
        {
            var zeroBetPlayers = activePlayers
                .Where(p => p.CurrentBet == 0)
                .Select(p => p.DisplayName)
                .ToList();
            if (zeroBetPlayers.Count > 0)
            {
                reason = $"Bet is 0 for: {string.Join(", ", zeroBetPlayers)}";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static PlayerState? _pendingSplitPlayer = null;
    private static Configuration? _pendingSplitConfig = null;
    private static List<PlayerState>? _pendingSplitPlayers = null;
    private static PlayerState? _pendingDDPlayer = null;
    private static PlayerState? _pendingTDPlayer = null;
}
