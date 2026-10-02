using System.Collections.Generic;
using UnityEngine;
using TicketToRide.Application;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 市场明牌区。只负责「显示 + 上报点击槽位」，不再直接读 GameState：
    /// 展示数据由 Refresh 传入（单机来自权威状态，客户端来自快照）；
    /// 取牌动作通过控制器提交 MarketSlot，单机立即执行、联机发给主机。
    /// </summary>
    public class TrainCardMarketView : MonoBehaviour
    {
        [SerializeField] private Transform marketRoot;
        [SerializeField] private TrainCarCardView marketCardPrefab;
        [SerializeField] private TrainCarCardSpriteLibrary spriteLibrary;
        [SerializeField] private Vector2 cardSpacing = new Vector2(0f, -190f);

        private IGameController _controller;
        private readonly List<TrainCarCardView> _marketViews = new List<TrainCarCardView>();

        /// <summary>接入控制器：点牌时提交 TakeMarketCard 命令。</summary>
        public void Bind(IGameController controller)
        {
            _controller = controller;
        }

        public void Refresh(IReadOnlyList<TrainCarCard> cards)
        {
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

            if (cards == null)
            {
                return;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
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

                // 槽位下标是市场位置的唯一事实来源，所以点击只上报下标、不上报颜色。
                int slot = i;
                view.OnCardClicked += _ => OnMarketCardClicked(slot);
                _marketViews.Add(view);

                var rect = view.transform as RectTransform;
                if (rect != null)
                {
                    float centeredIndex = i - (cards.Count - 1) * 0.5f;
                    rect.anchoredPosition = new Vector2(centeredIndex * cardSpacing.x, centeredIndex * cardSpacing.y);
                }
            }
        }

        private void OnMarketCardClicked(int slot)
        {
            if (_controller == null)
            {
                Debug.LogWarning("[TrainCardMarketView] 控制器尚未接入，无法取牌。");
                return;
            }

            Debug.Log($"[TrainCardMarketView] 从市场槽位 {slot} 取牌。");
            _controller.Submit(new GameCommand { Kind = CommandKind.TakeMarketCard, MarketSlot = slot });
        }
    }
}