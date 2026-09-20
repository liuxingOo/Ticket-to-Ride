using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TicketToRide.Application;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    public class TrainCardMarketView : MonoBehaviour
    {
        [SerializeField] private Transform marketRoot;
        [SerializeField] private TrainCarCardView marketCardPrefab;
        [SerializeField] private TrainCarCardSpriteLibrary spriteLibrary;
        [SerializeField] private Button drawDeckButton;
        [SerializeField] private Button resetMarketButton;

        private GameSession _session;
        private readonly List<TrainCarCardView> _marketViews = new List<TrainCarCardView>();
        private readonly List<TrainCarCard> _selectedMarketCards = new List<TrainCarCard>();

        public void Bind(GameSession session)
        {
            _session = session;
            Refresh();
        }

        public void Refresh()
        {
            if (_session == null || _session.GameState == null)
            {
                return;
            }

            if (marketRoot == null)
            {
                Debug.LogError("[TrainCardMarketView] 没有绑定 marketRoot。 ");
                return;
            }

            foreach (var view in _marketViews)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            _marketViews.Clear();
            _selectedMarketCards.Clear();

            var visibleCards = _session.GameState.TrainCarMarket.VisibleCards;
            for (int i = 0; i < visibleCards.Count; i++)
            {
                var card = visibleCards[i];
                if (card == null)
                {
                    continue;
                }

                var view = Instantiate(marketCardPrefab, marketRoot);
                if (view == null)
                {
                    continue;
                }

                var sprite = spriteLibrary != null ? spriteLibrary.GetSprite(card.Color) : null;
                view.Initialize(card, sprite);
                view.OnCardClicked += OnMarketCardClicked;
                _marketViews.Add(view);
            }

            if (drawDeckButton != null)
            {
                drawDeckButton.onClick.RemoveAllListeners();
                drawDeckButton.onClick.AddListener(OnDrawDeckClicked);
            }

            if (resetMarketButton != null)
            {
                resetMarketButton.onClick.RemoveAllListeners();
                resetMarketButton.onClick.AddListener(OnResetMarketClicked);
            }
        }

        private void OnMarketCardClicked(TrainCarCard card)
        {
            if (_session == null || card == null)
            {
                return;
            }

            if (_selectedMarketCards.Contains(card))
            {
                _selectedMarketCards.Remove(card);
                Debug.Log($"[TrainCardMarketView] 取消选择市场牌: {card}");
                return;
            }

            if (_selectedMarketCards.Count >= 2)
            {
                Debug.LogWarning("[TrainCardMarketView] 一次最多拿两张车票，且不能和市场机车叠加。 ");
                return;
            }

            if (card.IsLocomotive && _selectedMarketCards.Count > 0)
            {
                Debug.LogWarning("[TrainCardMarketView] 市场彩色车头车票只能单独拿一张，不能和第二张搭配。 ");
                return;
            }

            _selectedMarketCards.Add(card);
            Debug.Log($"[TrainCardMarketView] 选择市场牌: {card}");

            if (_selectedMarketCards.Count == 2)
            {
                TryTakeSelectedMarketCards();
            }
        }

        private void OnDrawDeckClicked()
        {
            if (_session == null)
            {
                return;
            }

            int deckDrawCount = 1;
            var selectedMarketCards = _selectedMarketCards.ToList();

            if (selectedMarketCards.Count == 0)
            {
                var taken = _session.TurnManager.AcquireTrainCards(new List<TrainCarCard>(), 1);
                Debug.Log($"[TrainCardMarketView] 从牌堆顶抽到: {string.Join(", ", taken.Select(c => c.ToString()))}");
                Refresh();
                return;
            }

            if (selectedMarketCards.Count == 1 && selectedMarketCards[0].IsLocomotive)
            {
                Debug.LogWarning("[TrainCardMarketView] 你已经选了市场机车，不能再从牌堆顶额外拿一张；只能选择这一张。 ");
                return;
            }

            var result = _session.TurnManager.AcquireTrainCards(selectedMarketCards, deckDrawCount);
            Debug.Log($"[TrainCardMarketView] 这次行动获得: {string.Join(", ", result.Select(c => c.ToString()))}");
            _selectedMarketCards.Clear();
            Refresh();
        }

        private void OnResetMarketClicked()
        {
            if (_session == null)
            {
                return;
            }

            _session.GameState.TrainCarMarket.ResetMarket();
            Refresh();
        }

        private void TryTakeSelectedMarketCards()
        {
            if (_session == null || _selectedMarketCards.Count == 0)
            {
                return;
            }

            var selected = _selectedMarketCards.ToList();
            if (selected.Count == 2 && selected.Any(card => card.IsLocomotive))
            {
                Debug.LogWarning("[TrainCardMarketView] 选中的市场彩色车头车票不能和第二张一起拿。 ");
                return;
            }

            var result = _session.TurnManager.AcquireTrainCards(selected, 0);
            Debug.Log($"[TrainCardMarketView] 从市场指定牌获得: {string.Join(", ", result.Select(c => c.ToString()))}");
            _selectedMarketCards.Clear();
            Refresh();
        }
    }
}
