using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    public class PlayerHand
    {
        private readonly List<TrainCarCard> _cards;

        public int Count => _cards.Count;
        public bool IsEmpty => _cards.Count == 0;

        public PlayerHand()
        {
            _cards = new List<TrainCarCard>();
        }

        public PlayerHand(IEnumerable<TrainCarCard> cards)
        {
            _cards = cards?.ToList() ?? throw new ArgumentNullException(nameof(cards));
        }

        public void AddCard(TrainCarCard card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card), "Cannot add a null train car card to hand.");
            }

            _cards.Add(card);
        }

        public void AddCards(IEnumerable<TrainCarCard> cards)
        {
            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards), "Cards collection cannot be null.");
            }

            foreach (var card in cards)
            {
                AddCard(card);
            }
        }

        public List<TrainCarCard> DrawFromDeck(TrainCarDeck deck, int count)
        {
            if (deck == null)
            {
                throw new ArgumentNullException(nameof(deck), "Deck cannot be null.");
            }

            var drawnCards = deck.DrawMany(count);
            AddCards(drawnCards);
            return drawnCards;
        }

        public bool ContainsCard(TrainCarCard card)
        {
            return _cards.Contains(card);
        }

        public bool RemoveCard(TrainCarCard card)
        {
            return _cards.Remove(card);
        }

        public int GetColorCount(TrainCarColor color)
        {
            return _cards.Count(card => card.Color == color);
        }

        public IReadOnlyList<TrainCarCard> GetCards()
        {
            return _cards.AsReadOnly();
        }
    }
}
