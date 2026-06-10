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

        foreach (var player in all)
        {
            var firstName = GetFirstName(player.Name);
            var sameFirstName = all.Count(p =>
                GetFirstName(p.Name).Equals(firstName, StringComparison.OrdinalIgnoreCase));
            if (sameFirstName == 1)
            {
                player.ResolvedName = firstName;
                continue;
            }

            var sameFullName = all.Count(p =>
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

    private static string GetFirstName(string fullName)
    {
        var trimmed = fullName.Trim();
        var separator = trimmed.IndexOf(' ');
        return separator < 0 ? trimmed : trimmed[..separator];
    }
}
