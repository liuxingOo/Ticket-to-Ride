using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    public class TrainCarMarket
    {
        private readonly TrainCarDeck _deck;
        private readonly List<TrainCarCard> _visibleCards;

        public const int DefaultVisibleCardCount = 5;

        public int MaxVisibleCards { get; }
        public IReadOnlyList<TrainCarCard> VisibleCards => _visibleCards.AsReadOnly();
        public int VisibleCount => _visibleCards.Count;

        public TrainCarMarket(TrainCarDeck deck, int maxVisibleCards = DefaultVisibleCardCount)
        {
            if (deck == null)
            {
                throw new ArgumentNullException(nameof(deck), "Market needs a deck source.");
            }

            if (maxVisibleCards <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxVisibleCards), "Market must expose at least one visible card.");
            }

            _deck = deck;
            _visibleCards = new List<TrainCarCard>();
            MaxVisibleCards = maxVisibleCards;
            RefillVisibleCards();

            // 初始发牌若出现 3 张及以上火车头，按规则整组弃掉重发。
            EnsureNoLocomotiveOverflow();
        }

        public void RefillVisibleCards()
        {
            while (_visibleCards.Count < MaxVisibleCards && !_deck.IsEmpty)
            {
                _visibleCards.Add(_deck.DrawOne());
            }
        }

        public void ResetMarket()
        {
            if (_visibleCards.Count > 0)
            {
                _deck.DiscardCards(_visibleCards);
                _visibleCards.Clear();
            }

            RefillVisibleCards();
            EnsureNoLocomotiveOverflow();
        }

        /// <summary>
        /// 规则：市场可见区出现 3 张及以上火车头时，整组弃掉重发。
        /// 循环直到满足条件或牌堆耗尽（安全上限防止极端情况死循环）。
        /// </summary>
        private void EnsureNoLocomotiveOverflow()
        {
            int safety = 0;
            while (CountVisibleLocomotives() >= 3 && safety < 100)
            {
                if (_visibleCards.Count > 0)
                {
                    _deck.DiscardCards(_visibleCards);
                    _visibleCards.Clear();
                }

                RefillVisibleCards();
                safety++;
            }
        }

        public TrainCarCard TakeTopDeckCard()
        {
            if (_deck.IsEmpty)
            {
                _deck.RecycleDiscardPile();
            }

            if (_deck.IsEmpty)
            {
                throw new InvalidOperationException("Cannot draw from the top of the deck because both deck and discard pile are empty.");
            }

            return _deck.DrawOne();
        }

        public List<TrainCarCard> TakeVisibleCards(IEnumerable<TrainCarCard> cardsToTake)
        {
            if (cardsToTake == null)
            {
                throw new ArgumentNullException(nameof(cardsToTake), "Cards to take cannot be null.");
            }

            var selected = cardsToTake.ToList();
            if (selected.Count == 0)
            {
                return new List<TrainCarCard>();
            }

            if (selected.Count > 2)
            {
                throw new InvalidOperationException("A player can only take up to two cards in one train-card action.");
            }

            if (selected.Any(card => card == null))
            {
                throw new ArgumentException("A selected card cannot be null.", nameof(cardsToTake));
            }

            if (selected.Any(card => !_visibleCards.Contains(card)))
            {
                throw new InvalidOperationException("One or more selected cards are not currently visible in the market.");
            }

            if (selected.Count > 1 && selected.Any(card => card.IsLocomotive))
            {
                throw new InvalidOperationException("If you take a locomotive from the market, you can only take that single locomotive and no second card.");
            }

            // 先把选中牌解析成“槽位编号”，槽位是市场位置的唯一事实来源。
            var emptiedSlots = selected
                .Select(card => _visibleCards.FindIndex(visible => visible.Equals(card)))
                .ToList();

            if (emptiedSlots.Any(slot => slot < 0))
            {
                throw new InvalidOperationException("Selected market card is no longer visible.");
            }

            var taken = new List<TrainCarCard>();

            // 从后往前删，避免删除前面的牌导致后面槽位编号整体前移。
            foreach (var slot in emptiedSlots.OrderByDescending(slot => slot))
            {
                taken.Add(_visibleCards[slot]);
                _visibleCards.RemoveAt(slot);
            }

            if (_deck.IsEmpty)
            {
                _deck.RecycleDiscardPile();
            }

            // 新牌插回被清空的原槽位，其它牌保持原位不动。
            foreach (var slot in emptiedSlots.OrderBy(slot => slot))
            {
                if (_deck.IsEmpty)
                {
                    break;
                }

                int insertIndex = Math.Min(slot, _visibleCards.Count);
                _visibleCards.Insert(insertIndex, _deck.DrawOne());
            }

            if (CountVisibleLocomotives() >= 3)
            {
                ResetMarket();
            }

            return taken;
        }

        public List<TrainCarCard> TakeTwoCardsFromMarketOrDeck(IEnumerable<TrainCarCard> visibleCardsToTake, bool takeTopDeckCard)
        {
            var taken = new List<TrainCarCard>();

            if (visibleCardsToTake != null)
            {
                taken.AddRange(TakeVisibleCards(visibleCardsToTake));
            }

            if (takeTopDeckCard)
            {
                var drawn = TakeTopDeckCard();
                taken.Add(drawn);
            }

            if (taken.Count > 2)
            {
                throw new InvalidOperationException("The player may only take a maximum of two cards in a single train-card acquisition action.");
            }

            if (taken.Count == 2 && taken.Any(card => card.IsLocomotive && taken.Count > 1))
            {
                throw new InvalidOperationException("A locomotive taken from the market cannot be combined with a second card by the same acquisition action.");
            }

            return taken;
        }

        public int CountVisibleLocomotives()
        {
            return _visibleCards.Count(card => card != null && card.IsLocomotive);
        }
    }
}
