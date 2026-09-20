using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    public class TrainCarDeck
    {
        private readonly Queue<TrainCarCard> _cards;
        private readonly List<TrainCarCard> _discardPile;

        public int Count => _cards.Count;
        public int DiscardCount => _discardPile.Count;
        public bool IsEmpty => _cards.Count == 0;
        public bool HasDiscardedCards => _discardPile.Count > 0;

        public TrainCarDeck(IEnumerable<TrainCarCard> cards = null)
        {
            _cards = new Queue<TrainCarCard>(cards ?? Enumerable.Empty<TrainCarCard>());
            _discardPile = new List<TrainCarCard>();
        }

        public static TrainCarDeck CreateStandardDeck()
        {
            var cards = new List<TrainCarCard>();

            foreach (TrainCarColor color in Enum.GetValues(typeof(TrainCarColor)))
            {
                if (color == TrainCarColor.Locomotive)
                {
                    for (int i = 0; i < 14; i++)
                    {
                        cards.Add(new TrainCarCard(color));
                    }

                    continue;
                }

                for (int i = 0; i < 12; i++)
                {
                    cards.Add(new TrainCarCard(color));
                }
            }

            var deck = new TrainCarDeck(cards);
            deck.Shuffle();
            return deck;
        }

        public void Shuffle()
        {
            var list = _cards.ToList();
            var rng = new Random();

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }

            _cards.Clear();

            foreach (var card in list)
            {
                _cards.Enqueue(card);
            }
        }

        public TrainCarCard DrawOne()
        {
            if (IsEmpty)
            {
                RecycleDiscardPile();
            }

            if (IsEmpty)
            {
                throw new InvalidOperationException("Cannot draw because both the train car deck and discard pile are empty.");
            }

            return _cards.Dequeue();
        }

        public List<TrainCarCard> DrawMany(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Draw count cannot be negative.");
            }

            var result = new List<TrainCarCard>();

            for (int i = 0; i < count && !IsEmpty; i++)
            {
                result.Add(DrawOne());
            }

            return result;
        }

        public void AddCard(TrainCarCard card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card), "Cannot add a null train car card.");
            }

            _cards.Enqueue(card);
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

        public void DiscardCard(TrainCarCard card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card), "Cannot discard a null train car card.");
            }

            _discardPile.Add(card);
        }

        public void DiscardCards(IEnumerable<TrainCarCard> cards)
        {
            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards), "Cards collection cannot be null.");
            }

            foreach (var card in cards)
            {
                DiscardCard(card);
            }
        }

        public void RecycleDiscardPile()
        {
            if (_discardPile.Count == 0)
            {
                return;
            }

            AddCards(_discardPile);
            _discardPile.Clear();
            Shuffle();
        }
    }
}
