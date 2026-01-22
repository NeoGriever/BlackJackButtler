using System;
using System.Collections.Generic;
using System.Linq;
using RRX = System.Text.RegularExpressions;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Regex;

public static class RegexEngine
{
    public static int? LastDetectedCardValue { get; private set; }

    private static int? TryParseBlackjackCardFromDice(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        return null;

        var m = RRX.Regex.Match(text, @"(\d+)\s*$");
        if (!m.Success) return null;

        if (!int.TryParse(m.Groups[1].Value, out var rolled))
        return null;

        return rolled switch
        {
            1 => 11,
            >= 2 and <= 9 => rolled,
            >= 10 and <= 13 => 10,
            _ => (int?)null
        };
    }

    public static void ProcessIncoming(ParsedChatMessage msg, Configuration cfg, List<PlayerState> players)
    {
        if (msg.Event)
        {
            var card = TryParseBlackjackCardFromDice(msg.Message);
            if (card.HasValue) LastDetectedCardValue = card.Value;
        }

        foreach (var entry in cfg.UserRegexes)
        {
            if (!entry.Enabled || string.IsNullOrWhiteSpace(entry.Pattern)) continue;
            var options = entry.CaseSensitive ? RRX.RegexOptions.Compiled : (RRX.RegexOptions.Compiled | RRX.RegexOptions.IgnoreCase);

            RRX.Regex rx;
            try { rx = new RRX.Regex(entry.Pattern, options); } catch { continue; }

            if (rx.IsMatch(msg.Message))
            {
                // Nur ausführen, wenn der Modus auf Trigger steht!
                if (entry.Mode == RegexEntryMode.Trigger)
                {
                    ExecuteAction(entry, msg.Name, players, cfg);
                }
                else if (entry.Mode == RegexEntryMode.SetVariable)
                {
                    VariableManager.SetVariable(entry.Name, msg.Message);
                }
            }

        }
    }

    private static void ExecuteAction(UserRegexEntry entry, string senderName, List<PlayerState> players, Configuration cfg)
    {
        var p = players.FirstOrDefault(x => x.Name == senderName);

        switch (entry.Action)
        {
            case RegexAction.BetInformationChange: if (p != null) p.HighlightBet = true; break;
            case RegexAction.WantHit: if (p != null) p.HighlightHit = true; break;
            case RegexAction.WantStand: if (p != null) p.HighlightStand = true; break;
            case RegexAction.WantDD: if (p != null) p.HighlightDD = true; break;
            case RegexAction.WantSplit: if (p != null) p.HighlightSplit = true; break;
            case RegexAction.BankOut: if (p != null) p.HighlightPay = true; break;

            case RegexAction.TakeBatch:
            var batch = cfg.MessageBatches.FirstOrDefault(b => b.Name == entry.ActionParam);
            if (batch != null)
            {
                string rawText = batch.GetNextMessage().Replace("<t>", senderName);
                string processedText = VariableManager.ProcessMessage(rawText);
                Plugin.CommandManager.ProcessCommand($"/p {processedText}");
            }
            break;
        }
    }
}
