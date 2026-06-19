using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static void SetRuntimeContext(List<PlayerState> players, PlayerState dealer)
    {
        PlayerIdentityManager.Refresh(players, dealer);
        lock (_ctxLock)
        {
            _ctxPlayers = players;
            _ctxDealer = dealer;
        }
    }

    private static string ResolveRecipientNameForCard()
    {
        lock (_ctxLock)
        {
            if (!string.IsNullOrWhiteSpace(_forcedRecipientName))
                return _forcedRecipientName;
        }

        return _virtualTargetName;
    }

    public static void ApplyCardToCurrentTarget(int cardValue, List<PlayerState> players, PlayerState dealer)
    {
        if (cardValue <= 0) return;
        var deckCard = DeckManager.PullCard(cardValue);
        deckCard.DrawnAt = DateTime.UtcNow;
        var recipientName = ResolveRecipientNameForCard();

        PlayerState? target = null;

        if (!string.IsNullOrWhiteSpace(recipientName) &&
            dealer.Name.Equals(recipientName, StringComparison.OrdinalIgnoreCase))
        {
            target = dealer;
        }
        else if (!string.IsNullOrWhiteSpace(recipientName))
        {
            target = PlayerIdentityManager.Find(players, null, recipientName);
        }

        target ??= players.FirstOrDefault(p => p.IsCurrentTurn) ?? dealer;

        if (target.Hands.Count == 0)
            target.Hands.Add(new HandState(target.CurrentBet));

        if (target.CurrentHandIndex < 0 || target.CurrentHandIndex >= target.Hands.Count)
            target.CurrentHandIndex = 0;

        var hand = target.Hands[target.CurrentHandIndex];

        hand.Cards.Add(deckCard);

        var (min, max) = target.CalculatePoints(target.CurrentHandIndex);
        hand.IsBust = min > 21 && (!max.HasValue || max.Value > 21);

        if (hand.Cards.Count == 2)
        {
            hand.IsNaturalBlackJack =
                (max.HasValue && max.Value == 21) ||
                (!max.HasValue && min == 21);

            if (hand.IsNaturalBlackJack)
                hand.IsStand = true;
        }
        bool isDealer = ReferenceEquals(target, dealer) ||
                       target.Name.Equals(dealer.Name, StringComparison.OrdinalIgnoreCase);

        if (isDealer)
        {
            int dealerScore = (max.HasValue && max.Value <= 21) ? max.Value : min;
            VariableManager.SetVariable("dealerpoints", dealerScore.ToString());
            VariableManager.SetVariable("dealerHand", dealer.GetCardsString(0));
        }
    }

    public static bool TryApplyCardToCurrentTargetFromRuntime(int cardValue)
    {
        lock (_ctxLock)
        {
            if (_ctxDealer == null || _ctxPlayers == null) return false;
            ApplyCardToCurrentTarget(cardValue, _ctxPlayers, _ctxDealer);
            return true;
        }
    }

    public static bool TryGetBestScoreForCurrentTarget(out int score)
    {
        score = 0;

        lock (_ctxLock)
        {
            if (_ctxDealer == null || _ctxPlayers == null) return false;

            var name = GetCurrentTargetName();

            PlayerState? target =
                (!string.IsNullOrWhiteSpace(name) && _ctxDealer.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ? _ctxDealer
                    : _ctxPlayers.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            target ??= _ctxPlayers.FirstOrDefault(p => p.IsCurrentTurn) ?? _ctxDealer;

            if (target.Hands.Count == 0) return false;

            if (target.CurrentHandIndex < 0 || target.CurrentHandIndex >= target.Hands.Count)
                target.CurrentHandIndex = 0;

            var (min, max) = target.CalculatePoints(target.CurrentHandIndex);
            score = (max.HasValue && max.Value <= 21) ? max.Value : min;
            return true;
        }
    }

    public static int MapDice13ToCardValue(int rolled)
    {
        return rolled;
    }

    private static bool IsPlayerDone(PlayerState p)
    {
        if (!p.IsActivePlayer) return true;
        if (!p.HasInitialHandDealt) return false;
        if (p.Hands.Count == 0) return false;

        return p.Hands.All(IsHandDone);
    }

    public static void HandlePostCardEvents(Configuration cfg, List<PlayerState> players, PlayerState dealer)
    {

        var targetName = GetCurrentTargetName();
        PlayerState? target =
        (!string.IsNullOrWhiteSpace(targetName) && dealer.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
        ? dealer
        : players.FirstOrDefault(p => p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

        target ??= players.FirstOrDefault(p => p.IsCurrentTurn) ?? dealer;

        if (target.Hands.Count == 0) return;

        if (target.CurrentHandIndex < 0 || target.CurrentHandIndex >= target.Hands.Count)
        target.CurrentHandIndex = 0;

    }

    private static async Task ExecutePlayerAction(PlayerState p, string actionName, Configuration cfg, List<PlayerState> players, Func<Task> logic)
    {
        Chat.GameLog.PushSnapshot(players, _ctxDealer!, CurrentPhase, $"{actionName}:{p.Name}");
        CommandExecutor.SetPreActionSnapshot(Chat.GameLog.CurrentIndex);

        long cost = 0;
        if (actionName == "Initial") cost = p.CurrentBet;
        if (actionName == "DD")      cost = p.CurrentBet;
        if (actionName == "Split")   cost = p.CurrentBet;

        p.Bank -= cost;

        await logic();
    }

    internal static int EvaluateBJTie(HandState hand, PlayerState dealer, BlackjackTieRule rule)
    {
        bool playerNat = hand.IsNaturalBlackJack;
        bool dealerNat = dealer.Hands.Count > 0 && dealer.Hands[0].IsNaturalBlackJack;

        return rule switch
        {
            BlackjackTieRule.PlayerNatBJWins => playerNat ? 1 : 0,
            BlackjackTieRule.DealerNatBJWins => dealerNat ? -1 : 0,
            BlackjackTieRule.NatBJBeatsDirty => playerNat == dealerNat ? 0 : (playerNat ? 1 : -1),
            _ => 0,
        };
    }

    public static async Task EvaluateFinalResults(List<PlayerState> players, PlayerState dealer, Configuration cfg)
    {
        if (Interlocked.CompareExchange(ref _payoutGuard, 1, 0) != 0)
        {
            Plugin.Instance.GetMainWindow().AddDebugLog("[Engine] EvaluateFinalResults skipped (already running)");
            return;
        }

        BeginPayoutOutput();

        int dealerScore = dealer.GetBestScore(0);
        bool dealerBust = dealer.Hands.Count > 0 && dealer.Hands[0].IsBust;

        var payoutTraces = new Dictionary<string, PayoutTrace>();
        PayoutTrace EnsureTrace(PlayerState pl)
        {
            if (!payoutTraces.TryGetValue(pl.DisplayName, out var t))
            {
                t = new PayoutTrace
                {
                    ReceiverDisplayName = pl.DisplayName,
                    ReceiverBankBeforePayout = pl.Bank,
                    ReceiverBankAfterPayout = pl.Bank,
                };
                payoutTraces[pl.DisplayName] = t;
            }
            return t;
        }
        void RecordLeg(PlayerState pl, long bankBefore, long bankAfter, long amount)
        {
            if (amount == 0) return;
            var t = EnsureTrace(pl);
            t.ReceiverBankAfterPayout = bankAfter;
            t.Legs.Add(new PayoutLeg
            {
                PayerDisplayName = pl.DisplayName,
                PayerBankBefore = bankBefore,
                PayerBankAfter = bankAfter,
                Amount = amount,
            });
        }

        if (cfg.SmallResult)
        {
            var winList = new List<string>();
            var pushList = new List<string>();
            var lossList = new List<string>();
            var bustList = new List<string>();
            var activeList = players.Where(x => x.IsActivePlayer && !x.IsOnHold).ToList();

            foreach (var p in players.Where(x => x.IsActivePlayer && !x.IsOnHold))
            {
                p.IsCurrentTurn = false;
                p.LastRoundResult = 0;
                string shortName = p.GetShortName(activeList);

                foreach (var hand in p.Hands)
                {
                    p.CurrentHandIndex = p.Hands.IndexOf(hand);
                    int pScore = p.GetBestScore(p.CurrentHandIndex);

                    if (hand.IsBust)
                    {
                        bustList.Add(shortName);
                        hand.RoundResult = -hand.Bet;
                        p.LastRoundResult -= hand.Bet;
                    }
                    else if (pScore == dealerScore && pScore == 21 && EvaluateBJTie(hand, dealer, cfg.BlackjackTieRule) < 0)
                    {
                        lossList.Add(shortName);
                        hand.RoundResult = -hand.Bet;
                        p.LastRoundResult -= hand.Bet;
                    }
                    else if (dealerBust || pScore > dealerScore || (hand.IsCharlie && cfg.CharlieInstantWin) || (pScore == 21 && dealerScore == 21 && EvaluateBJTie(hand, dealer, cfg.BlackjackTieRule) > 0))
                    {
                        winList.Add(shortName);
                        float mult = cfg.MultiplierNormalWin;
                        if (hand.IsNaturalBlackJack) mult = cfg.MultiplierBlackjackWin;
                        else if (hand.IsCharlie) mult = cfg.MultiplierBlackjackWin;
                        else if (pScore == 21) mult = cfg.MultiplierDirtyBlackjackWin;

                        long winAmount = (long)(hand.Bet * mult);
                        hand.RoundResult = winAmount;
                        long preBank = p.Bank;
                        p.Bank += (hand.Bet + winAmount);
                        p.LastRoundResult += winAmount;
                        RecordLeg(p, preBank, p.Bank, p.Bank - preBank);
                    }
                    else if (pScore == dealerScore)
                    {
                        pushList.Add(shortName);
                        hand.RoundResult = 0;
                        long preBank = p.Bank;
                        p.Bank += hand.Bet;
                        RecordLeg(p, preBank, p.Bank, p.Bank - preBank);
                    }
                    else
                    {
                        lossList.Add(shortName);
                        hand.RoundResult = -hand.Bet;
                        p.LastRoundResult -= hand.Bet;
                    }
                }
            }

            VariableManager.SetVariable("winners", FormatResultCategory(winList.Distinct().ToList(), "Winner", "Winners"));
            VariableManager.SetVariable("pushed", FormatResultCategory(pushList.Distinct().ToList(), "Pushed", "Pushed"));
            VariableManager.SetVariable("loosers", FormatResultCategory(lossList.Distinct().ToList(), "Lost", "Lost"));
            VariableManager.SetVariable("busted", FormatResultCategory(bustList.Distinct().ToList(), "Busted", "Busted"));

            var variableSnapshot = VariableManager.SnapshotForUi();
            string GetV(string n) => variableSnapshot.FirstOrDefault(v => v.Name == n)?.Value ?? "";

            var defaultResults = ShortResultFormatter.Render(cfg, winList, pushList, lossList, bustList);
            var resultTemplate = string.IsNullOrWhiteSpace(cfg.ResultTemplate) ? "${results}" : cfg.ResultTemplate;
            var renderedResults = resultTemplate
                .Replace("${results}", defaultResults)
                .Replace("<results>", defaultResults)
                .Replace("${winners}", GetV("winners"))
                .Replace("${pushed}", GetV("pushed"))
                .Replace("${loosers}", GetV("loosers"))
                .Replace("${busted}", GetV("busted"));
            VariableManager.SetVariable("results", renderedResults);

            await CommandExecutor.ExecuteGroup("ResultSmall", dealer.DisplayName, cfg);
        }
        else
        {
            foreach (var p in players.Where(x => x.IsActivePlayer && !x.IsOnHold))
            {
                p.IsCurrentTurn = false;
                p.LastRoundResult = 0;

                foreach (var hand in p.Hands)
                {
                    p.CurrentHandIndex = p.Hands.IndexOf(hand);
                    int pScore = p.GetBestScore(p.CurrentHandIndex);

                    if (hand.IsBust)
                    {
                        hand.RoundResult = -hand.Bet;
                        p.LastRoundResult -= hand.Bet;
                        await CommandExecutor.ExecuteGroup("ResultPlayerBusted", p.DisplayName, cfg);
                    }
                    else if (pScore == dealerScore && pScore == 21 && EvaluateBJTie(hand, dealer, cfg.BlackjackTieRule) < 0)
                    {
                        hand.RoundResult = -hand.Bet;
                        p.LastRoundResult -= hand.Bet;
                        await CommandExecutor.ExecuteGroup("ResultPlayerLost", p.DisplayName, cfg);
                    }
                    else if (dealerBust || pScore > dealerScore || (hand.IsCharlie && cfg.CharlieInstantWin) || (pScore == 21 && dealerScore == 21 && EvaluateBJTie(hand, dealer, cfg.BlackjackTieRule) > 0))
                    {
                        float mult = cfg.MultiplierNormalWin;
                        if (hand.IsNaturalBlackJack) mult = cfg.MultiplierBlackjackWin;
                        else if (hand.IsCharlie) mult = cfg.MultiplierBlackjackWin;
                        else if (pScore == 21) mult = cfg.MultiplierDirtyBlackjackWin;

                        long winAmount = (long)(hand.Bet * mult);
                        hand.RoundResult = winAmount;
                        long preBank = p.Bank;
                        p.Bank += (hand.Bet + winAmount);
                        p.LastRoundResult += winAmount;
                        RecordLeg(p, preBank, p.Bank, p.Bank - preBank);
                        await CommandExecutor.ExecuteGroup("ResultPlayerWin", p.DisplayName, cfg);
                    }
                    else if (pScore == dealerScore)
                    {
                        hand.RoundResult = 0;
                        long preBank = p.Bank;
                        p.Bank += hand.Bet;
                        RecordLeg(p, preBank, p.Bank, p.Bank - preBank);
                        await CommandExecutor.ExecuteGroup("ResultPlayerPush", p.DisplayName, cfg);
                    }
                    else
                    {
                        hand.RoundResult = -hand.Bet;
                        p.LastRoundResult -= hand.Bet;
                        await CommandExecutor.ExecuteGroup("ResultPlayerLost", p.DisplayName, cfg);
                    }
                }
            }
        }

        SaveSessionIfNeeded(players);
        dealer.IsCurrentTurn = false;
        CompanionSyncManager.SendPlayerUpdate(cfg, dealer);
        CompanionSyncManager.SendPlayersUpdate(cfg, players.Where(x => x.IsActivePlayer));
        MarkPayoutOutputComplete();
    }

    private static string FormatResultCategory(List<string> names, string singular, string plural)
    {
        if (!names.Any()) return "";
        string label = names.Count == 1 ? singular : plural;
        return $"{label}: {string.Join(", ", names)}";
    }
}
