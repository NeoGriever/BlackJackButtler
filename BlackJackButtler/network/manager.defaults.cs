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
        "TradeRegex": [
            { "Name": "Trade: Inbound", "Patterns": [
                "^(.+) möchte mit dir handeln\\.$",
                "^(.+) wishes to trade with you\\.$"
            ], "Action": "TradePartner" },
            { "Name": "Trade: Outbound", "Patterns": [
                "^Du hast (.+) einen Handel angeboten\\.$",
                "^Trade request sent to (.+)$"
            ], "Action": "TradePartner" },
            { "Name": "Trade: Gil In", "Patterns": [
                "^Du erhältst ([\\d.]+) Gil\\.$",
                "^You receive ([\\d.]+) gil\\.$"
            ], "Action": "TradeGilIn" },
            { "Name": "Trade: Gil Out", "Patterns": [
                "^Du überreichst ([\\d.]+) Gil\\.$",
                "^You hand over ([\\d.]+) gil\\.$"
            ], "Action": "TradeGilOut" },
            { "Name": "Trade: Success", "Patterns": [
                "^Der Handel wurde abgeschlossen\\.$",
                "^Trade complete\\.$"
            ], "Action": "TradeCommit" },
            { "Name": "Trade: Cancel", "Patterns": [
                "^Der Handel wurde abgebrochen\\.$",
                "^Trade canceled\\.$"
            ], "Action": "TradeCancel" },
            { "Name": "Dice: Blackjack Logic", "Patterns": [
                "Würfeln! .*?\\) (\\d+)\\s*$",
                "Random! .*?\\) (\\d+)\\s*$"
            ], "Action": "DiceRollValue" }
        ],
        "Commands": {
            "Initial": [
                { "Text": "/p #{Dividor}", "Delay": 1.2 },
                { "Text": "/p #{Player Deal Hand}", "Delay": 1.2 },
                { "Text": "/beam motion", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 1.2 }
            ],
            "Hit": [
                { "Text": "/p #{Player Draw Messages}", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 0.8 },
                { "Text": "/p #{Dividor}", "Delay": 0.8 }
            ],
            "Stand": [
                { "Text": "/thumbsup motion", "Delay": 0.8 },
                { "Text": "/p #{Player Stand Messages}", "Delay": 1.2 },
                { "Text": "/p #{Hand Reaction Messages}", "Delay": 1.0 },
                { "Text": "/p #{Dividor}", "Delay": 0.8 }
            ],
            "DD": [
                { "Text": "/p #{Player DD Messages}", "Delay": 1.2 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 1.3 },
                { "Text": "/p #{Player DD Messages Stand}", "Delay": 1.3 },
                { "Text": "/p #{Dividor}", "Delay": 0.8 }
            ],
            "Split": [
                { "Text": "/p #{Player Split Messages}", "Delay": 0.8 },
                { "Text": "/beam motion", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 0.8 }
            ],
            "PlayerBJ": [
                { "Text": "/p #{Player BlackJack Messages} <se.7>", "Delay": 1.3 },
                { "Text": "/thumbsup motion", "Delay": 0.8 },
                { "Text": "/shout #{Player BlackJack Messages Shout}", "Delay": 0.8 }
            ],
            "PlayerDirtyBJ": [
                { "Text": "/p #{Player Dirty BlackJack Messages} <se.7>", "Delay": 1.3 },
                { "Text": "/thumbsup motion", "Delay": 0.8 },
            ],
            "PlayerBust": [
                { "Text": "/p #{Player Busts Messages} <se.11>", "Delay": 0.8 },
                { "Text": "/upset", "Delay": 2.5 }
            ],
            "DealStart": [
                { "Text": "/p #{Dealer Draw Messages} <se.1>", "Delay": 0.8 },
                { "Text": "/beam motion", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 0.8 }
            ],
            "DealHit": [
                { "Text": "/p #{Dealer Hit Messages}", "Delay": 0.8 },
                { "Text": "/beam motion", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 0.8 }
            ],
            "DealStand": [
                { "Text": "/p #{Dealer Stands Messages}", "Delay": 1.5 }
            ],
            "DealerBJ": [
                { "Text": "/p #{Dealer Blackjack Messages} <se.7>", "Delay": 0.8 },
                { "Text": "/vpose", "Delay": 3.5 }
            ],
            "DealerBust": [
                { "Text": "/p #{Dealer Busts Messages} <se.11>", "Delay": 0.8 },
                { "Text": "/facepalm", "Delay": 3.5 }
            ],
            "ResultPlayerWin": [
                { "Text": "/p #{Win Messages}", "Delay": 1.9 }
            ],
            "ResultPlayerPush": [
                { "Text": "/p #{Push Messages}", "Delay": 1.9 }
            ],
            "ResultPlayerBusted": [
                { "Text": "/p #{Bust Messages}", "Delay": 1.9 }
            ],
            "ResultPlayerLost": [
                { "Text": "/p #{Lost Messages}", "Delay": 1.9 }
            ],
            "StateHSDS": [
                { "Text": "/p #{Player State Messages HSDS} <se.7>", "Delay": 0.8 }
            ],
            "StateHSD":  [
                { "Text": "/p #{Player State Messages HSD} <se.7>", "Delay": 0.8 }
            ],
            "StateHS":   [
                { "Text": "/p #{Player State Messages HS} <se.7>", "Delay": 0.8 }
            ],
            "ResultSmall": [
                { "Text": "/p  Results: <results> ", "Delay": 0.9 }
            ]
        },
        "Messages": {
            "Dividor":                         [
                "========================="
            ],

            "Player Deal Hand":                [
                " Here are your cards now, <t>. ",
                " Let me deal your hand, <t>. ",
                " Two cards for you, <t>. ",
                " What does the cards say for <t>? ",
                " <t> get's 2 cards. "
            ],

            "Player State Messages HSDS":      [
                " <t> - You have splittable <points> with ${playerCards}. Do you want to [Hit], [Stand], [Double Down] or [Split]? "
            ],

            "Player State Messages HSD":       [
                " <t> - You have <points> with ${playerCards}. Do you want to [Hit], [Stand] or [Double Down]? "
            ],

            "Player State Messages HS":        [
                " <t> - You have <points> with ${playerCards}. Do you want to [Hit] or [Stand]? "
            ],

            "Player Draw Messages":            [
                " <t> want another card? Then <t> will get another card~. ",
                " <t> want a card - here it is. ",
                " <t> decides to hit. ",
                " And with that, <t> gets another card. ",
                " I'll draw another card for <t>. "
            ],

            "Player Stand Messages":           [
                " <t> decides to keep the given hand. Good Luck. ",
                " This hand stands now on <points> for <t>. "
            ],

            "Player DD Messages":              [
                " <t> want to play a risky game? - DOUBLE DOWN! - Take this card and live with the consequences~. ",
                " Double Down? Did you say DOUBLE DOWN, <t>? - Well, you want it, you get it! ",
                " Double bet, double chance. Let's see, what fortuna wanna do with <t>'s hand now. ",
                " DD! - Let this card speak! ",
                " <t> wants another card. The last card for <t>'s hand. - Rolling drumsssss~. "
            ],

            "Player DD Messages Stand":        [
                " <t> wanna stand. Keep your hand now and good luck~. ",
                " The choice is clear. This hand stays at <points> for <t>"
            ],

            "Player Split Messages":           [
                " <t> splits the hand. Okay let's see~. ",
                " This two cards getting divided to 2 hands. What will <t> do with them? ",
                " <t> want to split. Let's gooo~. "
            ],

            "Player BlackJack Messages":       [
                " Wohoo. <t> got a NATURAL BLACKJACK! Congrats! ",
                " FANTASTIC. A NATURAL BLACKJACK FOR <t>! Congrats! "
            ],

            "Player BlackJack Messages Shout": [
                "Wohoo. <t> got a natural blackjack. Congrats to <t>!"
            ],

            "Player Dirty BlackJack Messages": [
                " Wohoo. <t> got a blackjack. Fantastic! ",
                " Unbelievable. A BLACKJACK FOR <t>! CONGRATS! "
            ],

            "Player Busts Messages":           [
                " Oh no. <t> got busted with <points>. ",
                " Thats bad luck. <t> busted with <points>. ",
                " Ouch! <t> busted at <points>. ",
                " Bust! <t> pushed it to <points> and paid the price. ",
                " Dealer smiles. <t> went over with <points>. ",
                " Too hot to handle - <t> burned out at <points>. ",
                " Unlucky! <t> cracked with <points>. ",
                " And that's a bust - <t> hits <points>. ",
                " Over 21 alert: <t> landed on <points>. ",
                " Greed got the best of <t> at <points>. ",
                " One card too far - <t> ended on <points>. ",
                " The cards said 'nope.” <t> busted with <points>. ",
                " Risky business - <t> busted at <points>. ",
                " <t> went full send… to <points>. Bust. ",
                " The dealer thanks you, <t>: <points> is a bust. ",
                " Close? Not really. <t> busted with <points>. ",
                " Math check failed: <t> reached <points>. ",
                " You hate to see it - <t> busted at <points>. ",
                " <t> hit <points> and instantly regretted it. ",
                " That last hit was spicy - <t> busted with <points>. ",
                " Bold move, <t>. <points> is still a bust. ",
                " Dealer: 'I'll allow it.” Rules: 'No.” <t> has <points>. ",
                " <t> chased 21 and caught <points>. Bust. ",
                " Congratulations, <t> - you found <points> the hard way. ",
                " House wins this round: <t> busted with <points>. ",
                " Fortune favors the bold… not <t> at <points>. ",
                " <t> tried to outsmart the deck and got <points>. ",
                " Too many hits, not enough sense - <t> busted at <points>. ",
                " <t> went overboard with <points>. ",
                " The deck giveth, the deck busteth: <t> at <points>. ",
                " <t> zigged when they should've stayed - <points>. ",
                " <t> just invented a new number: <points> (aka bust). "
            ],

            "Dealer Draw Messages": [
                " New round on the table - dealer starts off with a card. ",
                " Alright, fresh hand incoming. Dealer draws the opener. ",
                " Let's kick this round off - dealer reveals the first card. ",
                " The house makes the first move. Dealer draws. ",
                " Place your bets and brace yourself - dealer flips the opening card. ",
                " And we're live. Dealer draws to set the tone for this round. ",
                " Round start: the dealer breaks the silence with the first card. ",
                " Cards up - dealer opens the round with a draw. ",
                " Here we go again. Dealer draws the first card and the table wakes up. ",
                " The deck speaks first - dealer draws to begin the round. ",
                " New round, same nerves. Dealer reveals the opening card. ",
                " Let the drama begin - dealer pulls the first card. ",
                " The table is set and the dealer starts the story with one card. ",
                " First card down - dealer opens the round with a draw. ",
                " Starting whistle: dealer draws the opener and we're in. ",
                " Another round begins. Dealer draws and the tension returns. ",
                " Eyes on the felt - dealer flips the first card to start off. ",
                " No turning back now. Dealer draws the opening card. ",
                " Fresh round, fresh fate - dealer reveals what we're up against. ",
                " The house sets the pace - dealer draws the first card. ",
                " Let's see what kind of round this will be - dealer opens with a draw. ",
                " Dealer draws the opener. Somewhere, a wallet flinches. ",
                " New round boots up - dealer drops the first card on the table. ",
                " The dealer starts the countdown with a single draw. ",
                " Dealer draws the first card - now the round can officially misbehave. "
            ],

            "Dealer Hit Messages": [
                " Dealer hits - let's see what fate deals next. ",
                " One more card for the dealer. No fear, just confidence. ",
                " Dealer takes another - surely this can't go wrong. ",
                " Dealer hits. The deck is about to say something important. ",
                " Dealer asks for a card - fortune, don't embarrass me. ",
                " Another card, please. Let's spice this round up. ",
                " Dealer hits again - because playing it safe is overrated. ",
                " Dealer reaches for the deck. One more step toward glory… or doom. ",
                " Hit me - dealer style. ",
                " Dealer hits. The table holds its breath. ",
                " Dealer goes fishing for a better number. ",
                " Dealer: 'I can fix this.' *draws another card* ",
                " Dealer hits - trusting cardboard and bad decisions. ",
                " Dealer wants another card. The suspense is free, the stress is not. ",
                " Dealer hits. If this works, it was skill. If not, it was the deck. "
            ],

            "Dealer Stands Messages": [
                " Dealer stands. No more cards - time to settle this. ",
                " Dealer stays. Let's see who's smiling after this hand. ",
                " Dealer stands - final answer. Showdown time. ",
                " That's enough for the dealer. Let's compare hands. ",
                " Dealer stands. The rest is just math and regret. ",
                " Dealer locks it in - now we find out who guessed right. ",
                " Dealer stands. No more flirting with disaster. ",
                " Dealer holds. Cards down, results up. ",
                " Dealer stands - let the chips fall where they may. ",
                " Dealer stays. Time to see who played it smart. ",
                " Dealer stands. The hand is set; the verdict is next. ",
                " Dealer stops here. Let's finish this round. "
            ],

            "Dealer Blackjack Messages": [
                " Dealer hits BLACKJACK. The house sends its regards. ",
                " Blackjack for the dealer - clean, cruel, and classic. ",
                " Dealer blackjack. Sometimes the deck just chooses violence. ",
                " Dealer reveals BLACKJACK - instant pressure on the table. ",
                " Dealer has BLACKJACK. That escalated quickly. ",
                " BLACKJACK! Dealer didn't even break a sweat. ",
                " Dealer blackjack - lucky? maybe. painful? definitely. ",
                " Dealer shows BLACKJACK. Round went from 'maybe” to 'nope.” ",
                " Dealer blackjack. The house always remembers your last bet. ",
                " BLACKJACK for the dealer - short round, sharp sting. ",
                " Dealer reveals a perfect 21 - BLACKJACK. ",
                " Dealer blackjack. The table just got a lot quieter. "
            ],

            "Dealer Busts Messages": [
                " Dealer busts with <points>. Greed is a harsh dealer. ",
                " Dealer pushed too far - <points>. That's a bust. ",
                " Dealer overcooked it: <points>. Oops. ",
                " Dealer busted at <points>. The house does, in fact, lose sometimes. ",
                " Too many hits - dealer explodes with <points>. ",
                " Dealer drew one card too many and landed on <points>. ",
                " Dealer busts with <points>. The deck finally fights back. ",
                " Dealer went past 21 - <points>. That's what we call 'unfortunate.” ",
                " Dealer busts at <points>. Suddenly, the table feels lucky. ",
                " Dealer's confidence just turned into <points>. Bust. ",
                " Dealer tried to be brave - ended up at <points>. ",
                " Dealer busted with <points>. Turns out risk is universal. ",
                " Dealer hits <points> and immediately regrets it. ",
                " Dealer busted at <points>. The house takes an L this round. ",
                " Dealer went overboard: <points>. Bust, plain and simple. "
            ],

            "Hand Reaction Messages": [
                " Will <points> be enough for <t> this round? ",
                " <t> sits on <points> - bold choice or bad idea? ",
                " <points> for <t>. The table is watching. ",
                " <t> has <points>. Now we wait for the dealer's answer. "
            ],

            "Win Messages": [
                " <t> wins the round with <points>. ",
                " Victory for <t> - <points> takes it. ",
                " <t> takes the hand: <points>. ",
                " <t> comes out on top with <points>. "
            ],

            "Push Messages": [
                " Push for <t> at <points>. Bet returned - except the DD bet. ",
                " It's a push: <t> with <points>. Stake back (DD bet excluded). ",
                " Standoff! <t> pushes with <points>. Refund applies, not the DD bet. ",
                " No winner - push at <points> for <t>. Bet back, DD bet stays. "
            ],

            "Bust Messages": [
                " <t> busts with <points>. That one hurt. ",
                " Bust! <t> went over with <points>. ",
                " <t> pushed too far - <points>. Busted. ",
                " Unlucky round: <t> busts at <points>. "
            ],

            "Lost Messages": [
                " <t> loses this hand with <points>. ",
                " Not enough - <t> falls short with <points>. ",
                " <t> doesn't take it this time: <points>. ",
                " House takes this one - <t> ends on <points>. "
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
