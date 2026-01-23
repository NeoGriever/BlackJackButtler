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
            "Player Deal Hand":                ["Dealing [<t>]'s hand ..."],
            "Player State Messages HSDS":      ["[<t>] You have <points>. Do you want to hit, stand, double down or split?"],
            "Player State Messages HSD":       ["[<t>] You have <points>. Do you want to hit, stand or double down?"],
            "Player State Messages HS":        ["[<t>] You have <points>. Do you want to hit or stand?"],

            "Player Draw Messages":            ["[<t>] needs another card. Your card is ..."],
            "Player Stand Messages":           ["[<t>] decides to keep the given hand. Good Luck."],
            "Player DD Messages":              ["[<t>] want to play a risky game? - Good~ - DOUBLE DOWN!"],
            "Player DD Messages Stand":        ["[<t>] has to stand after a double down. Good luck."]
            "Player Split Messages":           ["[<t>] splits the hand. Okay let's see~"],

            "Player BlackJack Messages":       ["Wohoo. [<t>] got a natural blackjack. Congrats!"],
            "Player BlackJack Messages Shout": ["Wohoo. [<t>] got a natural blackjack. Congrats to [<t>]!"],

            "Player Dirty BlackJack Messages": ["Wohoo. [<t>] got a blackjack. Fantastic!"],

            "Player Busts Messages":           ["Oh no. [<t>] got busted with <points>."],

            "Dealer Draw Messages":            ["Let's get it started. The dealers first card is ..."],
            "Dealer Hit Messages":             ["Let fortune show me, that my next card is better!", "I need another one!", "Give me another card, miss fortune ...", "What would be my next card?", "One further card."],
            "Dealer Stands Messages":          ["Okay. Let's end this here. - Let's see who won ..."],
            "Dealer Blackjack Messages":       ["Wohoo. The Dealer got very lucky. BLACKJACK BABY!"],
            "Dealer Busts Messages":           ["Oh nooo. I was too greedy ... This <points> killed me"],

            "Hand Reaction Messages":          ["Will <points> be enough for [<t>] to win?"],

            "Win Messages":                    ["[<t>] won this round with <points>."],
            "Push Messages":                   ["[<t>] got pushed with <points>. He will get his bet back. Except of the DD bet."],
            "Bust Messages":                   ["[<t>] busted with <points>. I'm so sorry."],
            "Lost Messages":                   ["[<t>] did not with with <points>."],
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
                { "Text": "/p #{Player Deal Hand}", "Delay": 1.2 },
                { "Text": "/dice party 13", "Delay": 0.9 },
                { "Text": "/dice party 13", "Delay": 0.9 }
            ],
            "Hit": [
                { "Text": "/p #{Player Draw Messages}", "Delay": 1.2 },
                { "Text": "/dice party 13", "Delay": 0.9 }
            ],
            "Stand": [
                { "Text": "/p #{Player Stand Messages}", "Delay": 1.2 },
                { "Text": "/p #{Hand Reaction Messages}", "Delay": 1.2 }
            ],
            "DD": [
                { "Text": "/p #{Player DD Messages}", "Delay": 1.2 },
                { "Text": "/dice party 13", "Delay": 1.3 },
                { "Text": "/p #{Player DD Messages Stand}", "Delay": 0.8 }
            ],
            "Split": [
                { "Text": "/p #{Player Split Messages}", "Delay": 1.2 },
                { "Text": "/dice party 13", "Delay": 0.9 }
            ],
            "PlayerBJ": [
                { "Text": "/p #{Player BlackJack Messages}", "Delay": 1.3 },
                { "Text": "/shout #{Player BlackJack Messages Shout}", "Delay": 1.9 }
            ],
            "PlayerDirtyBJ": [
                { "Text": "/p #{Player Dirty BlackJack Messages}", "Delay": 0.9 }
            ],
            "PlayerBust": [
                { "Text": "/p #{Player Busts Messages}", "Delay": 0.9 }
            ],
            "DealStart": [
                { "Text": "/p #{Dealer Draw Messages}", "Delay": 0.7 },
                { "Text": "/dice party 13", "Delay": 0.9 }
            ],
            "DealHit": [
                { "Text": "/p #{Dealer Hit Messages}", "Delay": 1.2 },
                { "Text": "/dice party 13", "Delay": 0.9 }
            ],
            "DealStand": [
                { "Text": "/p #{Dealer Stands Messages}", "Delay": 1.2 }
            ],
            "DealerBJ": [
                { "Text": "/p #{Dealer Blackjack Messages}", "Delay": 1.2 }
            ],
            "DealerBust": [
                { "Text": "/p #{Dealer Busts Messages}", "Delay": 0.9 }
            ],
            "ResultPlayerWin": [
                { "Text": "/p #{Win Messages}", "Delay": 0.9 }
            ],
            "ResultPlayerPush": [
                { "Text": "/p #{Push Messages}", "Delay": 0.9 }
            ],
            "ResultPlayerBusted": [
                { "Text": "/p #{Bust Messages}", "Delay": 0.9 }
            ],
            "ResultPlayerLost": [
                { "Text": "/p #{Lost Messages}", "Delay": 0.9 }
            ]
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
