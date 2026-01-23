using System;
using System.Collections.Generic;
using System.Linq;
using BlackJackButtler.Regex;
using Newtonsoft.Json;

namespace BlackJackButtler;

public static class DefaultsManager
{
    private const string RawJson = """
    {
      "Messages": {
        "Welcome Messages": ["Welcome to BlackJack. I'm your dealer today. I wish everyone good luck."],
        "New Player Messages": ["Hello <t>. Welcome to my BlackJack table."],
        "Collecting Bets Messages": ["Collecting bets right now. Just trade to me."],
        "Dealer Draw Messages": ["Let's get it started. The dealers first card is ..."],
        "Player draw Messages": ["<t> needs another card. Your card is ..."],
        "Hand Reaction Messages": ["Will it be enough for <t> to win?"],
        "Win Messages": ["<t> won this round."],
        "Push Messages": ["<t> got pushed."],
        "Bust Messages": ["<t> busted. I'm so sorry."],
        "Blackjack Dealer Messages": ["Wohoo. I got a blackjack."],
        "BlackJack Player Messages": ["Wohoo. <t> got a blackjack."]
      },
      "TradeRegex": [
        { "Name": "Trade: Inbound", "Patterns": ["^(.+) möchte mit dir handeln\\.$"], "Action": "TradePartner" },
        { "Name": "Trade: Outbound", "Patterns": ["^Du hast (.+) einen Handel angeboten\\.$"], "Action": "TradePartner" },
        { "Name": "Trade: Gil In", "Patterns": ["^Du erhältst ([\\d.]+) Gil\\.$"], "Action": "TradeGilIn" },
        { "Name": "Trade: Gil Out", "Patterns": ["^Du überreichst ([\\d.]+) Gil\\.$"], "Action": "TradeGilOut" },
        { "Name": "Trade: Success", "Patterns": ["^Der Handel wurde abgeschlossen\\.$"], "Action": "TradeCommit" },
        { "Name": "Trade: Cancel", "Patterns": ["^Der Handel wurde abgebrochen\\.$"], "Action": "TradeCancel" },
        { "Name": "Dice: Blackjack Logic", "Patterns": ["(\\d+)\\s*$"], "Action": "DiceRollValue" }
      ],
      "Commands": {
        "Initial": [
          { "Text": "/p #{PlayerStarts}", "Delay": 0.4 },
          { "Text": "/dice party 13", "Delay": 0.4 },
          { "Text": "/dice party 13", "Delay": 0.4 }
        ],
        "Hit": [
          { "Text": "/p #{PlayerHits}", "Delay": 0.4 },
          { "Text": "/dice party 13", "Delay": 0.4 }
        ],
        "Stand": [{ "Text": "/p #{PlayerStands}", "Delay": 0.4 }],
        "DD": [
          { "Text": "/p #{PlayerDoubleDowns}", "Delay": 0.4 },
          { "Text": "/dice party 13", "Delay": 0.4 }
        ],
        "Split": [{ "Text": "/p #{PlayerSplits}", "Delay": 0.4 }],
        "PlayerBJ": [
          { "Text": "/p #{PlayerNatBJ}", "Delay": 0.4 },
          { "Text": "/shout #{PlayerNatBJShout}", "Delay": 0.4 }
        ],
        "PlayerDirtyBJ": [{ "Text": "/p #{PlayerDirtyBJ}", "Delay": 0.4 }],
        "PlayerBust": [{ "Text": "/p #{PlayerBusts}", "Delay": 0.4 }],
        "DealStart": [
          { "Text": "/p #{DealerStarts}", "Delay": 0.4 },
          { "Text": "/dice party 13", "Delay": 0.4 }
        ],
        "DealHit": [
          { "Text": "/p #{DealerHits}", "Delay": 0.4 },
          { "Text": "/dice party 13", "Delay": 0.4 }
        ],
        "DealStand": [{ "Text": "/p #{DealerStands}", "Delay": 0.4 }],
        "DealerBJ": [{ "Text": "/p #{DealerBlackjack}", "Delay": 0.4 }],
        "DealerBust": [{ "Text": "/p #{DealerBusts}", "Delay": 0.4 }],
        "ResultPlayerWin": [{ "Text": "/p <t> won!", "Delay": 0.1 }],
        "ResultPlayerPush": [{ "Text": "/p <t> got pushed!", "Delay": 0.1 }],
        "ResultPlayerBusted": [{ "Text": "/p <t> busted!", "Delay": 0.1 }],
        "ResultPlayerLost": [{ "Text": "/p <t> lost!", "Delay": 0.1 }]
      }
    }
    """;

    public static List<MessageBatch> GetDefaultMessages() {
        try {
            var data = JsonConvert.DeserializeObject<DefaultsContainer>(RawJson);
            if (data?.Messages == null) return new();
            return data.Messages.Select(kvp => new MessageBatch { Name = kvp.Key, Messages = kvp.Value }).ToList();
        } catch (Exception) { return new(); }
    }

    public static List<UserRegexEntry> GetDefaultRegex() {
        try {
            var data = JsonConvert.DeserializeObject<DefaultsContainer>(RawJson);
            if (data?.TradeRegex == null) return new();
            return data.TradeRegex.Select(r => new UserRegexEntry {
                Name = r.Name ?? "Unknown",
                Patterns = r.Patterns ?? new(),
                Action = Enum.TryParse<RegexAction>(r.Action, out var act) ? act : RegexAction.None,
                Mode = RegexEntryMode.Trigger,
                Enabled = true
            }).ToList();
        } catch (Exception) { return new(); }
    }

    public static List<CommandGroup> GetDefaultCommands() {
        try {
            var data = JsonConvert.DeserializeObject<DefaultsContainer>(RawJson);
            if (data?.Commands == null) return new();
            return data.Commands.Select(kvp => {
                var g = new CommandGroup { Name = kvp.Key };
                g.Commands.AddRange(kvp.Value.Select(c => new PluginCommand { Text = c.Text ?? "", Delay = c.Delay }));
                return g;
            }).ToList();
        } catch (Exception) { return new(); }
    }

    private class DefaultsContainer {
        public Dictionary<string, List<string>>? Messages { get; set; }
        public List<TradeRegexDto>? TradeRegex { get; set; }
        public Dictionary<string, List<CommandDto>>? Commands { get; set; }
    }
    private class TradeRegexDto {
        public string? Name { get; set; }
        public List<string>? Patterns { get; set; }
        public string? Action { get; set; }
    }
    private class CommandDto {
        public string? Text { get; set; }
        public float Delay { get; set; }
    }
}
