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
    public List<string> PreRoundEvents { get; set; } = new();
    public List<string> PostRoundEvents { get; set; } = new();
}

public sealed class PayoutTrace
{
    public string ReceiverDisplayName { get; set; } = "";
    public long ReceiverBankBeforePayout { get; set; }
    public long ReceiverBankAfterPayout { get; set; }
    public List<PayoutLeg> Legs { get; set; } = new();
}

public sealed class PayoutLeg
{
    public string PayerDisplayName { get; set; } = "";
    public long PayerBankBefore { get; set; }
    public long PayerBankAfter { get; set; }
    public long Amount { get; set; }
}

public static class RoundLogManager
{
    private static string _filePath = string.Empty;
    private static List<PersistentRoundEntry> _log = new();
    private static readonly List<(string playerKey, string line)> _pendingTrades = new();
    private static readonly List<string> _pendingJoins = new();
    private static readonly List<string> _pendingLeaves = new();
    private static readonly List<PayoutTrace> _pendingPayouts = new();

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
        _pendingTrades.Clear();
        _pendingJoins.Clear();
        _pendingLeaves.Clear();
        _pendingPayouts.Clear();
        Save();
    }

    public static void AddTradeLine(string playerKey, string line)
    {
        _pendingTrades.Add((playerKey ?? "", line ?? ""));
    }

    public static void ClearPendingTrades() => _pendingTrades.Clear();

    public static void RecordPlayerJoin(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return;
        _pendingJoins.Add($"{displayName} joins");
    }

    public static void RecordPlayerLeave(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return;
        _pendingLeaves.Add($"{displayName} leaves");
    }

    public static void RecordPayoutTrace(PayoutTrace trace)
    {
        if (trace == null) return;
        _pendingPayouts.Add(trace);
    }

    public static void AddRound(PlayerState dealer, List<PlayerState> players, Configuration cfg)
    {
        var activePlayers = players.Where(x => x.IsActivePlayer && !x.IsOnHold).ToList();
        var pausedPlayers = players.Where(x => !x.IsActivePlayer || x.IsOnHold).ToList();

        if (activePlayers.Count == 0 && (dealer.Hands.Count == 0 || dealer.Hands[0].Cards.Count == 0))
        {
            _pendingTrades.Clear();
            _pendingJoins.Clear();
            _pendingLeaves.Clear();
            _pendingPayouts.Clear();
            return;
        }

        int gilWidth = ComputeGilWidth(activePlayers, dealer);

        var preEvents = new List<string>(_pendingJoins);

        var lines = new List<string>();
        lines.Add("--- Round Start ---");

        foreach (var p in activePlayers)
        {
            lines.Add(BuildActivePlayerLine(p, gilWidth));

            string key = p.DisplayName;
            foreach (var trade in _pendingTrades.Where(t => KeyMatches(t.playerKey, key)).ToList())
                lines.Add(trade.line);

            if (p.Hands.Count > 1)
            {
                for (int h = 0; h < p.Hands.Count; h++)
                    lines.Add(BuildSplitHandLine(p, h));
                lines.Add(BuildSplitFinalLine(p, gilWidth));
            }
        }

        lines.Add(BuildDealerLine(dealer));

        foreach (var p in pausedPlayers)
            lines.Add(BuildPausedLine(p));

        lines.Add("--- Round End ---");

        var postEvents = new List<string>();
        foreach (var trace in _pendingPayouts)
        {
            postEvents.Add(BuildPayoutHeaderLine(trace, gilWidth));
            foreach (var leg in trace.Legs)
                postEvents.Add(BuildPayoutDetailLine(leg, gilWidth));
        }
        postEvents.AddRange(_pendingLeaves);

        long totalPlayerGain = activePlayers.Sum(p => p.Bank - p.BankAtRoundStart);
        string sign = totalPlayerGain > 0 ? "(-)" : totalPlayerGain < 0 ? "(+)" : "(=)";
        lines.Add($"Round outcome: {sign} {FormatGilPadded(Math.Abs(totalPlayerGain), gilWidth)}");

        try
        {
            var entry = new PersistentRoundEntry
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                Lines = lines,
                TradeLines = _pendingTrades.Select(t => t.line).ToList(),
                PreRoundEvents = preEvents,
                PostRoundEvents = postEvents,
            };
            _log.Add(entry);
            Save();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[RoundLog] AddRound failed: {ex.Message}");
        }

        _pendingTrades.Clear();
        _pendingJoins.Clear();
        _pendingLeaves.Clear();
        _pendingPayouts.Clear();
    }

    private static bool KeyMatches(string tradeKey, string playerKey)
    {
        if (string.IsNullOrEmpty(tradeKey) || string.IsNullOrEmpty(playerKey)) return false;
        if (string.Equals(tradeKey, playerKey, StringComparison.OrdinalIgnoreCase)) return true;
        var tk = tradeKey.Split('@', 2)[0];
        var pk = playerKey.Split('@', 2)[0];
        return string.Equals(tk, pk, StringComparison.OrdinalIgnoreCase);
    }

    private static int ComputeGilWidth(List<PlayerState> activePlayers, PlayerState dealer)
    {
        long max = 0;
        foreach (var p in activePlayers)
        {
            max = Math.Max(max, Math.Abs(p.Bank));
            max = Math.Max(max, Math.Abs(p.BankAtRoundStart));
        }
        int digits = max <= 0 ? 1 : (int)Math.Floor(Math.Log10(max)) + 1;
        int padded = ((digits + 2) / 3) * 3;
        return Math.Max(9, padded);
    }

    private static string BuildDealerLine(PlayerState dealer)
    {
        if (dealer.Hands.Count == 0 || dealer.Hands[0].Cards.Count == 0)
            return "Dealer | (no cards)";

        var hand = dealer.Hands[0];
        int score = dealer.GetBestScore(0);
        string actions = BuildActionString(hand);
        string scoreLabel = GetScoreLabel(hand, score);
        return $"Dealer | {actions} | {scoreLabel}";
    }

    private static string BuildActivePlayerLine(PlayerState p, int gilWidth)
    {
        if (p.Hands.Count == 0)
            return $"{p.DisplayName} | (no cards)";

        var hand = p.Hands[0];
        int score = p.GetBestScore(0);
        string scoreLabel = p.Hands.Count > 1 ? "SPLIT" : GetScoreLabel(hand, score);
        string actions = BuildActionString(hand);

        long baseBet = hand.Bet;
        if (hand.IsDoubleDown) baseBet /= 2;

        long bankStart = p.BankAtRoundStart;
        long bankAfterBet = bankStart - baseBet;
        long bankEnd = p.Bank;

        string bankEndCell = p.Hands.Count > 1
            ? new string('-', gilWidth + (gilWidth / 3) - 1)
            : FormatGilPadded(bankEnd, gilWidth);

        return $"{p.DisplayName} | {FormatGilPadded(bankStart, gilWidth)} | {FormatGilPadded(bankAfterBet, gilWidth)} | {actions} | {scoreLabel} | {bankEndCell}";
    }

    private static string BuildSplitHandLine(PlayerState p, int handIndex)
    {
        var hand = p.Hands[handIndex];
        int score = p.GetBestScore(handIndex);
        string scoreLabel = GetScoreLabel(hand, score);
        string actions = BuildActionString(hand);
        string result = HandResultLabel(hand);
        return $"  HAND {handIndex + 1} | {actions} | {scoreLabel}{(string.IsNullOrEmpty(result) ? "" : " | " + result)}";
    }

    private static string BuildSplitFinalLine(PlayerState p, int gilWidth)
    {
        return $"{p.DisplayName} | {FormatGilPadded(p.Bank, gilWidth)}";
    }

    private static string BuildPausedLine(PlayerState p)
    {
        return $"{p.DisplayName} | -------- Paused --------";
    }

    public static string BuildPayoutHeaderLine(PayoutTrace trace, int gilWidth)
    {
        return $"{trace.ReceiverDisplayName} | {FormatGilPadded(trace.ReceiverBankBeforePayout, gilWidth)} | {FormatGilPadded(trace.ReceiverBankAfterPayout, gilWidth)} | PAYOUT";
    }

    public static string BuildPayoutDetailLine(PayoutLeg leg, int gilWidth)
    {
        return $"{leg.PayerDisplayName} | {FormatGilPadded(leg.PayerBankBefore, gilWidth)} - {FormatGilPadded(leg.Amount, gilWidth)} | {FormatGilPadded(leg.PayerBankAfter, gilWidth)}";
    }

    private static string HandResultLabel(HandState hand)
    {
        if (hand.IsBust) return "bust";
        if (hand.IsCharlie) return "charlie";
        if (hand.IsNaturalBlackJack) return "nBJ";
        return "";
    }

    private static string BuildActionString(HandState hand)
    {
        if (hand.Cards.Count == 0) return "(empty)";

        var parts = new List<string>();
        parts.Add(FormatCard(hand.Cards[0]));

        int cardIdx = 1;
        foreach (var action in hand.ActionLog)
        {
            string a = action.ToLowerInvariant();
            if (a == "stand") { parts.Add("stand"); continue; }
            if (a == "split") { parts.Add("split"); continue; }
            if (a == "hit")
            {
                parts.Add("hit");
                if (cardIdx < hand.Cards.Count) parts.Add(FormatCard(hand.Cards[cardIdx++]));
                continue;
            }
            if (a == "bust")
            {
                parts.Add("bust");
                if (cardIdx < hand.Cards.Count) parts.Add(FormatCard(hand.Cards[cardIdx++]));
                continue;
            }
            if (a == "dd")
            {
                parts.Add("dd");
                if (cardIdx < hand.Cards.Count) parts.Add(FormatCard(hand.Cards[cardIdx++]));
                continue;
            }
            parts.Add(a);
        }

        while (cardIdx < hand.Cards.Count)
            parts.Add(FormatCard(hand.Cards[cardIdx++]));

        return string.Join(", ", parts);
    }

    private static string GetScoreLabel(HandState hand, int score)
    {
        if (hand.IsBust) return score.ToString();
        if (hand.IsCharlie) return "CH";
        if (score == 21 && hand.IsNaturalBlackJack) return "nBJ";
        if (score == 21) return "21";
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

    public static string FormatGilPadded(long value, int width)
    {
        long abs = Math.Abs(value);
        string raw = abs.ToString(CultureInfo.InvariantCulture);
        int targetDigits = Math.Max(width, raw.Length);
        targetDigits = ((targetDigits + 2) / 3) * 3;
        string padded = raw.PadLeft(targetDigits, '_');

        var sb = new StringBuilder();
        for (int i = 0; i < padded.Length; i++)
        {
            if (i > 0 && (padded.Length - i) % 3 == 0) sb.Append(',');
            sb.Append(padded[i]);
        }
        return sb.ToString();
    }

    public static string BuildTradeLineInbound(string playerDisplayName, long bankBefore, long bankAfter)
    {
        long delta = Math.Abs(bankAfter - bankBefore);
        return $"{playerDisplayName} | {FormatGilPadded(bankBefore, 9)} + {FormatGilPadded(delta, 9)} | {FormatGilPadded(bankAfter, 9)}";
    }

    public static string BuildTradeLineOutbound(string playerDisplayName, long bankBefore, long bankAfter)
    {
        long delta = Math.Abs(bankAfter - bankBefore);
        return $"{playerDisplayName} | {FormatGilPadded(bankBefore, 9)} - {FormatGilPadded(delta, 9)} | {FormatGilPadded(bankAfter, 9)}";
    }

    private static string FormatCard(DeckCard card)
    {
        string label = card.ValueLabel.Trim();
        if (label == "10") label = "X";
        return $"<{label}>";
    }
}
