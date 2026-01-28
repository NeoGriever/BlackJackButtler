using System;
using System.Collections.Generic;
using System.Linq;
using RRX = System.Text.RegularExpressions;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Regex;

public static class RegexEngine
{
    public static int? LastDetectedCardValue { get; private set; }

    public static bool TryConsumeDetectedCard(out int cardValue)
    {
        if (LastDetectedCardValue.HasValue)
        {
            cardValue = LastDetectedCardValue.Value;
            LastDetectedCardValue = null;
            return true;
        }
        cardValue = 0;
        return false;
    }

    public static int? MapRollToCard(int rolled) => MapValue(rolled);

    public static void ProcessIncoming(ParsedChatMessage msg, Configuration cfg, List<PlayerState> players, PlayerState dealer)
    {
        foreach (var entry in cfg.UserRegexes)
        {
            if (!entry.Enabled || entry.Patterns == null || entry.Patterns.Count == 0) continue;

            foreach (var pattern in entry.Patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;

                var options = entry.CaseSensitive ? RRX.RegexOptions.Compiled : (RRX.RegexOptions.Compiled | RRX.RegexOptions.IgnoreCase);
                RRX.Regex rx;
                try { rx = new RRX.Regex(pattern, options); } catch { continue; }

                if (rx.IsMatch(msg.Message))
                {
                    if (entry.Mode == RegexEntryMode.Trigger)
                    {
                        ExecuteAction(entry, pattern, msg, players, dealer, cfg);
                    }
                    else if (entry.Mode == RegexEntryMode.SetVariable)
                    {
                        VariableManager.SetVariable(entry.Name, msg.Message);
                    }
                    break;
                }
            }
        }
    }

    private static void ExecuteAction(UserRegexEntry entry, string matchedPattern, ParsedChatMessage msg, List<PlayerState> players, PlayerState dealer, Configuration cfg)
    {
        var p = players.FirstOrDefault(x => x.Name.Equals(msg.Name, StringComparison.OrdinalIgnoreCase));

        var options = entry.CaseSensitive ? RRX.RegexOptions.None : RRX.RegexOptions.IgnoreCase;
        var match = RRX.Regex.Match(msg.Message, matchedPattern, options);

        switch (entry.Action)
        {
            case RegexAction.DiceRollValue:
                if (match.Success && match.Groups.Count >= 2)
                {
                    if (int.TryParse(match.Groups[1].Value, out var rolled))
                    {
                        var card = MapValue(rolled);
                        if (card.HasValue)
                        {
                            LastDetectedCardValue = card.Value;
                            DiceResultHandler.HandleDiceResult(card.Value, cfg, players, dealer);
                        }
                    }
                }
                break;

            case RegexAction.TradePartner:
                if (match.Success && match.Groups.Count >= 2)
                    TradeManager.SetPartner(match.Groups[1].Value);
                break;

            case RegexAction.TradeGilIn:
                if (match.Success && match.Groups.Count >= 2)
                    TradeManager.AddGil(match.Groups[1].Value, true);
                break;

            case RegexAction.TradeGilOut:
                if (match.Success && match.Groups.Count >= 2)
                    TradeManager.AddGil(match.Groups[1].Value, false);
                break;

            case RegexAction.TradeCommit:
                TradeManager.CommitTrade(players);
                break;

            case RegexAction.TradeCancel:
                TradeManager.Reset();
                break;

            case RegexAction.BetInformationChange:
                if (p != null) p.HighlightBet = true;
                break;

            case RegexAction.WantHit:
                if (p != null) p.HighlightHit = true;
                break;

            case RegexAction.WantStand:
                if (p != null) p.HighlightStand = true;
                break;

            case RegexAction.WantDD:
                if (p != null) p.HighlightDD = true;
                break;

            case RegexAction.WantSplit:
                if (p != null) p.HighlightSplit = true;
                break;

            case RegexAction.BankOut:
                if (p != null) p.HighlightPay = true;
                break;

            case RegexAction.TakeBatch:
                var batch = cfg.MessageBatches.FirstOrDefault(b => b.Name == entry.ActionParam);
                if (batch != null)
                {
                    string rawText = batch.GetNextMessage(); // Erstmal zum test auslassen ... .Replace("<t>", msg.Name);
                    string processedText = VariableManager.ProcessMessage(rawText);
                    ChatCommandRouter.Send($"/p {processedText}", cfg, $"Batch:{batch.Name}->{msg.Name}");
                }
                break;
        }
    }

    private static int? MapValue(int rolled)
    {
        if (rolled >= 1 && rolled <= 13) return rolled;
        return null;
    }
}
