using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackJackButtler;

public static class PlayerIdentityManager
{
    public static void Refresh(List<PlayerState> players, PlayerState dealer)
    {
        var all = players.Append(dealer)
            .Where(p => !string.IsNullOrWhiteSpace(p.Name)
                && (ReferenceEquals(p, dealer) || p.IsInParty || p.IsActivePlayer))
            .ToList();
        var realPlayers = all.Where(p => !p.IsImaginaryPlayer).ToList();

        foreach (var player in all)
        {
            if (player.IsImaginaryPlayer)
            {
                player.ResolvedName = player.Name;
                continue;
            }

            var firstName = GetFirstName(player.Name);
            var sameFirstName = realPlayers.Count(p =>
                GetFirstName(p.Name).Equals(firstName, StringComparison.OrdinalIgnoreCase));
            if (sameFirstName == 1)
            {
                player.ResolvedName = firstName;
                continue;
            }

            var sameFullName = realPlayers.Count(p =>
                p.Name.Equals(player.Name, StringComparison.OrdinalIgnoreCase));
            if (sameFullName == 1)
            {
                player.ResolvedName = player.Name;
                continue;
            }

            var world = VipManager.ResolveWorldName(player.WorldId);
            player.ResolvedName = string.IsNullOrWhiteSpace(world)
                ? player.Name
                : $"{player.Name}@{world}";
        }
    }

    public static PlayerState? Find(
        IEnumerable<PlayerState> players,
        PlayerState? dealer,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var candidates = dealer == null ? players.ToList() : players.Append(dealer).ToList();
        var exact = candidates.Where(p =>
                p.DisplayName.Equals(value, StringComparison.OrdinalIgnoreCase)
                || p.Name.Equals(value, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(p.ResolvedName)
                    && p.ResolvedName.Equals(value, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (exact.Count == 1)
            return exact[0];

        var firstNameMatches = candidates.Where(p =>
            GetFirstName(p.Name).Equals(value, StringComparison.OrdinalIgnoreCase)).ToList();
        return firstNameMatches.Count == 1 ? firstNameMatches[0] : null;
    }

    public static string GetQualifiedName(PlayerState player)
    {
        return !string.IsNullOrWhiteSpace(player.ResolvedName)
            ? player.ResolvedName
            : player.Name;
    }

    public static bool References(PlayerState imaginaryPlayer, PlayerState realPlayer)
    {
        return imaginaryPlayer.IsImaginaryPlayer
            && !realPlayer.IsImaginaryPlayer
            && imaginaryPlayer.ReferencedPlayerName.Equals(realPlayer.Name, StringComparison.OrdinalIgnoreCase)
            && (imaginaryPlayer.ReferencedPlayerWorldId == 0
                || realPlayer.WorldId == 0
                || imaginaryPlayer.ReferencedPlayerWorldId == realPlayer.WorldId);
    }

    public static PlayerState? GetReferencedPlayer(IEnumerable<PlayerState> players, PlayerState player)
    {
        if (!player.IsImaginaryPlayer)
            return null;

        return players.FirstOrDefault(candidate => References(player, candidate));
    }

    public static PlayerState? GetImaginaryPlayer(IEnumerable<PlayerState> players, PlayerState realPlayer)
    {
        return players.FirstOrDefault(candidate => References(candidate, realPlayer));
    }

    public static PlayerState ResolveMessageActionPlayer(IEnumerable<PlayerState> players, PlayerState sourcePlayer)
    {
        return players.FirstOrDefault(candidate =>
                   candidate.IsImaginaryPlayer
                   && candidate.IsActivePlayer
                   && candidate.IsCurrentTurn
                   && References(candidate, sourcePlayer))
               ?? sourcePlayer;
    }

    public static string GetFirstName(string fullName)
    {
        var trimmed = fullName.Trim();
        var separator = trimmed.IndexOf(' ');
        return separator < 0 ? trimmed : trimmed[..separator];
    }
}
