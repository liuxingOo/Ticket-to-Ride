using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TicketToRide.Presentation
{
    public class DeckCountView : MonoBehaviour, IPointerClickHandler
    {
        public event Action OnDeckClicked;

        [SerializeField] private Image deckImage;
        [SerializeField] private Sprite level10Sprite;
        [SerializeField] private Sprite level20Sprite;
        [SerializeField] private Sprite level30Sprite;
        [SerializeField] private Sprite level40Sprite;
        [SerializeField] private Sprite level50Sprite;
        [SerializeField] private Sprite level60Sprite;
        [SerializeField] private Sprite level70Sprite;
        [SerializeField] private Sprite level80Sprite;
        [SerializeField] private Sprite level90Sprite;
        [SerializeField] private Sprite level100Sprite;

        public void Refresh(int remainingCount)
        {
            if (deckImage == null)
            {
                Debug.LogError("[DeckCountView] 没有绑定牌堆 Image。");
                return;
            }

            var selectedSprite = GetSpriteForCount(remainingCount);
            var level = GetLevelForCount(remainingCount);

            Debug.Log($"[DeckCountView] 剩余牌数: {remainingCount}，使用档位: {level}，图片: {(selectedSprite == null ? "空" : selectedSprite.name)}");

            if (selectedSprite == null && remainingCount > 0)
            {
                Debug.LogWarning($"[DeckCountView] 剩余 {remainingCount} 张时没有绑定对应牌堆图片，继续使用当前图片。");
            }
            else if (selectedSprite != null)
            {
                deckImage.sprite = selectedSprite;
            }

            deckImage.enabled = remainingCount > 0 && deckImage.sprite != null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (deckImage != null && !deckImage.enabled)
            {
                return;
            }

            OnDeckClicked?.Invoke();
        }

        private Sprite GetSpriteForCount(int remainingCount)
        {
            switch (GetLevelForCount(remainingCount))
            {
                case 100:
                    return level100Sprite;
                case 90:
                    return level90Sprite;
                case 80:
                    return level80Sprite;
                case 70:
                    return level70Sprite;
                case 60:
                    return level60Sprite;
                case 50:
                    return level50Sprite;
                case 40:
                    return level40Sprite;
                case 30:
                    return level30Sprite;
                case 20:
                    return level20Sprite;
                default:
                    return level10Sprite;
            }
        }

        private int GetLevelForCount(int remainingCount)
        {
            if (remainingCount <= 0)
            {
                return 0;
            }

            if (remainingCount >= 100)
            {
                return 100;
            }

            return (remainingCount / 10) * 10 < 20
                ? 10
                : (remainingCount / 10) * 10;
        }
    }
}
