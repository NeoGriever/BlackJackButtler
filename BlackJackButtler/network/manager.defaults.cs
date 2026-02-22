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
                "\ue070 Results: <results> \ue070",
                "\ue070 This round is over. The battlefield looks like this: <results> \ue070",
                "\ue070 After this round, we have the results: <results> \ue070",
                "\ue070 Round complete. Here's how everyone fared: <results> \ue070",
                "\ue070 The dust settles - time to count the chips: <results> \ue070",
                "\ue070 Cards are down, wallets are open. Results: <results> \ue070",
                "\ue070 That's a wrap on this round. Final standings: <results> \ue070",
                "\ue070 Another round in the books. Here's the damage: <results> \ue070",
                "\ue070 Alright, let's see who's celebrating and who's crying: <results> \ue070",
                "\ue070 Round concluded. Winners and losers revealed: <results> \ue070",
                "\ue070 The table speaks its verdict: <results> \ue070",
                "\ue070 Fortune has made her decision this round: <results> \ue070",
                "\ue070 Time to face the music. Round results: <results> \ue070",
                "\ue070 Cards never lie - here's what happened: <results> \ue070",
                "\ue070 The house has spoken. Results: <results> \ue070",
                "\ue070 Round's over. Let's see who beat the odds: <results> \ue070",
                "\ue070 Another spin of fate complete. Results: <results> \ue070",
                "\ue070 Chips counted, tears shed. Here's the breakdown: <results> \ue070",
                "\ue070 The verdict is in for this round: <results> \ue070",
                "\ue070 Round finished. Time for the truth: <results> \ue070"
            ],

            "Player Deal Hand": [
                "\ue070 Here are your cards now, <t>. \ue070",
                "\ue070 Let me deal your hand, <t>. \ue070",
                "\ue070 Two cards for you, <t>. \ue070",
                "\ue070 What do the cards say for <t>? \ue070",
                "\ue070 <t> gets 2 cards. \ue070"
            ],

            "Player Hand Preview Messages": [
                "\ue070 <points> as a start, <t>. \ue070",
                "\ue070 First card lands at <points> for <t>. \ue070",
                "\ue070 <t> opens with <points> - interesting. \ue070",
                "\ue070 The cards begin at <points> for <t>. \ue070",
                "\ue070 And the first card says <points>. \ue070"
            ],

            "Player State Messages HSDS": [
                "\ue070 ${HandIndex}<t> - You have splittable <points> with ${playerCards}. Dealer has ${dealerpoints}. Do you want to [Hit], [Stand], [Double Down] or [Split]? \ue070"
            ],

            "Player State Messages HSD": [
                "\ue070 ${HandIndex}<t> - You have <points> with ${playerCards}. Dealer has ${dealerpoints}. Do you want to [Hit], [Stand] or [Double Down]? \ue070"
            ],

            "Player State Messages HS": [
                "\ue070 ${HandIndex}<t> - You have <points> with ${playerCards}. Dealer has ${dealerpoints}. Do you want to [Hit] or [Stand]? \ue070"
            ],

            "Player DD Forced Stand Messages": [
                "\ue070 ${HandIndex}Now you have <points> with ${playerCards}. Since it was a Double Down, this hand is now locked. \ue070"
            ],

            "Player Draw Messages": [
                "\ue070 ${HandIndex}<t> want another card? Then <t> will get another card~. \ue070",
                "\ue070 ${HandIndex}<t> want a card - here it is. \ue070",
                "\ue070 ${HandIndex}<t> decides to hit. \ue070",
                "\ue070 ${HandIndex}And with that, <t> gets another card. \ue070",
                "\ue070 ${HandIndex}I'll draw another card for <t>. \ue070"
            ],

            "Player Stand Messages": [
                "\ue070 ${HandIndex}<t> decides to keep the given hand. Good Luck. \ue070",
                "\ue070 ${HandIndex}This hand stands now on <points> for <t>. \ue070"
            ],

            "Player DD Messages": [
                "\ue070 ${HandIndex}<t> want to play a risky game? - DOUBLE DOWN! - Take this card and live with the consequences~. \ue070",
                "\ue070 ${HandIndex}Double Down? Did you say DOUBLE DOWN, <t>? - Well, you want it, you get it! \ue070",
                "\ue070 ${HandIndex}Double bet, double chance. Let's see, what fortuna wanna do with <t>'s hand now. \ue070",
                "\ue070 ${HandIndex}DD! - Let this card speak! \ue070",
                "\ue070 ${HandIndex}<t> wants another card. The last card for <t>'s hand. - Rolling drumsssss~. \ue070"
            ],

            "Player DD Messages Stand": [
                "\ue070 ${HandIndex}<t> drew the DD card. Hand is now locked at <points>. \ue070",
                "\ue070 ${HandIndex}Double Down complete. <t> stands automatically at <points>. \ue070"
            ],

            "Player Split Messages": [
                "\ue070 <t> splits the hand. Okay let's see~. \ue070",
                "\ue070 These two cards are getting divided into 2 hands. What will <t> do with them? \ue070",
                "\ue070 <t> wants to split. Let's gooo~. \ue070"
            ],

            "Player Split Draw Messages": [
                "\ue070 ${HandIndex}Now dealing the opening card for <t>'s next hand. \ue070",
                "\ue070 ${HandIndex}Next hand for <t> - let's see what we're working with. \ue070",
                "\ue070 ${HandIndex}Drawing the starting card for <t>'s split hand. \ue070",
                "\ue070 ${HandIndex}<t>'s next hand gets its second card. Let's go. \ue070",
                "\ue070 ${HandIndex}Time to set up <t>'s next split hand - here comes the card. \ue070"
            ],

            "Player BlackJack Messages": [
                "\ue070 ${HandIndex}Wohoo. <t> got a NATURAL BLACKJACK! Congrats! \ue070",
                "\ue070 ${HandIndex}FANTASTIC. A NATURAL BLACKJACK FOR <t>! Congrats! \ue070"
            ],

            "Player BlackJack Messages Shout": [
                "Wohoo. <t> got a natural blackjack. Congrats to <t>!"
            ],

            "Player Dirty BlackJack Messages": [
                "\ue070 ${HandIndex}Wohoo. <t> got a blackjack. Fantastic! \ue070",
                "\ue070 ${HandIndex}Unbelievable. A BLACKJACK FOR <t>! CONGRATS! \ue070"
            ],

            "Player Busts Messages": [
                "\ue070 ${HandIndex}Oh no. <t> got busted with <points>. \ue070",
                "\ue070 ${HandIndex}That's bad luck. <t> busted with <points>. \ue070",
                "\ue070 ${HandIndex}Ouch! <t> busted at <points>. \ue070",
                "\ue070 ${HandIndex}Bust! <t> pushed it to <points> and paid the price. \ue070",
                "\ue070 ${HandIndex}Dealer smiles. <t> went over with <points>. \ue070",
                "\ue070 ${HandIndex}Too hot to handle - <t> burned out at <points>. \ue070",
                "\ue070 ${HandIndex}Unlucky! <t> cracked with <points>. \ue070",
                "\ue070 ${HandIndex}And that's a bust - <t> hits <points>. \ue070",
                "\ue070 ${HandIndex}Over 21 alert: <t> landed on <points>. \ue070",
                "\ue070 ${HandIndex}Greed got the best of <t> at <points>. \ue070",
                "\ue070 ${HandIndex}One card too far - <t> ended on <points>. \ue070",
                "\ue070 ${HandIndex}The cards said 'nope.' <t> busted with <points>. \ue070",
                "\ue070 ${HandIndex}Risky business - <t> busted at <points>. \ue070",
                "\ue070 ${HandIndex}<t> went full send… to <points>. Bust. \ue070",
                "\ue070 ${HandIndex}The dealer thanks you, <t>: <points> is a bust. \ue070",
                "\ue070 ${HandIndex}Close? Not really. <t> busted with <points>. \ue070",
                "\ue070 ${HandIndex}Math check failed: <t> reached <points>. \ue070",
                "\ue070 ${HandIndex}You hate to see it - <t> busted at <points>. \ue070",
                "\ue070 ${HandIndex}<t> hit <points> and instantly regretted it. \ue070",
                "\ue070 ${HandIndex}That last hit was spicy - <t> busted with <points>. \ue070",
                "\ue070 ${HandIndex}Bold move, <t>. <points> is still a bust. \ue070",
                "\ue070 ${HandIndex}Dealer: 'I'll allow it.' Rules: 'No.' <t> has <points>. \ue070",
                "\ue070 ${HandIndex}<t> chased 21 and caught <points>. Bust. \ue070",
                "\ue070 ${HandIndex}Congratulations, <t> - you found <points> the hard way. \ue070",
                "\ue070 ${HandIndex}House wins this round: <t> busted with <points>. \ue070",
                "\ue070 ${HandIndex}Fortune favors the bold… not <t> at <points>. \ue070",
                "\ue070 ${HandIndex}<t> tried to outsmart the deck and got <points>. \ue070",
                "\ue070 ${HandIndex}Too many hits, not enough sense - <t> busted at <points>. \ue070",
                "\ue070 ${HandIndex}<t> went overboard with <points>. \ue070",
                "\ue070 ${HandIndex}The deck giveth, the deck busteth: <t> at <points>. \ue070",
                "\ue070 ${HandIndex}<t> zigged when they should've stayed - <points>. \ue070",
                "\ue070 ${HandIndex}<t> just invented a new number: <points> (aka bust). \ue070"
            ],

            "Dealer Draw Messages": [
                "\ue070 New round on the table - dealer starts off with a card. \ue070",
                "\ue070 Alright, fresh hand incoming. Dealer draws the opener. \ue070",
                "\ue070 Let's kick this round off - dealer reveals the first card. \ue070",
                "\ue070 The house makes the first move. Dealer draws. \ue070",
                "\ue070 Place your bets and brace yourself - dealer flips the opening card. \ue070",
                "\ue070 And we're live. Dealer draws to set the tone for this round. \ue070",
                "\ue070 Round start: the dealer breaks the silence with the first card. \ue070",
                "\ue070 Cards up - dealer opens the round with a draw. \ue070",
                "\ue070 Here we go again. Dealer draws the first card and the table wakes up. \ue070",
                "\ue070 The deck speaks first - dealer draws to begin the round. \ue070",
                "\ue070 New round, same nerves. Dealer reveals the opening card. \ue070",
                "\ue070 Let the drama begin - dealer pulls the first card. \ue070",
                "\ue070 The table is set and the dealer starts the story with one card. \ue070",
                "\ue070 First card down - dealer opens the round with a draw. \ue070",
                "\ue070 Starting whistle: dealer draws the opener and we're in. \ue070",
                "\ue070 Another round begins. Dealer draws and the tension returns. \ue070",
                "\ue070 Eyes on the felt - dealer flips the first card to start off. \ue070",
                "\ue070 No turning back now. Dealer draws the opening card. \ue070",
                "\ue070 Fresh round, fresh fate - dealer reveals what we're up against. \ue070",
                "\ue070 The house sets the pace - dealer draws the first card. \ue070",
                "\ue070 Let's see what kind of round this will be - dealer opens with a draw. \ue070",
                "\ue070 Dealer draws the opener. Somewhere, a wallet flinches. \ue070",
                "\ue070 New round boots up - dealer drops the first card on the table. \ue070",
                "\ue070 The dealer starts the countdown with a single draw. \ue070",
                "\ue070 Dealer draws the first card - now the round can officially misbehave. \ue070"
            ],

            "Dealer First Card Messages": [
                "\ue070 My opening card shows ${dealerpoints}. \ue070",
                "\ue070 Just ${dealerpoints} for me right now - let's see how this goes. \ue070",
                "\ue070 ${dealerpoints} on my side. Plan accordingly. \ue070",
                "\ue070 First card gives me ${dealerpoints}. \ue070",
                "\ue070 The house starts with ${dealerpoints}. \ue070"
            ],

            "Dealer Hit Messages": [
                "\ue070 Dealer hits - let's see what fate deals next. \ue070",
                "\ue070 One more card for the dealer. No fear, just confidence. \ue070",
                "\ue070 Dealer takes another - surely this can't go wrong. \ue070",
                "\ue070 Dealer hits. The deck is about to say something important. \ue070",
                "\ue070 Dealer asks for a card - fortune, don't embarrass me. \ue070",
                "\ue070 Another card, please. Let's spice this round up. \ue070",
                "\ue070 Dealer hits again - because playing it safe is overrated. \ue070",
                "\ue070 Dealer reaches for the deck. One more step toward glory… or doom. \ue070",
                "\ue070 Hit me - dealer style. \ue070",
                "\ue070 Dealer hits. The table holds its breath. \ue070",
                "\ue070 Dealer goes fishing for a better number. \ue070",
                "\ue070 Dealer: 'I can fix this.' *draws another card* \ue070",
                "\ue070 Dealer hits - trusting cardboard and bad decisions. \ue070",
                "\ue070 Dealer wants another card. The suspense is free, the stress is not. \ue070",
                "\ue070 Dealer hits. If this works, it was skill. If not, it was the deck. \ue070"
            ],

            "Dealer Stands Messages": [
                "\ue070 Dealer stands. No more cards - time to settle this. \ue070",
                "\ue070 Dealer stays. Let's see who'll be smiling after this hand. \ue070",
                "\ue070 Dealer stands - final answer. Showdown time. \ue070",
                "\ue070 That's enough for the dealer. Let's compare hands. \ue070",
                "\ue070 Dealer stands. The rest is just math and regret. \ue070",
                "\ue070 Dealer locks it in - now we find out who guessed right. \ue070",
                "\ue070 Dealer stands. No more flirting with disaster. \ue070",
                "\ue070 Dealer holds. Cards down, results up. \ue070",
                "\ue070 Dealer stands - let the chips fall where they may. \ue070",
                "\ue070 Dealer stays. Time to see who played it smart. \ue070",
                "\ue070 Dealer stands. The hand is set; the verdict is next. \ue070",
                "\ue070 Dealer stops here. Let's finish this round. \ue070"
            ],

            "Dealer Blackjack Messages": [
                "\ue070 Dealer hits BLACKJACK. The house sends its regards. \ue070",
                "\ue070 Blackjack for the dealer - clean, cruel, and classic. \ue070",
                "\ue070 Dealer blackjack. Sometimes the deck just chooses violence. \ue070",
                "\ue070 Dealer reveals BLACKJACK - instant pressure on the table. \ue070",
                "\ue070 Dealer has BLACKJACK. That escalated quickly. \ue070",
                "\ue070 BLACKJACK! Dealer didn't even break a sweat. \ue070",
                "\ue070 Dealer blackjack - lucky? maybe. painful? definitely. \ue070",
                "\ue070 Dealer shows BLACKJACK. Round went from 'maybe' to 'nope.' \ue070",
                "\ue070 Dealer blackjack. The house always remembers your last bet. \ue070",
                "\ue070 BLACKJACK for the dealer - short round, sharp sting. \ue070",
                "\ue070 Dealer reveals a perfect 21 - BLACKJACK. \ue070",
                "\ue070 Dealer blackjack. The table just got a lot quieter. \ue070"
            ],

            "Dealer Busts Messages": [
                "\ue070 Dealer busts with <points>. Greed is a harsh dealer. \ue070",
                "\ue070 Dealer pushed too far - <points>. That's a bust. \ue070",
                "\ue070 Dealer overcooked it: <points>. Oops. \ue070",
                "\ue070 Dealer busted at <points>. The house does, in fact, lose sometimes. \ue070",
                "\ue070 Too many hits - dealer explodes with <points>. \ue070",
                "\ue070 Dealer drew one card too many and landed on <points>. \ue070",
                "\ue070 Dealer busts with <points>. The deck finally fights back. \ue070",
                "\ue070 Dealer went past 21 - <points>. That's what we call 'unfortunate.' \ue070",
                "\ue070 Dealer busts at <points>. Suddenly, the table feels lucky. \ue070",
                "\ue070 Dealer's confidence just turned into <points>. Bust. \ue070",
                "\ue070 Dealer tried to be brave - ended up at <points>. \ue070",
                "\ue070 Dealer busted with <points>. Turns out risk is universal. \ue070",
                "\ue070 Dealer hits <points> and immediately regrets it. \ue070",
                "\ue070 Dealer busted at <points>. The house takes an L this round. \ue070",
                "\ue070 Dealer went overboard: <points>. Bust, plain and simple. \ue070"
            ],

            "Hand Reaction Messages": [
                "\ue070 ${HandIndex}Will <points> be enough for <t> this round? \ue070",
                "\ue070 ${HandIndex}<t> sits on <points> - bold choice or bad idea? \ue070",
                "\ue070 ${HandIndex}<points> for <t>. The table is watching. \ue070",
                "\ue070 ${HandIndex}<t> has <points>. Now we wait for the dealer's answer. \ue070"
            ],

            "Win Messages": [
                "\ue070 ${HandIndex}<t> wins the round with <points>. \ue070",
                "\ue070 ${HandIndex}Victory for <t> - <points> takes it. \ue070",
                "\ue070 ${HandIndex}<t> takes the hand: <points>. \ue070",
                "\ue070 ${HandIndex}<t> comes out on top with <points>. \ue070"
            ],

            "Push Messages": [
                "\ue070 ${HandIndex}Push for <t> at <points>. Full bet returned. \ue070",
                "\ue070 ${HandIndex}It's a push: <t> with <points>. All stakes back. \ue070",
                "\ue070 ${HandIndex}Standoff! <t> pushes with <points>. Complete refund. \ue070",
                "\ue070 ${HandIndex}No winner - push at <points> for <t>. Total bet returned. \ue070"
            ],

            "Bust Messages": [
                "\ue070 ${HandIndex}<t> busts with <points>. That one hurt. \ue070",
                "\ue070 ${HandIndex}Bust! <t> went over with <points>. \ue070",
                "\ue070 ${HandIndex}<t> pushed too far - <points>. Busted. \ue070",
                "\ue070 ${HandIndex}Unlucky round: <t> busts at <points>. \ue070"
            ],

            "Lost Messages": [
                "\ue070 ${HandIndex}<t> loses this hand with <points>. \ue070",
                "\ue070 ${HandIndex}Not enough - <t> falls short with <points>. \ue070",
                "\ue070 ${HandIndex}<t> doesn't take it this time: <points>. \ue070",
                "\ue070 ${HandIndex}House takes this one - <t> ends on <points>. \ue070"
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
