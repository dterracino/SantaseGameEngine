﻿namespace Santase.AI.SmartPlayer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Santase.AI.SmartPlayer.Helpers;
    using Santase.Logic;
    using Santase.Logic.Cards;
    using Santase.Logic.Players;

    public class SmartPlayerOld : BasePlayer
    {
        private readonly ICollection<Card> playedCards = new List<Card>();

        private readonly OpponentSuitCardsProvider opponentSuitCardsProvider = new OpponentSuitCardsProvider();

        public override string Name => "Smart Player Old";

        public override PlayerAction GetTurn(PlayerTurnContext context)
        {
            // When possible change the trump card as this is always a good move
            if (this.PlayerActionValidator.IsValid(PlayerAction.ChangeTrump(), context, this.Cards))
            {
                return this.ChangeTrump(context.TrumpCard);
            }

            if (this.CloseGame(context))
            {
                return PlayerAction.CloseGame();
            }

            return this.ChooseCard(context);
        }

        public override void EndRound()
        {
            this.playedCards.Clear();
            base.EndRound();
        }

        public override void EndTurn(PlayerTurnContext context)
        {
            this.playedCards.Add(context.FirstPlayedCard);
            this.playedCards.Add(context.SecondPlayedCard);
        }

        // TODO: Close the game?
        private bool CloseGame(PlayerTurnContext context)
        {
            // 5 trump cards => close the game
            return this.PlayerActionValidator.IsValid(PlayerAction.CloseGame(), context, this.Cards)
                   && this.Cards.Count(x => x.Suit == context.TrumpCard.Suit) == 5;
        }

        // TODO: Choose appropriate card
        private PlayerAction ChooseCard(PlayerTurnContext context)
        {
            var possibleCardsToPlay = this.PlayerActionValidator.GetPossibleCardsToPlay(context, this.Cards);
            return context.State.ShouldObserveRules
                       ? (context.IsFirstPlayerTurn
                              ? this.ChooseCardWhenPlayingFirstAndRulesApply(context, possibleCardsToPlay)
                              : this.ChooseCardWhenPlayingSecondAndRulesApply(context, possibleCardsToPlay))
                       : (context.IsFirstPlayerTurn
                              ? this.ChooseCardWhenPlayingFirstAndRulesDoNotApply(context, possibleCardsToPlay)
                              : this.ChooseCardWhenPlayingSecondAndRulesDoNotApply(context, possibleCardsToPlay));
        }

        private PlayerAction ChooseCardWhenPlayingFirstAndRulesDoNotApply(PlayerTurnContext context, ICollection<Card> possibleCardsToPlay)
        {
            var action = this.TryToAnnounce20Or40(context, possibleCardsToPlay);
            if (action != null)
            {
                return action;
            }

            var opponentBiggestTrumpCard =
                this.opponentSuitCardsProvider.GetOpponentCards(this.Cards, this.playedCards, null, context.TrumpCard.Suit)
                    .OrderByDescending(x => x.GetValue())
                    .FirstOrDefault();
            var myBiggestTrumpCard =
                possibleCardsToPlay.Where(x => x.Suit == context.TrumpCard.Suit)
                    .OrderByDescending(x => x.GetValue())
                    .FirstOrDefault();

            if (myBiggestTrumpCard != null)
            {
                if (context.FirstPlayerRoundPoints >= 66 - myBiggestTrumpCard.GetValue())
                {
                    if (opponentBiggestTrumpCard == null
                        || myBiggestTrumpCard.GetValue() > opponentBiggestTrumpCard.GetValue())
                    {
                        return this.PlayCard(myBiggestTrumpCard);
                    }
                }
            }

            // Smallest non-trump card
            var cardToPlay =
                possibleCardsToPlay.Where(x => x.Suit != context.TrumpCard.Suit)
                    .OrderBy(x => x.GetValue())
                    .FirstOrDefault();
            if (cardToPlay != null)
            {
                return this.PlayCard(cardToPlay);
            }

            cardToPlay = possibleCardsToPlay.OrderByDescending(x => x.GetValue()).FirstOrDefault();
            return this.PlayCard(cardToPlay);
        }

        private PlayerAction ChooseCardWhenPlayingFirstAndRulesApply(
            PlayerTurnContext context,
            ICollection<Card> possibleCardsToPlay)
        {
            // Find card that will surely win the trick
            var opponentHasTrump = this.opponentSuitCardsProvider.GetOpponentCards(
                this.Cards,
                this.playedCards,
                null,
                context.TrumpCard.Suit).Any();

            var trumpCard = this.GetCardWhichWillSurelyWinTheTrick(context.TrumpCard.Suit, null, opponentHasTrump);
            if (trumpCard != null)
            {
                return this.PlayCard(trumpCard);
            }

            foreach (CardSuit suit in Enum.GetValues(typeof(CardSuit)))
            {
                var possibleCard = this.GetCardWhichWillSurelyWinTheTrick(suit, null, opponentHasTrump);
                if (possibleCard != null)
                {
                    return this.PlayCard(possibleCard);
                }
            }

            // Announce 20 or 40 if possible
            var action = this.TryToAnnounce20Or40(context, possibleCardsToPlay);
            if (action != null)
            {
                return action;
            }

            // Smallest non-trump card
            var cardToPlay =
                possibleCardsToPlay.Where(x => x.Suit != context.TrumpCard.Suit)
                    .OrderBy(x => x.GetValue())
                    .FirstOrDefault();
            if (cardToPlay != null)
            {
                return this.PlayCard(cardToPlay);
            }

            // Smallest card
            cardToPlay = possibleCardsToPlay.OrderBy(x => x.GetValue()).FirstOrDefault();
            return this.PlayCard(cardToPlay);
        }

        private Card GetCardWhichWillSurelyWinTheTrick(CardSuit suit, Card trumpCard, bool opponentHasTrump)
        {
            var myBiggestCard =
                this.Cards.Where(x => x.Suit == suit).OrderByDescending(x => x.GetValue()).FirstOrDefault();
            if (myBiggestCard == null)
            {
                return null;
            }

            var opponentBiggestCard =
                this.opponentSuitCardsProvider.GetOpponentCards(this.Cards, this.playedCards, null, suit)
                    .OrderByDescending(x => x.GetValue())
                    .FirstOrDefault();

            if (!opponentHasTrump && opponentBiggestCard == null)
            {
                return myBiggestCard;
            }

            if (opponentBiggestCard != null && opponentBiggestCard.GetValue() < myBiggestCard.GetValue())
            {
                return myBiggestCard;
            }

            return null;
        }

        private PlayerAction ChooseCardWhenPlayingSecondAndRulesDoNotApply(PlayerTurnContext context, ICollection<Card> possibleCardsToPlay)
        {
            // If bigger card is available => play it
            var biggerCard =
                possibleCardsToPlay.Where(
                    x => x.Suit == context.FirstPlayedCard.Suit && x.GetValue() > context.FirstPlayedCard.GetValue())
                    .OrderByDescending(x => x.GetValue())
                    .FirstOrDefault();
            if (biggerCard != null)
            {
                // Don't have Queen and King
                if (biggerCard.Type != CardType.Queen || !this.Cards.Contains(new Card(biggerCard.Suit, CardType.King)))
                {
                    if (biggerCard.Type != CardType.King
                        || !this.Cards.Contains(new Card(biggerCard.Suit, CardType.Queen)))
                    {
                        return this.PlayCard(biggerCard);
                    }
                }
            }

            // When opponent plays Ace or Ten => play trump card
            if (context.FirstPlayedCard.Type == CardType.Ace || context.FirstPlayedCard.Type == CardType.Ten)
            {
                if (possibleCardsToPlay.Contains(new Card(context.TrumpCard.Suit, CardType.Jack)))
                {
                    return this.PlayCard(new Card(context.TrumpCard.Suit, CardType.Jack));
                }

                if (possibleCardsToPlay.Contains(new Card(context.TrumpCard.Suit, CardType.Nine))
                    && context.TrumpCard.Type == CardType.Jack)
                {
                    return this.PlayCard(new Card(context.TrumpCard.Suit, CardType.Nine));
                }

                if (possibleCardsToPlay.Contains(new Card(context.TrumpCard.Suit, CardType.Queen))
                    && this.playedCards.Contains(new Card(context.TrumpCard.Suit, CardType.King)))
                {
                    return this.PlayCard(new Card(context.TrumpCard.Suit, CardType.Queen));
                }

                if (possibleCardsToPlay.Contains(new Card(context.TrumpCard.Suit, CardType.King))
                    && this.playedCards.Contains(new Card(context.TrumpCard.Suit, CardType.Queen)))
                {
                    return this.PlayCard(new Card(context.TrumpCard.Suit, CardType.King));
                }
            }

            // Smallest card
            var smallestCard = possibleCardsToPlay.OrderBy(x => x.GetValue()).FirstOrDefault();
            return this.PlayCard(smallestCard);
        }

        private PlayerAction ChooseCardWhenPlayingSecondAndRulesApply(
            PlayerTurnContext context,
            ICollection<Card> possibleCardsToPlay)
        {
            // If bigger card is available => play it
            var biggerCard =
                possibleCardsToPlay.Where(
                    x => x.Suit == context.FirstPlayedCard.Suit && x.GetValue() > context.FirstPlayedCard.GetValue())
                    .OrderByDescending(x => x.GetValue())
                    .FirstOrDefault();
            if (biggerCard != null)
            {
                return this.PlayCard(biggerCard);
            }

            // Play smallest trump card?
            var smallestTrumpCard =
                possibleCardsToPlay.Where(x => x.Suit == context.TrumpCard.Suit)
                    .OrderBy(x => x.GetValue())
                    .FirstOrDefault();
            if (smallestTrumpCard != null)
            {
                return this.PlayCard(smallestTrumpCard);
            }

            // Smallest card
            var cardToPlay = possibleCardsToPlay.OrderBy(x => x.GetValue()).FirstOrDefault();
            return this.PlayCard(cardToPlay);
        }

        private PlayerAction TryToAnnounce20Or40(PlayerTurnContext context, ICollection<Card> possibleCardsToPlay)
        {
            // Choose card with announce 40 if possible
            foreach (var card in possibleCardsToPlay)
            {
                if (card.Type == CardType.Queen
                    && this.AnnounceValidator.GetPossibleAnnounce(this.Cards, card, context.TrumpCard)
                    == Announce.Forty)
                {
                    return this.PlayCard(card);
                }
            }

            // Choose card with announce 20 if possible
            foreach (var card in possibleCardsToPlay)
            {
                if (card.Type == CardType.Queen
                    && this.AnnounceValidator.GetPossibleAnnounce(this.Cards, card, context.TrumpCard)
                    == Announce.Twenty)
                {
                    return this.PlayCard(card);
                }
            }

            return null;
        }
    }
}
