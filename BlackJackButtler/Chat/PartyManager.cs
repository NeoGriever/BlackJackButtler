using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackJackButtler.Chat;

public static class PartyManager
{
    public static void HandleJoin(string name, List<PlayerState> players)
    {
        var p = players.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (p != null)
        {
            p.IsInParty = true;
        }
        else
        {
            players.Add(new PlayerState { Name = name, IsInParty = true, IsActivePlayer = false });
        }
    }

    public static void HandleLeave(string name, List<PlayerState> players)
    {
        var p = players.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (p != null)
        {
            p.IsInParty = false;
            if (!p.IsActivePlayer && p.Bank == 0) 
            {
                players.Remove(p);
            }
        }
    }

    public static void HandleDisband(List<PlayerState> players)
    {
        foreach (var p in players) p.IsInParty = false;
        players.RemoveAll(x => !x.IsActivePlayer && x.Bank == 0);
    }
}
