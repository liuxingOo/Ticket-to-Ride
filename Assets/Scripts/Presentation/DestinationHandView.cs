using System.Collections.Generic;
using UnityEngine;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 左下角的任务卡手牌区：像手里抓的牌一样横向堆叠，
    /// 下面的牌只露出左边一小条，最后一张完整可见。
    /// 点击某张卡：弹起一点并在地图上显示两城蓝标；再点一次收回并隐藏蓝标。
    /// 组件挂在 Canvas 下自建的空物体上（物体本身就是容器，位置摆在手牌区左边）。
    /// </summary>
    public class DestinationHandView : MonoBehaviour
    {
        [Tooltip("单张任务卡的显示尺寸（画布像素，卡面等比缩放适配）。")]
        [SerializeField] private Vector2 cardSize = new Vector2(260f, 240f);

        [Tooltip("相邻两张卡的水平错位 = 下面那张露出的左边宽度。")]
        [SerializeField] private float overlapOffset = 55f;

        [Tooltip("选中时整张卡向上弹起的距离。")]
        [SerializeField] private float popUpOffset = 46f;

        private readonly List<DestinationTicketCardView> _cards = new List<DestinationTicketCardView>();
        private DestinationTicketCardView _selected;

        public void Refresh(Player player)
        {
            ClearCards();

            if (player == null)
            {
                return;
            }

            foreach (var ticket in player.DestinationTickets)
            {
                if (ticket == null)
                {
                    continue;
                }

                var cardObject = new GameObject($"Ticket_{ticket.Id}", typeof(RectTransform));
                cardObject.transform.SetParent(transform, false);

                var view = cardObject.AddComponent<DestinationTicketCardView>();
                var face = DestinationTicketSpriteLibrary.Global != null
                    ? DestinationTicketSpriteLibrary.Global.GetFace(ticket.ArtIndex)
                    : null;
                view.Initialize(ticket, face);
                view.Clicked += OnCardClicked;

                var rect = (RectTransform)cardObject.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = cardSize;

                _cards.Add(view);
            }

            _selected = null;
            LayoutCards();

            // 刷新意味着手里的卡变了，地图上残留的蓝标一并清掉。
            DestinationHighlightView.Global?.Hide();
        }

        private void ClearCards()
        {
            foreach (var card in _cards)
            {
                if (card != null)
                {
                    card.Clicked -= OnCardClicked;
                    Destroy(card.gameObject);
                }
            }

            _cards.Clear();
        }

        private void OnCardClicked(DestinationTicketCardView view)
        {
            if (_selected == view)
            {
                // 再点一次：收回堆叠，地图蓝标隐藏。
                _selected = null;
                LayoutCards();
                DestinationHighlightView.Global?.Hide();
                return;
            }

            _selected = view;
            LayoutCards();

            // 选中卡弹起一点并放到最上层。
            var rect = (RectTransform)view.transform;
            rect.anchoredPosition += Vector2.up * popUpOffset;
            rect.SetAsLastSibling();

            DestinationHighlightView.Global?.Show(view.Ticket.StartCity, view.Ticket.EndCity);
            Debug.Log($"[DestinationHandView] 任务卡 {view.Ticket.StartCity} → {view.Ticket.EndCity}（{view.Ticket.Points} 分）已在地图上标出。");
        }

        /// <summary>按堆叠规则摆放所有卡：第 i 张向右错开 overlap，整叠在容器内居中。</summary>
        private void LayoutCards()
        {
            int count = _cards.Count;
            float stackWidth = cardSize.x + Mathf.Max(0, count - 1) * overlapOffset;

            for (int i = 0; i < count; i++)
            {
                var view = _cards[i];
                if (view == null)
                {
                    continue;
                }

                var rect = (RectTransform)view.transform;
                float x = -stackWidth * 0.5f + cardSize.x * 0.5f + i * overlapOffset;
                rect.anchoredPosition = new Vector2(x, 0f);
                rect.SetSiblingIndex(i);
            }
        }
    }
}
