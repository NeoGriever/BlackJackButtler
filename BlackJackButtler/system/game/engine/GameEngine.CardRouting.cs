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
                (!string.IsNullOrWhiteSpace(name) && _ctxDealer.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ? _ctxDealer : _ctxPlayers.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

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

}
