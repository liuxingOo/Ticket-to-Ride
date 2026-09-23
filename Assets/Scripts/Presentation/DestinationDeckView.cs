using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 任务卡牌堆（市场区最下面）：按剩余张数换三档牌背
    /// 21-30 张 → 30 档，11-20 张 → 20 档，1-10 张 → 10 档，0 张 → 消失且不可点击。
    /// 用法和 DeckCountView 一样：物体上挂 Image + 本组件，Inspector 拖三张牌背 sprite。
    /// </summary>
    public class DestinationDeckView : MonoBehaviour, IPointerClickHandler
    {
        public event Action OnDeckClicked;

        [SerializeField] private Image deckImage;
        [SerializeField] private Sprite level10Sprite;
        [SerializeField] private Sprite level20Sprite;
        [SerializeField] private Sprite level30Sprite;

        /// <summary>按剩余张数刷新牌背档位。</summary>
        public void Refresh(int remainingCount)
        {
            if (deckImage == null)
            {
                Debug.LogError("[DestinationDeckView] 没有绑定牌堆 Image。");
                return;
            }

            var selectedSprite = GetSpriteForCount(remainingCount);

            if (selectedSprite != null)
            {
                deckImage.sprite = selectedSprite;
            }

            // 0 张：牌堆消失（Image 禁用），点击也一并失效。
            deckImage.enabled = remainingCount > 0 && deckImage.sprite != null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (deckImage == null || !deckImage.enabled)
            {
                return;
            }

            OnDeckClicked?.Invoke();
        }

        private Sprite GetSpriteForCount(int remainingCount)
        {
            if (remainingCount >= 21)
            {
                return level30Sprite;
            }

            if (remainingCount >= 11)
            {
                return level20Sprite;
            }

            return level10Sprite;
        }
    }
}
