using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    public class TrainCarCardView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image cardImage;
        [SerializeField] private Image borderImage;

        private TrainCarCard _card;
        public event Action<TrainCarCard> OnCardClicked;

        public TrainCarCard Card => _card;

        public void Initialize(TrainCarCard card, Sprite sprite)
        {
            _card = card;

            if (card == null)
            {
                Debug.LogError("[TrainCarCardView] 不能初始化空车票卡牌。");
                return;
            }

            if (cardImage != null)
            {
                cardImage.sprite = sprite;
                cardImage.preserveAspect = true;
                cardImage.raycastTarget = true;
            }
            else
            {
                Debug.LogWarning("[TrainCarCardView] 没有绑定 Image 组件，无法显示卡牌切片。");
            }

            if (borderImage != null)
            {
                borderImage.color = GetBorderColor(card.Color);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_card == null)
            {
                return;
            }

            OnCardClicked?.Invoke(_card);
        }

        private Color GetBorderColor(TrainCarColor color)
        {
            switch (color)
            {
                case TrainCarColor.Red:
                    return new Color(0.85f, 0.25f, 0.25f, 1f);
                case TrainCarColor.Orange:
                    return new Color(0.9f, 0.6f, 0.2f, 1f);
                case TrainCarColor.Yellow:
                    return new Color(0.9f, 0.85f, 0.3f, 1f);
                case TrainCarColor.Green:
                    return new Color(0.2f, 0.75f, 0.35f, 1f);
                case TrainCarColor.Blue:
                    return new Color(0.2f, 0.45f, 0.9f, 1f);
                case TrainCarColor.Purple:
                    return new Color(0.58f, 0.28f, 0.72f, 1f);
                case TrainCarColor.Black:
                    return new Color(0.18f, 0.18f, 0.18f, 1f);
                case TrainCarColor.White:
                    return new Color(0.9f, 0.92f, 0.92f, 1f);
                case TrainCarColor.Locomotive:
                    return new Color(0.6f, 0.12f, 0.12f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
