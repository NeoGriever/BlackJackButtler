using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace BlackJackButtler;

public sealed class PersistentRoundEntry
{
    public string Timestamp { get; set; } = "";
    public List<string> Lines { get; set; } = new();
}

public static class RoundLogManager
{
    private static string _filePath = string.Empty;
    private static List<PersistentRoundEntry> _log = new();

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

    public static void AddRound(PlayerState dealer, List<PlayerState> players, Configuration cfg)
    {
        var activePlayers = players.Where(x => x.IsActivePlayer && !x.IsOnHold).ToList();
        if (activePlayers.Count == 0 && (dealer.Hands.Count == 0 || dealer.Hands[0].Cards.Count == 0))
            return;

        var lines = new List<string>();

        var dealerLines = BuildDealerLines(dealer);
        lines.AddRange(dealerLines);
        lines.Add("");

        foreach (var p in activePlayers)
        {
            var playerLines = BuildPlayerLines(p, cfg);
            lines.AddRange(playerLines);
            lines.Add("");
        }

        long totalPlayerGain = activePlayers.Sum(p => p.Bank - p.BankAtRoundStart);
        string sign = totalPlayerGain > 0 ? "(-)" : totalPlayerGain < 0 ? "(+)" : "(=)";
        lines.Add($"Round outcome: {sign} {FormatGil(Math.Abs(totalPlayerGain))}");

        try
        {
            var est = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, est);
            var timestamp = now.ToString("MM/dd/yyyy hh:mm tt") + " EST";

            var entry = new PersistentRoundEntry
            {
                Timestamp = timestamp,
                Lines = lines
            };

            _log.Add(entry);
            Save();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[RoundLog] AddRound failed: {ex.Message}");
        }
    }

    private static List<string> BuildDealerLines(PlayerState dealer)
    {
        var lines = new List<string>();
        if (dealer.Hands.Count == 0 || dealer.Hands[0].Cards.Count == 0)
            return lines;

        var hand = dealer.Hands[0];
        int score = dealer.GetBestScore(0);
        string scoreLabel = GetScoreLabel(hand, score);

        lines.Add(BuildHeader("Dealer", scoreLabel));

        var sb = new StringBuilder("  ");
        sb.Append(FormatCard(hand.Cards[0]));

        int cardIdx = 1;
        foreach (var action in hand.ActionLog)
        {
            sb.Append(':').Append(action);
            if ((action == "Hit" || action == "Bust") && cardIdx < hand.Cards.Count)
            {
                sb.Append(':').Append(FormatCard(hand.Cards[cardIdx]));
                cardIdx++;
            }
        }

        if (hand.ActionLog.Count > 0)
        {
            var lastAction = hand.ActionLog[^1];
            if (lastAction == "Stand" || lastAction == "Bust")
                sb.Append(';').Append(scoreLabel);
        }

        lines.Add(sb.ToString());
        return lines;
    }

    private static List<string> BuildPlayerLines(PlayerState player, Configuration cfg)
    {
        var lines = new List<string>();
        if (player.Hands.Count == 0) return lines;

        bool isSplit = player.Hands.Count > 1;

        var scoreLabels = new List<string>();
        for (int h = 0; h < player.Hands.Count; h++)
        {
            int score = player.GetBestScore(h);
            scoreLabels.Add(GetScoreLabel(player.Hands[h], score));
        }

        string headerScore = isSplit
            ? string.Join(",.", scoreLabels)
            : scoreLabels[0];

        lines.Add(BuildHeader(player.DisplayName, headerScore));
        lines.Add($"  {FormatGil(player.BankAtRoundStart)}");

        if (isSplit)
        {
            var firstHand = player.Hands[0];
            long baseBet = player.CurrentBet;
            lines.Add($"  {FormatGil(baseBet)}");

            int splitActionIdx = firstHand.ActionLog.IndexOf("Split");
            if (splitActionIdx >= 0)
            {
                for (int a = 0; a < splitActionIdx; a++)
                {
                    string action = firstHand.ActionLog[a];
                    int cardsShown = 2 + a + 1;
                    string cards = FormatCardsRange(firstHand.Cards, 0, Math.Min(cardsShown, firstHand.Cards.Count));
                    lines.Add($"    {cards}:{action}");
                }

                DeckCard splitCard1 = default;
                DeckCard splitCard2 = default;
                if (firstHand.Cards.Count >= 1) splitCard1 = firstHand.Cards[0];
                if (player.Hands.Count > 1 && player.Hands[1].Cards.Count >= 1)
                    splitCard2 = player.Hands[1].Cards[0];

                string splitCards = $"{FormatCard(splitCard1)},{FormatCard(splitCard2)}";
                lines.Add($"  {splitCards}:Split");
                lines.Add($"  {FormatGil(baseBet)}");
            }

            char letter = 'A';
            for (int h = 0; h < player.Hands.Count; h++)
            {
                var hand = player.Hands[h];
                string prefix = $"{letter} ";
                letter++;

                var handActions = hand.ActionLog.Where(a => a != "Split").ToList();
                int initialCards = 1;
                int hitsSoFar = 0;

                for (int a = 0; a < handActions.Count; a++)
                {
                    string action = handActions[a];
                    bool isLast = a == handActions.Count - 1;

                    if (action == "Hit" || action == "Bust")
                    {
                        hitsSoFar++;
                        int cardsToShow = initialCards + hitsSoFar;
                        string cards = FormatCardsRange(hand.Cards, 0, Math.Min(cardsToShow, hand.Cards.Count));
                        string suffix = isLast ? $":{action};{GetScoreLabel(hand, player.GetBestScore(h))}" : $":{action}";
                        lines.Add($"{prefix}{cards}{suffix}");
                    }
                    else if (action == "DD")
                    {
                        int cardsToShow = initialCards + hitsSoFar;
                        string cardsBefore = FormatCardsRange(hand.Cards, 0, Math.Min(cardsToShow, hand.Cards.Count));
                        lines.Add($"{prefix}{cardsBefore}:DD");
                        lines.Add($"  {FormatGil(baseBet)}");
                        hitsSoFar++;
                        string allCards = FormatCardsRange(hand.Cards, 0, hand.Cards.Count);
                        lines.Add($"{prefix}{allCards}:Stand;{GetScoreLabel(hand, player.GetBestScore(h))}");
                    }
                    else if (action == "Stand")
                    {
                        string allCards = FormatCardsRange(hand.Cards, 0, hand.Cards.Count);
                        lines.Add($"{prefix}{allCards}:Stand;{GetScoreLabel(hand, player.GetBestScore(h))}");
                    }
                }

                if (handActions.Count == 0 && hand.Cards.Count > 0)
                {
                    string allCards = FormatCardsRange(hand.Cards, 0, hand.Cards.Count);
                    string label = GetScoreLabel(hand, player.GetBestScore(h));
                    lines.Add($"{prefix}{allCards}:{label}");
                }
            }
        }
        else
        {
            var hand = player.Hands[0];
            long baseBet = hand.Bet;
            if (hand.IsDoubleDown) baseBet /= 2;
            lines.Add($"  {FormatGil(baseBet)}");

            int initialCards = 2;
            int hitsSoFar = 0;

            for (int a = 0; a < hand.ActionLog.Count; a++)
            {
                string action = hand.ActionLog[a];
                bool isLast = a == hand.ActionLog.Count - 1;

                if (action == "Hit" || action == "Bust")
                {
                    hitsSoFar++;
                    int cardsToShow = initialCards + hitsSoFar;
                    string cards = FormatCardsRange(hand.Cards, 0, Math.Min(cardsToShow, hand.Cards.Count));
                    string suffix = isLast ? $":{action};{GetScoreLabel(hand, player.GetBestScore(0))}" : $":{action}";
                    lines.Add($"    {cards}{suffix}");
                }
                else if (action == "DD")
                {
                    int cardsToShow = initialCards + hitsSoFar;
                    string cardsBefore = FormatCardsRange(hand.Cards, 0, Math.Min(cardsToShow, hand.Cards.Count));
                    lines.Add($"    {cardsBefore}:DD");
                    lines.Add($"  {FormatGil(baseBet)}");
                    hitsSoFar++;
                    string allCards = FormatCardsRange(hand.Cards, 0, hand.Cards.Count);
                    lines.Add($"    {allCards}:Stand;{GetScoreLabel(hand, player.GetBestScore(0))}");
                }
                else if (action == "Stand")
                {
                    string allCards = FormatCardsRange(hand.Cards, 0, hand.Cards.Count);
                    lines.Add($"    {allCards}:Stand;{GetScoreLabel(hand, player.GetBestScore(0))}");
                }
            }

            if (hand.ActionLog.Count == 0 && hand.Cards.Count > 0)
            {
                string allCards = FormatCardsRange(hand.Cards, 0, hand.Cards.Count);
                string label = GetScoreLabel(hand, player.GetBestScore(0));
                lines.Add($"    {allCards}:{label}");
            }
        }

        lines.Add($"  {FormatGil(player.Bank)}");
        return lines;
    }

    private static string BuildHeader(string name, string score)
    {
        const int width = 50;
        int dotsNeeded = width - name.Length - score.Length;
        if (dotsNeeded < 1) dotsNeeded = 1;
        return name + new string('.', dotsNeeded) + score;
    }

    private static string GetScoreLabel(HandState hand, int score)
    {
        if (hand.IsBust) return ">21";
        if (score == 21 && hand.IsNaturalBlackJack) return "n21";
        if (score == 21) return "d21";
        return score.ToString();
    }

    public static string FormatGil(long value)
    {
        string formatted = Math.Abs(value).ToString("000,000,000");
        var chars = formatted.ToCharArray();
        bool leadingZero = true;
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] == ',') continue;
            if (leadingZero && chars[i] == '0')
                chars[i] = '-';
            else
                leadingZero = false;
        }
        return new string(chars);
    }

    private static string FormatCard(DeckCard card)
    {
        return $"[{card.ValueLabel,2}]";
    }

    private static string FormatCardsRange(List<DeckCard> cards, int start, int count)
    {
        var parts = new List<string>();
        for (int i = start; i < start + count && i < cards.Count; i++)
            parts.Add(FormatCard(cards[i]));
        return string.Join(",", parts);
    }
}
