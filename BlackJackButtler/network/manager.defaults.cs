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
                "^You receive ([\\d,]+) gil\\.$"
            ], "Action": "TradeGilIn" },
            { "Name": "Trade: Gil Out", "Patterns": [
                "^Du überreichst ([\\d.]+) Gil\\.$",
                "^You hand over ([\\d,]+) gil\\.$"
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
                { "Text": "/ft", "Delay": 0.5 },
                { "Text": "/p #{Dividor}", "Delay": 1.2 },
                { "Text": "/p #{Player Deal Hand}", "Delay": 1.2 },
                { "Text": "/beam motion", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 0.8 },
                { "Text": "/p #{Player Hand Preview Messages}", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 1.2 },
                { "Text": "/p Thats <points> for your hand.", "Delay": 1.9 },
                { "Text": "/p #{Dividor}", "Delay": 1.0 }
            ],
            "Hit": [
                { "Text": "/ft", "Delay": 0.5 },
                { "Text": "/p #{Player Draw Messages}", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 0.8 },
                { "Text": "/p #{Dividor}", "Delay": 1.4 }
            ],
            "Stand": [
                { "Text": "/ft", "Delay": 0.5 },
                { "Text": "/yes motion", "Delay": 0.8 },
                { "Text": "/p #{Player Stand Messages}", "Delay": 1.2 },
                { "Text": "/p #{Hand Reaction Messages}", "Delay": 1.0 },
                { "Text": "/p #{Dividor}", "Delay": 1.6 }
            ],
            "DD": [
                { "Text": "/ft", "Delay": 0.5 },
                { "Text": "/p #{Player DD Messages}", "Delay": 1.2 },
                { "Text": "/wow", "Delay": 2.0 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 1.3 },
                { "Text": "/p #{Player DD Messages Stand}", "Delay": 1.3 },
                { "Text": "/p #{Dividor}", "Delay": 0.8 }
            ],
            "Split": [
                { "Text": "/ft", "Delay": 0.5 },
                { "Text": "/p #{Player Split Messages}", "Delay": 0.8 },
                { "Text": "/beam motion", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 0.8 }
            ],
            "SplitDraw": [
                { "Text": "/p #{Player Split Draw Messages}", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 0.8 }
            ],
            "PlayerBJ": [
                { "Text": "/ft", "Delay": 0.5 },
                { "Text": "/p #{Player BlackJack Messages} <se.7>", "Delay": 1.3 },
                { "Text": "/wow motion", "Delay": 0.8 },
                { "Text": "/shout #{Player BlackJack Messages Shout}", "Delay": 3.4 }
            ],
            "PlayerDirtyBJ": [
                { "Text": "/ft", "Delay": 0.5 },
                { "Text": "/p #{Player Dirty BlackJack Messages} <se.7>", "Delay": 1.3 },
                { "Text": "/wow motion", "Delay": 3.4 }
            ],
            "PlayerBust": [
                { "Text": "/ft", "Delay": 0.5 },
                { "Text": "/p #{Player Busts Messages} <se.11>", "Delay": 0.8 },
                { "Text": "/upset", "Delay": 3.5 }
            ],
            "DealStart": [
                { "Text": "/p #{Dividor}", "Delay": 0.8 },
                { "Text": "/p #{Dealer Draw Messages} <se.1>", "Delay": 0.8 },
                { "Text": "/beam motion", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 1.8 },
                { "Text": "/p #{Dealer First Card Messages}", "Delay": 1.8 },
                { "Text": "/p #{Dividor}", "Delay": 1.0 }
            ],
            "DealHit": [
                { "Text": "/p #{Dividor}", "Delay": 0.8 },
                { "Text": "/p #{Dealer Hit Messages}", "Delay": 0.8 },
                { "Text": "/beam motion", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 0.8 },
                { "Text": "/p Now i have ${dealerpoints}.", "Delay": 0.5 }
            ],
            "DealStand": [
                { "Text": "/p #{Dealer Stands Messages}", "Delay": 1.5 }
            ],
            "DealerBJ": [
                { "Text": "/p #{Dealer Blackjack Messages} <se.7>", "Delay": 0.8 },
                { "Text": "/psych", "Delay": 2.0 }
            ],
            "DealerBust": [
                { "Text": "/p #{Dealer Busts Messages} <se.11>", "Delay": 0.8 },
                { "Text": "/disappointed", "Delay": 3.5 }
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
                { "Text": "/ft", "Delay": 0.5, "Enabled": true },
                { "Text": "/p #{Dividor}", "Delay": 1.0, "Enabled": false },
                { "Text": "/p #{Player State Messages HSDS} <se.7>", "Delay": 0.5 }
            ],
            "StateHSD":  [
                { "Text": "/ft", "Delay": 0.5, "Enabled": true },
                { "Text": "/p #{Dividor}", "Delay": 1.0, "Enabled": false },
                { "Text": "/p #{Player State Messages HSD} <se.7>", "Delay": 0.5 }
            ],
            "StateHS":   [
                { "Text": "/ft", "Delay": 0.5, "Enabled": true },
                { "Text": "/p #{Dividor}", "Delay": 1.0, "Enabled": false },
                { "Text": "/p #{Player State Messages HS} <se.7>", "Delay": 0.5 }
            ],
            "PlayerDDForcedStand": [
                { "Text": "/ft", "Delay": 0.5 },
                { "Text": "/p #{Player DD Forced Stand Messages}", "Delay": 1.2 },
                { "Text": "/p #{Dividor}", "Delay": 1.0 }
            ],
            "ResultSmall": [
                { "Text": "/p #{Dividor}", "Delay": 0.9 },
                { "Text": "/p #{Match Results}", "Delay": 1.8 },
                { "Text": "/p #{Dividor} <se.15>", "Delay": 0.5 }
            ],
            "BankTell": [
                { "Text": "/ft", "Delay": 0.5 },
                { "Text": "/tell #{Bank Tell Messages}", "Delay": 0.5 }
            ]
        },
        "Messages": {
            "Dividor": [
                "========================="
            ],

            "Match Results": [
                " Results: <results> ",
                " This round is over. The battlefield looks like this: <results> ",
                " After this round, we have the results: <results> ",
                " Round complete. Here's how everyone fared: <results> ",
                " The dust settles - time to count the chips: <results> ",
                " Cards are down, wallets are open. Results: <results> ",
                " That's a wrap on this round. Final standings: <results> ",
                " Another round in the books. Here's the damage: <results> ",
                " Alright, let's see who's celebrating and who's crying: <results> ",
                " Round concluded. Winners and losers revealed: <results> ",
                " The table speaks its verdict: <results> ",
                " Fortune has made her decision this round: <results> ",
                " Time to face the music. Round results: <results> ",
                " Cards never lie - here's what happened: <results> ",
                " The house has spoken. Results: <results> ",
                " Round's over. Let's see who beat the odds: <results> ",
                " Another spin of fate complete. Results: <results> ",
                " Chips counted, tears shed. Here's the breakdown: <results> ",
                " The verdict is in for this round: <results> ",
                " Round finished. Time for the truth: <results> "
            ],

            "Player Deal Hand": [
                " Here are your cards now, <t>. ",
                " Let me deal your hand, <t>. ",
                " Two cards for you, <t>. ",
                " What do the cards say for <t>? ",
                " <t> gets 2 cards. "
            ],

            "Player Hand Preview Messages": [
                " <points> as a start, <t>. ",
                " First card lands at <points> for <t>. ",
                " <t> opens with <points> - interesting. ",
                " The cards begin at <points> for <t>. ",
                " And the first card says <points>. "
            ],

            "Player State Messages HSDS": [
                " ${HandIndex}<t> - You have splittable <points> with ${playerCards}. Dealer has ${dealerpoints}. Do you want to [Hit], [Stand], [Double Down] or [Split]? "
            ],

            "Player State Messages HSD": [
                " ${HandIndex}<t> - You have <points> with ${playerCards}. Dealer has ${dealerpoints}. Do you want to [Hit], [Stand] or [Double Down]? "
            ],

            "Player State Messages HS": [
                " ${HandIndex}<t> - You have <points> with ${playerCards}. Dealer has ${dealerpoints}. Do you want to [Hit] or [Stand]? "
            ],

            "Player DD Forced Stand Messages": [
                " ${HandIndex}Now you have <points> with ${playerCards}. Since it was a Double Down, this hand is now locked. "
            ],

            "Player Draw Messages": [
                " ${HandIndex}<t> want another card? Then <t> will get another card~. ",
                " ${HandIndex}<t> want a card - here it is. ",
                " ${HandIndex}<t> decides to hit. ",
                " ${HandIndex}And with that, <t> gets another card. ",
                " ${HandIndex}I'll draw another card for <t>. "
            ],

            "Player Stand Messages": [
                " ${HandIndex}<t> decides to keep the given hand. Good Luck. ",
                " ${HandIndex}This hand stands now on <points> for <t>. "
            ],

            "Player DD Messages": [
                " ${HandIndex}<t> want to play a risky game? - DOUBLE DOWN! - Take this card and live with the consequences~. ",
                " ${HandIndex}Double Down? Did you say DOUBLE DOWN, <t>? - Well, you want it, you get it! ",
                " ${HandIndex}Double bet, double chance. Let's see, what fortuna wanna do with <t>'s hand now. ",
                " ${HandIndex}DD! - Let this card speak! ",
                " ${HandIndex}<t> wants another card. The last card for <t>'s hand. - Rolling drumsssss~. "
            ],

            "Player DD Messages Stand": [
                " ${HandIndex}<t> drew the DD card. Hand is now locked at <points>. ",
                " ${HandIndex}Double Down complete. <t> stands automatically at <points>. "
            ],

            "Player Split Messages": [
                " <t> splits the hand. Okay let's see~. ",
                " These two cards are getting divided into 2 hands. What will <t> do with them? ",
                " <t> wants to split. Let's gooo~. "
            ],

            "Player Split Draw Messages": [
                " ${HandIndex}Now dealing the opening card for <t>'s next hand. ",
                " ${HandIndex}Next hand for <t> - let's see what we're working with. ",
                " ${HandIndex}Drawing the starting card for <t>'s split hand. ",
                " ${HandIndex}<t>'s next hand gets its second card. Let's go. ",
                " ${HandIndex}Time to set up <t>'s next split hand - here comes the card. "
            ],

            "Player BlackJack Messages": [
                " ${HandIndex}Wohoo. <t> got a NATURAL BLACKJACK! Congrats! ",
                " ${HandIndex}FANTASTIC. A NATURAL BLACKJACK FOR <t>! Congrats! "
            ],

            "Player BlackJack Messages Shout": [
                "Wohoo. <t> got a natural blackjack. Congrats to <t>!"
            ],

            "Player Dirty BlackJack Messages": [
                " ${HandIndex}Wohoo. <t> got a blackjack. Fantastic! ",
                " ${HandIndex}Unbelievable. A BLACKJACK FOR <t>! CONGRATS! "
            ],

            "Player Busts Messages": [
                " ${HandIndex}Oh no. <t> got busted with <points>. ",
                " ${HandIndex}That's bad luck. <t> busted with <points>. ",
                " ${HandIndex}Ouch! <t> busted at <points>. ",
                " ${HandIndex}Bust! <t> pushed it to <points> and paid the price. ",
                " ${HandIndex}Dealer smiles. <t> went over with <points>. ",
                " ${HandIndex}Too hot to handle - <t> burned out at <points>. ",
                " ${HandIndex}Unlucky! <t> cracked with <points>. ",
                " ${HandIndex}And that's a bust - <t> hits <points>. ",
                " ${HandIndex}Over 21 alert: <t> landed on <points>. ",
                " ${HandIndex}Greed got the best of <t> at <points>. ",
                " ${HandIndex}One card too far - <t> ended on <points>. ",
                " ${HandIndex}The cards said 'nope.' <t> busted with <points>. ",
                " ${HandIndex}Risky business - <t> busted at <points>. ",
                " ${HandIndex}<t> went full send… to <points>. Bust. ",
                " ${HandIndex}The dealer thanks you, <t>: <points> is a bust. ",
                " ${HandIndex}Close? Not really. <t> busted with <points>. ",
                " ${HandIndex}Math check failed: <t> reached <points>. ",
                " ${HandIndex}You hate to see it - <t> busted at <points>. ",
                " ${HandIndex}<t> hit <points> and instantly regretted it. ",
                " ${HandIndex}That last hit was spicy - <t> busted with <points>. ",
                " ${HandIndex}Bold move, <t>. <points> is still a bust. ",
                " ${HandIndex}Dealer: 'I'll allow it.' Rules: 'No.' <t> has <points>. ",
                " ${HandIndex}<t> chased 21 and caught <points>. Bust. ",
                " ${HandIndex}Congratulations, <t> - you found <points> the hard way. ",
                " ${HandIndex}House wins this round: <t> busted with <points>. ",
                " ${HandIndex}Fortune favors the bold… not <t> at <points>. ",
                " ${HandIndex}<t> tried to outsmart the deck and got <points>. ",
                " ${HandIndex}Too many hits, not enough sense - <t> busted at <points>. ",
                " ${HandIndex}<t> went overboard with <points>. ",
                " ${HandIndex}The deck giveth, the deck busteth: <t> at <points>. ",
                " ${HandIndex}<t> zigged when they should've stayed - <points>. ",
                " ${HandIndex}<t> just invented a new number: <points> (aka bust). "
            ],

            "Dealer Draw Messages": [
                " New round on the table - dealer starts off with a card. ",
                " Alright, fresh hand incoming. Dealer draws the opener. ",
                " Let's kick this round off - dealer reveals the first card. ",
                " The house makes the first move. Dealer draws. ",
                " Place your bets and brace yourself - dealer flips the opening card. ",
                " And we're live. Dealer draws to set the tone for this round. ",
                " Round start: the dealer breaks the silence with the first card. ",
                " Cards up - dealer opens the round with a draw. ",
                " Here we go again. Dealer draws the first card and the table wakes up. ",
                " The deck speaks first - dealer draws to begin the round. ",
                " New round, same nerves. Dealer reveals the opening card. ",
                " Let the drama begin - dealer pulls the first card. ",
                " The table is set and the dealer starts the story with one card. ",
                " First card down - dealer opens the round with a draw. ",
                " Starting whistle: dealer draws the opener and we're in. ",
                " Another round begins. Dealer draws and the tension returns. ",
                " Eyes on the felt - dealer flips the first card to start off. ",
                " No turning back now. Dealer draws the opening card. ",
                " Fresh round, fresh fate - dealer reveals what we're up against. ",
                " The house sets the pace - dealer draws the first card. ",
                " Let's see what kind of round this will be - dealer opens with a draw. ",
                " Dealer draws the opener. Somewhere, a wallet flinches. ",
                " New round boots up - dealer drops the first card on the table. ",
                " The dealer starts the countdown with a single draw. ",
                " Dealer draws the first card - now the round can officially misbehave. "
            ],

            "Dealer First Card Messages": [
                " My opening card shows ${dealerpoints}. ",
                " Just ${dealerpoints} for me right now - let's see how this goes. ",
                " ${dealerpoints} on my side. Plan accordingly. ",
                " First card gives me ${dealerpoints}. ",
                " The house starts with ${dealerpoints}. "
            ],

            "Dealer Hit Messages": [
                " Dealer hits - let's see what fate deals next. ",
                " One more card for the dealer. No fear, just confidence. ",
                " Dealer takes another - surely this can't go wrong. ",
                " Dealer hits. The deck is about to say something important. ",
                " Dealer asks for a card - fortune, don't embarrass me. ",
                " Another card, please. Let's spice this round up. ",
                " Dealer hits again - because playing it safe is overrated. ",
                " Dealer reaches for the deck. One more step toward glory… or doom. ",
                " Hit me - dealer style. ",
                " Dealer hits. The table holds its breath. ",
                " Dealer goes fishing for a better number. ",
                " Dealer: 'I can fix this.' *draws another card* ",
                " Dealer hits - trusting cardboard and bad decisions. ",
                " Dealer wants another card. The suspense is free, the stress is not. ",
                " Dealer hits. If this works, it was skill. If not, it was the deck. "
            ],

            "Dealer Stands Messages": [
                " Dealer stands. No more cards - time to settle this. ",
                " Dealer stays. Let's see who'll be smiling after this hand. ",
                " Dealer stands - final answer. Showdown time. ",
                " That's enough for the dealer. Let's compare hands. ",
                " Dealer stands. The rest is just math and regret. ",
                " Dealer locks it in - now we find out who guessed right. ",
                " Dealer stands. No more flirting with disaster. ",
                " Dealer holds. Cards down, results up. ",
                " Dealer stands - let the chips fall where they may. ",
                " Dealer stays. Time to see who played it smart. ",
                " Dealer stands. The hand is set; the verdict is next. ",
                " Dealer stops here. Let's finish this round. "
            ],

            "Dealer Blackjack Messages": [
                " Dealer hits BLACKJACK. The house sends its regards. ",
                " Blackjack for the dealer - clean, cruel, and classic. ",
                " Dealer blackjack. Sometimes the deck just chooses violence. ",
                " Dealer reveals BLACKJACK - instant pressure on the table. ",
                " Dealer has BLACKJACK. That escalated quickly. ",
                " BLACKJACK! Dealer didn't even break a sweat. ",
                " Dealer blackjack - lucky? maybe. painful? definitely. ",
                " Dealer shows BLACKJACK. Round went from 'maybe' to 'nope.' ",
                " Dealer blackjack. The house always remembers your last bet. ",
                " BLACKJACK for the dealer - short round, sharp sting. ",
                " Dealer reveals a perfect 21 - BLACKJACK. ",
                " Dealer blackjack. The table just got a lot quieter. "
            ],

            "Dealer Busts Messages": [
                " Dealer busts with <points>. Greed is a harsh dealer. ",
                " Dealer pushed too far - <points>. That's a bust. ",
                " Dealer overcooked it: <points>. Oops. ",
                " Dealer busted at <points>. The house does, in fact, lose sometimes. ",
                " Too many hits - dealer explodes with <points>. ",
                " Dealer drew one card too many and landed on <points>. ",
                " Dealer busts with <points>. The deck finally fights back. ",
                " Dealer went past 21 - <points>. That's what we call 'unfortunate.' ",
                " Dealer busts at <points>. Suddenly, the table feels lucky. ",
                " Dealer's confidence just turned into <points>. Bust. ",
                " Dealer tried to be brave - ended up at <points>. ",
                " Dealer busted with <points>. Turns out risk is universal. ",
                " Dealer hits <points> and immediately regrets it. ",
                " Dealer busted at <points>. The house takes an L this round. ",
                " Dealer went overboard: <points>. Bust, plain and simple. "
            ],

            "Hand Reaction Messages": [
                " ${HandIndex}Will <points> be enough for <t> this round? ",
                " ${HandIndex}<t> sits on <points> - bold choice or bad idea? ",
                " ${HandIndex}<points> for <t>. The table is watching. ",
                " ${HandIndex}<t> has <points>. Now we wait for the dealer's answer. "
            ],

            "Win Messages": [
                " ${HandIndex}<t> wins the round with <points>. ",
                " ${HandIndex}Victory for <t> - <points> takes it. ",
                " ${HandIndex}<t> takes the hand: <points>. ",
                " ${HandIndex}<t> comes out on top with <points>. "
            ],

            "Push Messages": [
                " ${HandIndex}Push for <t> at <points>. Full bet returned. ",
                " ${HandIndex}It's a push: <t> with <points>. All stakes back. ",
                " ${HandIndex}Standoff! <t> pushes with <points>. Complete refund. ",
                " ${HandIndex}No winner - push at <points> for <t>. Total bet returned. "
            ],

            "Bust Messages": [
                " ${HandIndex}<t> busts with <points>. That one hurt. ",
                " ${HandIndex}Bust! <t> went over with <points>. ",
                " ${HandIndex}<t> pushed too far - <points>. Busted. ",
                " ${HandIndex}Unlucky round: <t> busts at <points>. "
            ],

            "Lost Messages": [
                " ${HandIndex}<t> loses this hand with <points>. ",
                " ${HandIndex}Not enough - <t> falls short with <points>. ",
                " ${HandIndex}<t> doesn't take it this time: <points>. ",
                " ${HandIndex}House takes this one - <t> ends on <points>. "
            ],

            "Payment Reminder": [
                "Please pay ${missingGil} gil to continue your ${action}. Otherwise your choice gets revoked.",
                "I still need ${missingGil} gil from you to proceed with the ${action}.",
                "Wallet check! ${missingGil} gil are missing for your ${action}."
            ],

            "Bank Tell Messages": [
                "<t> - Bank: ${bankamount} | Bet: ${betamount}"
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
                g.Commands.AddRange(kvp.Value.Select(c => new PluginCommand { Text = c.Text ?? "", Delay = c.Delay, Enabled = c.Enabled }));
                return g;
            }).ToList();
        } catch (Exception) { return new(); }
    }

    internal class DefaultsContainer {
        public Dictionary<string, List<string>>? Messages { get; set; }
        public List<TradeRegexDto>? TradeRegex { get; set; }
        public Dictionary<string, List<CommandDto>>? Commands { get; set; }
    }
    internal class TradeRegexDto {
        public string? Name { get; set; }
        public List<string>? Patterns { get; set; }
        public string? Action { get; set; }
    }
    internal class CommandDto {
        public string? Text { get; set; }
        public float Delay { get; set; }
        public bool Enabled { get; set; } = true;
    }

    internal static DefaultsContainer? GetRawContainer() {
        try {
            return JsonConvert.DeserializeObject<DefaultsContainer>(RawJson);
        } catch (Exception) { return null; }
    }
}
