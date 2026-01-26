using System;
using System.Collections.Generic;
using System.Linq;
using RRX = System.Text.RegularExpressions;

namespace BlackJackButtler.Chat;

public static class TradeManager
{
    private static string? _currentPartner;
    private static long _buffer;

    public static void SetPartner(string name)
    {
        _currentPartner = name.Trim();
        _buffer = 0;
    }

    public static void AddGil(string rawAmount, bool isPositive)
    {
        long amount = ParseGil(rawAmount);
        if (isPositive) _buffer += amount;
        else _buffer -= amount;
    }

    public static void CommitTrade(List<PlayerState> players)
    {
        if (string.IsNullOrEmpty(_currentPartner)) return;

        var p = players.FirstOrDefault(x => x.Name.Equals(_currentPartner, StringComparison.OrdinalIgnoreCase));
        if (p != null && p.IsActivePlayer)
        {
            p.Bank += _buffer;
        }
        Reset();
    }

    public static void Reset()
    {
        _currentPartner = null;
        _buffer = 0;
    }

    private static long ParseGil(string input)
    {
        string cleaned = RRX.Regex.Replace(input, @"[^\d]", "");
        return long.TryParse(cleaned, out var val) ? val : 0;
    }
}
