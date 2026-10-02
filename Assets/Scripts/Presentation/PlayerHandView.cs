using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    public class PlayerHandView : MonoBehaviour
    {
        [SerializeField] private Transform handRoot;
        [SerializeField] private TrainCarCardView cardPrefab;
        [SerializeField] private TrainCarCardSpriteLibrary spriteLibrary;
        [SerializeField] private Vector2 cardSpacing = new Vector2(110f, 0f);

        /// <summary>
        /// 用手牌里的车厢卡重建手牌区。只接受卡列表，不碰 Player——
        /// 联机时客户端拿到的也只是本人手牌（由 IMatchView.MyHand 提供），看不到别人的牌。
        /// </summary>
        public void Refresh(IReadOnlyList<TrainCarCard> cards)
        {
            if (cards == null)
            {
                Debug.LogWarning("[PlayerHandView] 不能刷新空手牌列表。");
                return;
            }

            if (handRoot == null)
            {
                Debug.LogError("[PlayerHandView] 没有绑定手牌容器。");
                return;
            }

            foreach (Transform child in handRoot)
            {
                Destroy(child.gameObject);
            }

            var groupedCards = cards
                .Where(card => card != null)
                .GroupBy(card => card.Color)
                .OrderBy(group => (int)group.Key);

            var colorGroups = groupedCards.ToList();
            for (int slotIndex = 0; slotIndex < colorGroups.Count; slotIndex++)
            {
                var group = colorGroups[slotIndex];
                var cardView = Instantiate(cardPrefab, handRoot);
                if (cardView == null)
                {
                    continue;
                }

                var sprite = spriteLibrary != null ? spriteLibrary.GetSprite(group.Key) : null;
                cardView.Initialize(group.First(), sprite);
                cardView.SetCount(group.Count());

                var rect = cardView.transform as RectTransform;
                if (rect != null)
                {
                    // 以容器中心为基准向两侧对称排开，保证任意数量手牌都居中。
                    float centeredIndex = slotIndex - (colorGroups.Count - 1) * 0.5f;
                    rect.anchoredPosition = new Vector2(centeredIndex * cardSpacing.x, centeredIndex * cardSpacing.y);
                }
            }
        }
    }
}
