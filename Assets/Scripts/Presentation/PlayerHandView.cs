using System.Collections.Generic;
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

            var cards = player.Hand.GetCards();
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null)
                {
                    continue;
                }

                var cardView = Instantiate(cardPrefab, handRoot);
                if (cardView == null)
                {
                    continue;
                }

                var sprite = spriteLibrary != null ? spriteLibrary.GetSprite(card.Color) : null;
                cardView.Initialize(card, sprite);

                var rect = cardView.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(i * cardSpacing.x, i * cardSpacing.y);
                }
            }
        }
    }
}
