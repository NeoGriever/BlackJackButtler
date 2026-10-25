using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
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
            t.Legs.Add(
                new PayoutLeg
                {
                    PayerDisplayName = pl.DisplayName,
                    PayerBankBefore = bankBefore,
                    PayerBankAfter = bankAfter,
                    Amount = amount,
                }
            );
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
                        float mult = GetWinMultiplier(p, hand, pScore, cfg);

                        long winAmount = CalculateWinAmount(hand.Bet, mult);
                        hand.RoundResult = winAmount;
                        long preBank = p.Bank;
                        p.Bank += (hand.Bet + winAmount);
                        p.LastRoundResult += winAmount;
                        RecordLeg(p, preBank, p.Bank, p.Bank - preBank);
                    }
                    else if (pScore == dealerScore)
                    {
                        pushList.Add(shortName);
                        long refund = GetPushRefund(hand, cfg);
                        hand.RoundResult = refund - hand.Bet;
                        long preBank = p.Bank;
                        p.Bank += refund;
                        p.LastRoundResult += hand.RoundResult;
                        RecordLeg(p, preBank, p.Bank, refund);
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
                        float mult = GetWinMultiplier(p, hand, pScore, cfg);

                        long winAmount = CalculateWinAmount(hand.Bet, mult);
                        hand.RoundResult = winAmount;
                        long preBank = p.Bank;
                        p.Bank += (hand.Bet + winAmount);
                        p.LastRoundResult += winAmount;
                        RecordLeg(p, preBank, p.Bank, p.Bank - preBank);
                        await CommandExecutor.ExecuteGroup("ResultPlayerWin", p.DisplayName, cfg);
                    }
                    else if (pScore == dealerScore)
                    {
                        long refund = GetPushRefund(hand, cfg);
                        hand.RoundResult = refund - hand.Bet;
                        long preBank = p.Bank;
                        p.Bank += refund;
                        p.LastRoundResult += hand.RoundResult;
                        RecordLeg(p, preBank, p.Bank, refund);
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

        foreach (var trace in payoutTraces.Values)
            RoundLogManager.RecordPayoutTrace(trace);

        ActivityLogManager.LogRoundEnd(dealer, players);
        RoundLogManager.AddRound(dealer, players, cfg);

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
