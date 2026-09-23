using UnityEngine;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// road-highlight.png 切出的 9 帧高亮条与颜色的映射。
    /// 挂在场景里任意位置（Canvas 下就行），Inspector 里把 9 个 Sprite 拖到对应颜色槽。
    /// RouteVisual 通过 Global getter 直接获取（内部用 FindAnyObjectByType，不怕 Awake 顺序和热重载）。
    /// </summary>
    public class HighlightSpriteMapper : MonoBehaviour
    {
        [Header("road-highlight 9 帧 → 颜色映射（项目面板里切片后直接拖）")]
        public Sprite red;
        public Sprite orange;
        public Sprite yellow;
        public Sprite green;
        public Sprite blue;
        public Sprite pink;
        public Sprite black;
        public Sprite white;
        public Sprite locomotive;

        /// <summary>场景里唯一的实例，每次访问都查一次（Unity 6 里很快）。</summary>
        public static HighlightSpriteMapper Global => FindAnyObjectByType<HighlightSpriteMapper>();

        /// <summary>根据路线颜色取对应高亮 sprite；灰色路线走白色槽。</summary>
        public Sprite GetHighlightFor(TrainCarColor? routeColor)
        {
            if (!routeColor.HasValue)
            {
                return white;
            }

            switch (routeColor.Value)
            {
                case TrainCarColor.Red: return red;
                case TrainCarColor.Orange: return orange;
                case TrainCarColor.Yellow: return yellow;
                case TrainCarColor.Green: return green;
                case TrainCarColor.Blue: return blue;
                case TrainCarColor.Pink: return pink;
                case TrainCarColor.Black: return black;
                case TrainCarColor.White: return white;
                case TrainCarColor.Locomotive: return locomotive;
                default: return white;
            }
        }
    }
}

