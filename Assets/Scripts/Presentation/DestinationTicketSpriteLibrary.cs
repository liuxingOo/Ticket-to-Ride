using System.Collections.Generic;
using UnityEngine;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// destinations.png 切出的 30 张任务卡卡面。
    /// 挂在场景里任意位置，Inspector 里按"10 列 × 3 行、从左到右从上到下"的顺序
    /// 把 destinations_0 ~ destinations_29 依次拖进列表（和 HighlightSpriteMapper 一个用法）。
    /// 卡牌通过领域层的 ArtIndex 找自己的卡面。
    /// </summary>
    public class DestinationTicketSpriteLibrary : MonoBehaviour
    {
        [Header("destinations_0 ~ destinations_29 按网格顺序拖入（0 = 左上 Boston-Miami）")]
        [SerializeField] private List<Sprite> cardFaces = new List<Sprite>();

        /// <summary>场景里唯一的实例，每次访问都查一次（不怕 Awake 顺序和热重载）。</summary>
        public static DestinationTicketSpriteLibrary Global =>
            FindAnyObjectByType<DestinationTicketSpriteLibrary>();

        /// <summary>按卡面序号取对应 sprite；没配置时返回 null（调用方会显示文字占位）。</summary>
        public Sprite GetFace(int artIndex)
        {
            if (artIndex < 0 || artIndex >= cardFaces.Count || cardFaces[artIndex] == null)
            {
                Debug.LogWarning($"[DestinationTicketSpriteLibrary] 没有配置第 {artIndex} 号卡面，" +
                                 "请把 destinations_0~29 按顺序拖进 Inspector 列表。");
                return null;
            }

            return cardFaces[artIndex];
        }
    }
}
