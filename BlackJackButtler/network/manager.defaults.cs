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
                { "Text": "/p #{Dividor}", "Delay": 0.8 },
                { "Text": "/p #{Dealer Draw Messages} <se.1>", "Delay": 0.8 },
                { "Text": "/beam motion", "Delay": 0.8 },
                { "Text": "/bstance motion", "Delay": 4.5 },
                { "Text": "/dice party 13", "Delay": 0.8 }
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
                { "Text": "/p #{Dividor}", "Delay": 0.5 },
                { "Text": "/p #{Player State Messages HSDS} <se.7>", "Delay": 0.5 }
            ],
            "StateHSD":  [
                { "Text": "/p #{Dividor}", "Delay": 0.5 },
                { "Text": "/p #{Player State Messages HSD} <se.7>", "Delay": 0.5 }
            ],
            "StateHS":   [
                { "Text": "/p #{Dividor}", "Delay": 0.5 },
                { "Text": "/p #{Player State Messages HS} <se.7>", "Delay": 0.5 }
            ],
            "HandStateHSDS": [
                { "Text": "/p #{Dividor}", "Delay": 0.5 },
                { "Text": "/p #{Player Hand State Messages HSDS} <se.7>", "Delay": 0.5 }
            ],
            "HandStateHSD": [
                { "Text": "/p #{Dividor}", "Delay": 0.5 },
                { "Text": "/p #{Player Hand State Messages HSD} <se.7>", "Delay": 0.5 }
            ],
            "HandStateHS": [
                { "Text": "/p #{Dividor}", "Delay": 0.5 },
                { "Text": "/p #{Player Hand State Messages HS} <se.7>", "Delay": 0.5 }
            ],
            "PlayerDDForcedStand": [
                { "Text": "/p #{Player DD Forced Stand Messages}", "Delay": 1.2 },
                { "Text": "/p #{Dividor}", "Delay": 0.8 }
            ],
            "ResultSmall": [
                { "Text": "/p #{Match Results}", "Delay": 0.9 }
            ],
            "BankTell": [
                { "Text": "/p #{Bank Tell Messages}", "Delay": 0.5 }
            ]
        },
        "Messages": {
            "Dividor": [
                "========================="
            ],

            "Match Results": [
                " Results: <results> ",
                " This round is over. The battlefield looks like this: <results> ",
                " After this round, we have the results: <results> ",
                " Round complete. Here's how everyone fared: <results> ",
                " The dust settles - time to count the chips: <results> ",
                " Cards are down, wallets are open. Results: <results> ",
                " That's a wrap on this round. Final standings: <results> ",
                " Another round in the books. Here's the damage: <results> ",
                " Alright, let's see who's celebrating and who's crying: <results> ",
                " Round concluded. Winners and losers revealed: <results> ",
                " The table speaks its verdict: <results> ",
                " Fortune has made her decision this round: <results> ",
                " Time to face the music. Round results: <results> ",
                " Cards never lie - here's what happened: <results> ",
                " The house has spoken. Results: <results> ",
                " Round's over. Let's see who beat the odds: <results> ",
                " Another spin of fate complete. Results: <results> ",
                " Chips counted, tears shed. Here's the breakdown: <results> ",
                " The verdict is in for this round: <results> ",
                " Round finished. Time for the truth: <results> "
            ],

            "Player Deal Hand": [
                " Here are your cards now, <t>. ",
                " Let me deal your hand, <t>. ",
                " Two cards for you, <t>. ",
                " What do the cards say for <t>? ",
                " <t> gets 2 cards. ",
                " Fate's got something for you, <t>. Let's see what it is. ",
                " Cards incoming, <t> - fingers crossed~. ",
                " Your turn to find out, <t>. Two cards, one destiny. ",
                " The deck has spoken for <t>. Here you go. ",
                " Alright <t>, let's see what we're working with. "
            ],

            "Player State Messages HSDS": [
                " <t> - You have splittable <points> with ${playerCards}. Dealer has ${dealerpoints}. Do you want to [Hit], [Stand], [Double Down] or [Split]? "
            ],

            "Player State Messages HSD": [
                " <t> - You have <points> with ${playerCards}. Dealer has ${dealerpoints}. Do you want to [Hit], [Stand] or [Double Down]? "
            ],

            "Player State Messages HS": [
                " <t> - You have <points> with ${playerCards}. Dealer has ${dealerpoints}. Do you want to [Hit] or [Stand]? "
            ],

            "Player Hand State Messages HSDS": [
                " <t> - Hand <handnumber> of <totalhands>: You have splittable <points> with ${playerCards}. Dealer has ${dealerpoints}. Do you want to [Hit], [Stand], [Double Down] or [Split]? "
            ],

            "Player Hand State Messages HSD": [
                " <t> - Hand <handnumber> of <totalhands>: You have <points> with ${playerCards}. Dealer has ${dealerpoints}. Do you want to [Hit], [Stand] or [Double Down]? "
            ],

            "Player Hand State Messages HS": [
                " <t> - Hand <handnumber> of <totalhands>: You have <points> with ${playerCards}. Dealer has ${dealerpoints}. Do you want to [Hit] or [Stand]? "
            ],

            "Player DD Forced Stand Messages": [
                " Now you have <points> with ${playerCards}. Since it was a Double Down, this hand is now locked. ",
                " <t>'s hand is sealed at <points>. No more cards, no more prayers. ",
                " Double Down done - <t> sits at <points> whether they like it or not. ",
                " That's it, <t>. <points> is your final answer after the DD. ",
                " <t> locked in at <points>. The Double Down gods have decided. ",
                " Hand frozen at <points> for <t>. Double Down means no turning back~. "
            ],

            "Player Draw Messages": [
                " <t> want another card? Then <t> will get another card~. ",
                " <t> want a card - here it is. ",
                " <t> decides to hit. ",
                " And with that, <t> gets another card. ",
                " I'll draw another card for <t>. ",
                " One more for <t> - let's see if the deck is feeling generous. ",
                " <t> goes for another card. Brave or reckless? We'll find out. ",
                " Hit it is! Another card slides across the table to <t>. ",
                " <t> wants more. The deck obliges. ",
                " Another card for <t>~. May luck be on your side. "
            ],

            "Player Stand Messages": [
                " <t> decides to keep the given hand. Good Luck. ",
                " This hand stands now on <points> for <t>. ",
                " <t> holds at <points>. Confidence or acceptance? Either way, it's done. ",
                " Standing at <points>. <t> trusts the hand and hopes for the best. ",
                " <t> waves off the next card. <points> it is. ",
                " No more cards for <t>. Locked in at <points>. Let's see how it plays out. ",
                " <t> stands firm at <points>. The rest is up to the dealer now. "
            ],

            "Player DD Messages": [
                " <t> want to play a risky game? - DOUBLE DOWN! - Take this card and live with the consequences~. ",
                " Double Down? Did you say DOUBLE DOWN, <t>? - Well, you want it, you get it! ",
                " Double bet, double chance. Let's see, what fortuna wanna do with <t>'s hand now. ",
                " DD! - Let this card speak! ",
                " <t> wants another card. The last card for <t>'s hand. - Rolling drumsssss~. ",
                " All or nothing for <t>! DOUBLE DOWN - one card to seal the fate~. ",
                " <t> goes big! Double Down - this next card better be worth it. ",
                " The stakes just doubled for <t>. One card left. Make it count! ",
                " <t> slams the table - DOUBLE DOWN! Here comes the moment of truth. ",
                " Feeling bold, <t>? Double Down accepted. Let's see that final card~. "
            ],

            "Player DD Messages Stand": [
                " <t> drew the DD card. Hand is now locked at <points>. ",
                " Double Down complete. <t> stands automatically at <points>. ",
                " And that's a wrap. <t>'s DD hand locks at <points>. ",
                " Final count after Double Down: <t> stands at <points>. No takebacks. ",
                " DD card drawn. <t> is stuck with <points> now - for better or worse. ",
                " <t>'s Double Down settles at <points>. The bet is sealed. ",
                " One card, big bet, <points> result. <t>'s DD hand is done. "
            ],

            "Player Split Messages": [
                " <t> splits the hand. Okay let's see~. ",
                " These two cards are getting divided into 2 hands. What will <t> do with them? ",
                " <t> wants to split. Let's gooo~. ",
                " <t> sees double and wants to play both sides. Split it is! ",
                " Split! <t> turns one hand into two. Let's see if that pays off~. ",
                " <t> breaks the pair apart. Two hands, two chances, double the drama. ",
                " A bold split from <t>! Now the real fun begins. ",
                " <t> splits the hand in two. More cards, more possibilities~. "
            ],

            "Player BlackJack Messages": [
                " Wohoo. <t> got a NATURAL BLACKJACK! Congrats! ",
                " FANTASTIC. A NATURAL BLACKJACK FOR <t>! Congrats! ",
                " 21 on the dot! NATURAL BLACKJACK for <t>! The deck loves you today! ",
                " BLACKJACK! <t> just hit the jackpot - a perfect 21 right out of the gate! ",
                " That's the dream, <t>! NATURAL BLACKJACK - can't get better than this! ",
                " The cards aligned perfectly for <t> - NATURAL BLACKJACK! Incredible! ",
                " Straight to 21! <t> pulls a NATURAL BLACKJACK like it's nothing! "
            ],

            "Player BlackJack Messages Shout": [
                "Wohoo. <t> got a natural blackjack. Congrats to <t>!"
            ],

            "Player Dirty BlackJack Messages": [
                " Wohoo. <t> got a blackjack. Fantastic! ",
                " Unbelievable. A BLACKJACK FOR <t>! CONGRATS! ",
                " Look at that - <t> pieced together a blackjack! Well played! ",
                " 21! <t> found the magic number the hard way. Nicely done! ",
                " <t> hits exactly 21 - a blackjack built from the ground up! ",
                " Step by step to 21 - <t> earned that blackjack! ",
                " <t> reaches the promised land: 21! A hard-fought blackjack! "
            ],

            "Player Busts Messages": [
                " Oh no. <t> got busted with <points>. ",
                " That's bad luck. <t> busted with <points>. ",
                " Ouch! <t> busted at <points>. ",
                " Bust! <t> pushed it to <points> and paid the price. ",
                " Dealer smiles. <t> went over with <points>. ",
                " Too hot to handle - <t> burned out at <points>. ",
                " Unlucky! <t> cracked with <points>. ",
                " And that's a bust - <t> hits <points>. ",
                " Over 21 alert: <t> landed on <points>. ",
                " Greed got the best of <t> at <points>. ",
                " One card too far - <t> ended on <points>. ",
                " The cards said 'nope.' <t> busted with <points>. ",
                " Risky business - <t> busted at <points>. ",
                " <t> went full send… to <points>. Bust. ",
                " The dealer thanks you, <t>: <points> is a bust. ",
                " Close? Not really. <t> busted with <points>. ",
                " Math check failed: <t> reached <points>. ",
                " You hate to see it - <t> busted at <points>. ",
                " <t> hit <points> and instantly regretted it. ",
                " That last hit was spicy - <t> busted with <points>. ",
                " Bold move, <t>. <points> is still a bust. ",
                " Dealer: 'I'll allow it.' Rules: 'No.' <t> has <points>. ",
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
                " Dealer stays. Let's see who'll be smiling after this hand. ",
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
                " Dealer shows BLACKJACK. Round went from 'maybe' to 'nope.' ",
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
                " Dealer went past 21 - <points>. That's what we call 'unfortunate.' ",
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
                " <t> has <points>. Now we wait for the dealer's answer. ",
                " <t> locks in at <points>. Nervous yet? ",
                " <points> on the board for <t>. Let's hope the dealer stumbles. ",
                " Is <points> the winning number? <t> sure hopes so. ",
                " <t> bets it all on <points>. The table holds its breath. ",
                " Standing at <points>, <t> can only watch and wait now. "
            ],

            "Win Messages": [
                " <t> wins the round with <points>. ",
                " Victory for <t> - <points> takes it. ",
                " <t> takes the hand: <points>. ",
                " <t> comes out on top with <points>. ",
                " <t> walks away a winner with <points>. Well played! ",
                " That's a W for <t>! <points> beats the dealer. ",
                " <t> claims the win at <points>. The chips slide over~. ",
                " Winner winner! <t> takes it with <points>. ",
                " <t> outplayed the house this round - <points> seals the deal. "
            ],

            "Push Messages": [
                " Push for <t> at <points>. Full bet returned. ",
                " It's a push: <t> with <points>. All stakes back. ",
                " Standoff! <t> pushes with <points>. Complete refund. ",
                " No winner - push at <points> for <t>. Total bet returned. ",
                " Dead even at <points>. <t> gets the full bet back. ",
                " <t> and the dealer both land on <points>. Push - money returned. ",
                " A tie at <points> for <t>. Nobody wins, nobody loses. ",
                " <t> matches the dealer at <points>. Full refund incoming. ",
                " Push at <points>! <t> lives to bet another round with the same stack. "
            ],

            "Bust Messages": [
                " <t> busts with <points>. That one hurt. ",
                " Bust! <t> went over with <points>. ",
                " <t> pushed too far - <points>. Busted. ",
                " Unlucky round: <t> busts at <points>. ",
                " <t> went too far and hit <points>. That's a bust. ",
                " Busted at <points>. <t> won't be happy about this one. ",
                " <t> crashed and burned at <points>. Better luck next hand. ",
                " <points> for <t> - the cards were not kind this round. ",
                " Over and out: <t> busts with <points>. "
            ],

            "Lost Messages": [
                " <t> loses this hand with <points>. ",
                " Not enough - <t> falls short with <points>. ",
                " <t> doesn't take it this time: <points>. ",
                " House takes this one - <t> ends on <points>. ",
                " <t> falls to the dealer with <points>. Tough break. ",
                " The house takes it - <t> couldn't overcome with <points>. ",
                " <t> tried with <points>, but the dealer had the edge. ",
                " Loss for <t> at <points>. The dealer smiles. ",
                " <t> ends the round on <points> - not enough to beat the house. "
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
                g.Commands.AddRange(kvp.Value.Select(c => new PluginCommand { Text = c.Text ?? "", Delay = c.Delay }));
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
    }

    internal static DefaultsContainer? GetRawContainer() {
        try {
            return JsonConvert.DeserializeObject<DefaultsContainer>(RawJson);
        } catch (Exception) { return null; }
    }
}
