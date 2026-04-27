using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace BlackJackButtler;

public sealed class PersistentRoundEntry
{
    public string Timestamp { get; set; } = "";
    public List<string> Lines { get; set; } = new();
    public List<string> TradeLines { get; set; } = new();
}

public static class RoundLogManager
{
    private const int NameWidth = 24;

    private static string _filePath = string.Empty;
    private static List<PersistentRoundEntry> _log = new();
    private static readonly List<(string playerKey, string line)> _pendingTrades = new();

    public static void Init(string configDir)
    {
        _filePath = Path.Combine(configDir, "bjb_roundlog.json");
        Load();
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return;
            _log = JsonConvert.DeserializeObject<List<PersistentRoundEntry>>(json) ?? new();
        }
        catch { _log = new(); }
    }

    public static void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_log, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[RoundLog] Save failed: {ex.Message}");
        }
    }

    public static List<PersistentRoundEntry> GetLog() => _log;

    public static void ClearLog()
    {
        _log.Clear();
        Save();
    }

    public static void AddTradeLine(string playerKey, string line)
    {
        _pendingTrades.Add((playerKey ?? "", line ?? ""));
    }

    public static void ClearPendingTrades() => _pendingTrades.Clear();

    public static void AddRound(PlayerState dealer, List<PlayerState> players, Configuration cfg)
    {
        var activePlayers = players.Where(x => x.IsActivePlayer && !x.IsOnHold).ToList();
        var pausedPlayers = players.Where(x => !x.IsActivePlayer || x.IsOnHold).ToList();

        if (activePlayers.Count == 0 && (dealer.Hands.Count == 0 || dealer.Hands[0].Cards.Count == 0))
        {
            _pendingTrades.Clear();
            return;
        }

        var lines = new List<string>();
        var tradeLinesForPersist = new List<string>();

        lines.Add(BuildDealerLine(dealer));

        foreach (var p in activePlayers)
        {
            lines.Add(BuildActivePlayerLine(p));
            var key = p.DisplayName;
            foreach (var trade in _pendingTrades.Where(t => KeyMatches(t.playerKey, key)).ToList())
            {
                lines.Add(trade.line);
                tradeLinesForPersist.Add(trade.line);
            }

            if (p.Hands.Count > 1)
            {
                for (int h = 0; h < p.Hands.Count; h++)
                    lines.Add(BuildSplitHandLine(p, h));
                lines.Add(BuildSplitFinalLine(p));
            }
        }

        foreach (var p in pausedPlayers)
        {
            lines.Add(BuildPausedLine(p));
        }

        long totalPlayerGain = activePlayers.Sum(p => p.Bank - p.BankAtRoundStart);
        string sign = totalPlayerGain > 0 ? "(-)" : totalPlayerGain < 0 ? "(+)" : "(=)";
        lines.Add($"Round outcome: {sign} {FormatGil(Math.Abs(totalPlayerGain))}");

        try
        {
            var entry = new PersistentRoundEntry
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                Lines = lines,
                TradeLines = tradeLinesForPersist
            };

            _log.Add(entry);
            Save();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[RoundLog] AddRound failed: {ex.Message}");
        }

        _pendingTrades.Clear();
    }

    private static bool KeyMatches(string tradeKey, string playerKey)
    {
        if (string.IsNullOrEmpty(tradeKey) || string.IsNullOrEmpty(playerKey)) return false;
        if (string.Equals(tradeKey, playerKey, StringComparison.OrdinalIgnoreCase)) return true;
        var tk = tradeKey.Split('@', 2)[0];
        var pk = playerKey.Split('@', 2)[0];
        return string.Equals(tk, pk, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDealerLine(PlayerState dealer)
    {
        if (dealer.Hands.Count == 0 || dealer.Hands[0].Cards.Count == 0)
            return "[ Dealer".PadRight(NameWidth + 2) + "| (no cards) ]";

        var hand = dealer.Hands[0];
        int score = dealer.GetBestScore(0);
        string scoreLabel = GetScoreLabel(hand, score);
        string actionSeq = BuildActionSequence(hand);

        return $"[ {"Dealer".PadRight(NameWidth)} | {actionSeq} {scoreLabel} ]";
    }

    private static string BuildActivePlayerLine(PlayerState p)
    {
        if (p.Hands.Count == 0)
            return $"[ {p.DisplayName.PadRight(NameWidth)} | (no cards) ]";

        var hand = p.Hands[0];
        int score = p.GetBestScore(0);
        string scoreLabel = p.Hands.Count > 1 ? "SPLIT" : GetScoreLabel(hand, score);

        string actionSeq = BuildActionSequence(hand);
        long baseBet = hand.Bet;
        if (hand.IsDoubleDown) baseBet /= 2;

        long bankStart = p.BankAtRoundStart;
        long bankAfterBet = bankStart - baseBet;
        long bankEnd = p.Bank;

        string name = p.DisplayName.Length > NameWidth ? p.DisplayName.Substring(0, NameWidth) : p.DisplayName.PadRight(NameWidth);
        string bankEndCell = p.Hands.Count > 1 ? new string('-', 11) : FormatGil(bankEnd);

        return $"[ {name} | {FormatGil(bankStart)} | {FormatGil(bankAfterBet)} | {actionSeq} {scoreLabel} | {bankEndCell} ]";
    }

    private static string BuildSplitHandLine(PlayerState p, int handIndex)
    {
        var hand = p.Hands[handIndex];
        int score = p.GetBestScore(handIndex);
        string scoreLabel = GetScoreLabel(hand, score);
        string actionSeq = BuildActionSequence(hand);
        string result = HandResultLabel(hand);

        return $"    [ HAND {handIndex + 1} | {actionSeq} {scoreLabel} | {result} ]";
    }

    private static string BuildSplitFinalLine(PlayerState p)
    {
        string name = p.DisplayName.Length > NameWidth ? p.DisplayName.Substring(0, NameWidth) : p.DisplayName.PadRight(NameWidth);
        string empty = new string(' ', 11);
        return $"[ {name} | {empty} | {empty} | {new string(' ', 40)} | {FormatGil(p.Bank)} ]";
    }

    private static string BuildPausedLine(PlayerState p)
    {
        string name = p.DisplayName.Length > NameWidth ? p.DisplayName.Substring(0, NameWidth) : p.DisplayName.PadRight(NameWidth);
        return $"[ {name} | {new string('-', 28)} Paused {new string('-', 28)} ]";
    }

    private static string HandResultLabel(HandState hand)
    {
        if (hand.IsBust) return "BUST";
        if (hand.IsCharlie) return "CH";
        if (hand.IsNaturalBlackJack) return "nBJ";
        return "";
    }

    private static string BuildActionSequence(HandState hand)
    {
        if (hand.Cards.Count == 0) return "(empty)";

        var sb = new StringBuilder();
        sb.Append(FormatCard(hand.Cards[0]));

        int cardIdx = 1;
        foreach (var action in hand.ActionLog)
        {
            sb.Append(':').Append(action);
            if ((action == "Hit" || action == "Bust" || action == "DD") && cardIdx < hand.Cards.Count)
            {
                sb.Append(':').Append(FormatCard(hand.Cards[cardIdx]));
                cardIdx++;
            }
        }

        while (cardIdx < hand.Cards.Count)
        {
            sb.Append(':').Append(FormatCard(hand.Cards[cardIdx]));
            cardIdx++;
        }

        return sb.ToString();
    }

    private static string GetScoreLabel(HandState hand, int score)
    {
        if (hand.IsBust) return "BUST";
        if (hand.IsCharlie) return "CH";
        if (score == 21 && hand.IsNaturalBlackJack) return "nBJ";
        if (score == 21) return "dBJ";
        return score.ToString();
    }

    public static string FormatTimestamp(string stored, int utcOffsetHours)
    {
        if (DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            var utc = parsed.UtcDateTime;
            var local = utc.AddHours(utcOffsetHours);
            string suffix = utcOffsetHours == 0 ? " UTC" : utcOffsetHours > 0 ? $" UTC+{utcOffsetHours}" : $" UTC{utcOffsetHours}";
            return local.ToString("MM/dd/yyyy hh:mm tt") + suffix;
        }
        return stored;
    }

    public static string FormatGil(long value)
    {
        long abs = Math.Abs(value);
        string digits = abs.ToString(CultureInfo.InvariantCulture).PadLeft(9, ' ');
        return $"{digits.Substring(0, 3)},{digits.Substring(3, 3)},{digits.Substring(6, 3)}";
    }

    public static string BuildTradeLineInbound(string playerDisplayName, long bankBefore, long bankAfter)
    {
        string name = playerDisplayName.Length > NameWidth ? playerDisplayName.Substring(0, NameWidth) : playerDisplayName.PadRight(NameWidth);
        return $"[ {name} | <<<<TRADE<<<< | {FormatGil(bankBefore)} | {FormatGil(bankAfter)} ]";
    }

    public static string BuildTradeLineOutbound(string playerDisplayName, long bankBefore, long bankAfter)
    {
        string name = playerDisplayName.Length > NameWidth ? playerDisplayName.Substring(0, NameWidth) : playerDisplayName.PadRight(NameWidth);
        return $"[ {name} | >>>>TRADE>>>> | {FormatGil(bankBefore)} | {FormatGil(bankAfter)} ]";
    }

    private static string FormatCard(DeckCard card)
    {
        return $"[{card.ValueLabel,2}]";
    }
}
