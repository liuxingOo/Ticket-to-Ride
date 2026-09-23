using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    public class TurnManager
    {
        private readonly GameState _gameState;

        public TurnManager(GameState gameState)
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        }

        public TrainCarCard DrawTrainCard()
        {
            var card = _gameState.TrainCarDeck.DrawOne();
            _gameState.CurrentPlayer.Hand.AddCard(card);
            return card;
        }

        public TrainCarCard TakeOneMarketCard(TrainCarCard card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card), "Cannot take a null market card.");
            }

            var taken = _gameState.TrainCarMarket.TakeVisibleCards(new List<TrainCarCard> { card });
            if (taken.Count == 0)
            {
                throw new InvalidOperationException("The selected market card could not be taken.");
            }

            _gameState.CurrentPlayer.Hand.AddCards(taken);
            return taken[0];
        }

        public List<TrainCarCard> AcquireTrainCards(IEnumerable<TrainCarCard> marketCards, int deckDrawCount)
        {
            if (marketCards == null)
            {
                throw new ArgumentNullException(nameof(marketCards), "Visible market cards collection cannot be null.");
            }

            if (deckDrawCount < 0 || deckDrawCount > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(deckDrawCount), "Deck draw count must be between 0 and 2.");
            }

            var selectedMarketCards = marketCards.Distinct().ToList();
            int totalCardsTaken = selectedMarketCards.Count + deckDrawCount;

            if (totalCardsTaken == 0)
            {
                return new List<TrainCarCard>();
            }

            if (totalCardsTaken > 2)
            {
                throw new InvalidOperationException("A player can take at most two train cards in one action.");
            }

            var hasMarketLocomotive = selectedMarketCards.Any(card => card != null && card.IsLocomotive);
            if (hasMarketLocomotive)
            {
                var locomotiveCount = selectedMarketCards.Count(card => card != null && card.IsLocomotive);
                if (locomotiveCount > 1 || deckDrawCount > 0 || selectedMarketCards.Count != 1)
                {
                    throw new InvalidOperationException("If a market locomotive is taken, it must be the only card obtained in this action.");
                }
            }

            var takenFromMarket = new List<TrainCarCard>();
            if (selectedMarketCards.Count > 0)
            {
                takenFromMarket = _gameState.TrainCarMarket.TakeVisibleCards(selectedMarketCards);
            }

            var takenFromDeck = new List<TrainCarCard>();
            if (deckDrawCount > 0)
            {
                takenFromDeck = _gameState.TrainCarDeck.DrawMany(deckDrawCount);
            }

            var allTaken = takenFromMarket.Concat(takenFromDeck).ToList();
            _gameState.CurrentPlayer.Hand.AddCards(allTaken);

            if (_gameState.TrainCarMarket.CountVisibleLocomotives() >= 3)
            {
                _gameState.TrainCarMarket.ResetMarket();
            }

            return allTaken;
        }

        public void DiscardTrainCard(TrainCarCard card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card), "Card cannot be null.");
            }

            if (!_gameState.CurrentPlayer.Hand.RemoveCard(card))
            {
                throw new InvalidOperationException("The current player does not have this card.");
            }

            _gameState.TrainCarDeck.DiscardCard(card);
        }

        public void DrawTrainCards(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Draw count must be greater than zero.");
            }

            for (int i = 0; i < count; i++)
            {
                DrawTrainCard();
            }
        }

        /// <summary>
        /// 任务卡挑选结算：保留的加入当前玩家；只要有弃卡，就放回牌堆底并洗一次牌
        /// （开局挑选完、游戏中抽选走同一个方法）。
        /// </summary>
        public void ChooseDestinationTickets(IReadOnlyCollection<DestinationTicket> kept, IReadOnlyCollection<DestinationTicket> discarded)
        {
            if (kept == null)
            {
                throw new ArgumentNullException(nameof(kept));
            }

            if (discarded == null)
            {
                throw new ArgumentNullException(nameof(discarded));
            }

            _gameState.CurrentPlayer.AddDestinationTickets(kept);

            if (discarded.Count > 0)
            {
                _gameState.DestinationTicketDeck.ReturnToBottom(discarded);
                _gameState.DestinationTicketDeck.Shuffle();
            }
        }

        public bool ClaimRoute(Route route, IEnumerable<TrainCarCard> playedCards)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route), "Route cannot be null.");
            }

            if (playedCards == null)
            {
                throw new ArgumentNullException(nameof(playedCards), "Played cards cannot be null.");
            }

            var currentPlayer = _gameState.CurrentPlayer;
            var list = playedCards.ToList();

            if (!currentPlayer.CanClaimRoute(route, list))
            {
                return false;
            }

            currentPlayer.TryClaimRoute(route, list);
            return true;
        }

        public void EndTurn()
        {
            _gameState.NextTurn();
        }
    }
}
