using System;
using System.Collections.Generic;
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
        [SerializeField] private Vector2 cardSpacing = new Vector2(0f, -190f);

        private GameSession _session;
        private readonly List<TrainCarCardView> _marketViews = new List<TrainCarCardView>();

        public event Action CardsAcquired;

        private void Awake()
        {
            if (drawDeckButton != null)
            {
                drawDeckButton.onClick.AddListener(OnDrawDeckClicked);
            }

            if (resetMarketButton != null)
            {
                resetMarketButton.onClick.AddListener(OnResetMarketClicked);
            }
        }

        private void OnDestroy()
        {
            if (drawDeckButton != null)
            {
                drawDeckButton.onClick.RemoveListener(OnDrawDeckClicked);
            }

            if (resetMarketButton != null)
            {
                resetMarketButton.onClick.RemoveListener(OnResetMarketClicked);
            }
        }

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

            if (marketCardPrefab == null)
            {
                Debug.LogError("[TrainCardMarketView] 没有绑定 marketCardPrefab。 ");
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

                var rect = view.transform as RectTransform;
                if (rect != null)
                {
                    float centeredIndex = i - (visibleCards.Count - 1) * 0.5f;
                    rect.anchoredPosition = new Vector2(centeredIndex * cardSpacing.x, centeredIndex * cardSpacing.y);
                }
            }
        }

        private void OnMarketCardClicked(TrainCarCard card)
        {
            if (_session == null || card == null)
            {
                return;
            }

            try
            {
                _session.TurnManager.TakeOneMarketCard(card);
                Debug.Log($"[TrainCardMarketView] 从市场获得: {card}");
                CardsAcquired?.Invoke();
                Refresh();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TrainCardMarketView] 拿取市场车票失败: {exception.Message}");
            }
        }

        private void OnDrawDeckClicked()
        {
            if (_session == null)
            {
                return;
            }

            try
            {
                var card = _session.TurnManager.DrawTrainCard();
                Debug.Log($"[TrainCardMarketView] 从牌堆顶摸到: {card}");
                CardsAcquired?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TrainCardMarketView] 从牌堆摸牌失败: {exception.Message}");
            }
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
    }
}
