using System;
using System.Collections.Generic;
using System.Linq;
using RRX = System.Text.RegularExpressions;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Regex;

public static class RegexEngine
{
    public static int? LastDetectedCardValue { get; private set; }

    public static void ProcessIncoming(ParsedChatMessage msg, Configuration cfg, List<PlayerState> players)
    {
        foreach (var entry in cfg.UserRegexes)
        {
            if (!entry.Enabled || string.IsNullOrWhiteSpace(entry.Pattern)) continue;
            var options = entry.CaseSensitive ? RRX.RegexOptions.Compiled : (RRX.RegexOptions.Compiled | RRX.RegexOptions.IgnoreCase);

            RRX.Regex rx;
            try { rx = new RRX.Regex(entry.Pattern, options); } catch { continue; }

            if (rx.IsMatch(msg.Message))
            {
                if (entry.Mode == RegexEntryMode.Trigger)
                {
                    ExecuteAction(entry, msg, players, cfg);
                }
                else if (entry.Mode == RegexEntryMode.SetVariable)
                {
                    VariableManager.SetVariable(entry.Name, msg.Message);
                }
            }
        }
    }

    private static void ExecuteAction(UserRegexEntry entry, ParsedChatMessage msg, List<PlayerState> players, Configuration cfg)
    {
        var p = players.FirstOrDefault(x => x.Name.Equals(msg.Name, StringComparison.OrdinalIgnoreCase));
        var match = RRX.Regex.Match(msg.Message, entry.Pattern, RRX.RegexOptions.IgnoreCase);

        switch (entry.Action)
        {
            case RegexAction.DiceRollValue:
                if (msg.Event && match.Success && match.Groups.Count >= 2)
                {
                    if (int.TryParse(match.Groups[1].Value, out var rolled))
                    {
                        LastDetectedCardValue = MapValue(rolled);
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
                    string rawText = batch.GetNextMessage().Replace("<t>", msg.Name);
                    string processedText = VariableManager.ProcessMessage(rawText);
                    Plugin.CommandManager.ProcessCommand($"/p {processedText}");
                }
                break;

            case RegexAction.PartyJoin:
                if (match.Success && match.Groups.Count >= 2)
                    PartyManager.HandleJoin(match.Groups[1].Value, players);
                break;

            case RegexAction.PartyLeave:
                if (match.Success && match.Groups.Count >= 2)
                    PartyManager.HandleLeave(match.Groups[1].Value, players);
                break;

            case RegexAction.PartyDisband:
                PartyManager.HandleDisband(players);
                break;
        }
    }

    private static int? MapValue(int rolled)
    {
        return rolled switch
        {
            1 => 11,
            >= 2 and <= 9 => rolled,
            >= 10 and <= 13 => 10,
            _ => null
        };
    }
}
