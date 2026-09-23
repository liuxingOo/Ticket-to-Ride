using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 一张任务卡的可点击视图：Image 和点击区域由代码在运行时生成（无需预制体），
    /// 卡面从 DestinationTicketSpriteLibrary 按 ArtIndex 取；没切图时显示文字占位。
    /// 点击只发事件，选中弹起、地图蓝标等由外层（手牌区/选择面板）决定。
    /// </summary>
    public class DestinationTicketCardView : MonoBehaviour, IPointerClickHandler
    {
        private Image _cardImage;
        private DestinationTicket _ticket;

        public event Action<DestinationTicketCardView> Clicked;

        public DestinationTicket Ticket => _ticket;

        public void Initialize(DestinationTicket ticket, Sprite face)
        {
            if (ticket == null)
            {
                Debug.LogError("[DestinationTicketCardView] 不能初始化空任务卡。");
                return;
            }

            _ticket = ticket;
            EnsureImage(face);

            if (face == null)
            {
                BuildTextLabel();
            }
        }

        private void EnsureImage(Sprite face)
        {
            if (_cardImage != null)
            {
                return;
            }

            _cardImage = GetComponent<Image>();
            if (_cardImage == null)
            {
                _cardImage = gameObject.AddComponent<Image>();
            }

            _cardImage.sprite = face;
            _cardImage.preserveAspect = true;
            _cardImage.raycastTarget = true; // 整张卡接收点击，堆叠时上层卡自动挡住下层。
        }

        /// <summary>没切卡面图时兜底：直接显示 起点→终点 + 分数。</summary>
        private void BuildTextLabel()
        {
            var labelObject = new GameObject("FallbackLabel", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(transform, false);

            var rect = (RectTransform)labelObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, 120f);

            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.12f, 0.1f, 0.06f, 1f);
            label.text = $"{_ticket.StartCity}\n→\n{_ticket.EndCity}\n{_ticket.Points} 分";
            label.raycastTarget = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_ticket == null)
            {
                return;
            }

            Clicked?.Invoke(this);
        }
    }
}
