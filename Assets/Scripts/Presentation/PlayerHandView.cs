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

        public void Refresh(Player player)
        {
            if (player == null)
            {
                Debug.LogWarning("[PlayerHandView] 不能刷新空玩家手牌。");
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

            var groupedCards = player.Hand.GetCards()
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
