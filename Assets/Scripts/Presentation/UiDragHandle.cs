using UnityEngine;
using UnityEngine.EventSystems;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 通用「拖动把手」：挂在标题栏这类空白区域上，把鼠标拖动位移累加到目标 RectTransform。
    /// 只让把手接收拖动，不挂在整块面板上——否则会和卡片、按钮的点击抢事件。
    /// </summary>
    public class UiDragHandle : MonoBehaviour, IDragHandler
    {
        /// <summary>被拖动的目标；不指定则拖动自己。</summary>
        public RectTransform Target;

        /// <summary>至少留在画面内的边距，避免把面板拖到看不见的地方。</summary>
        public float KeepVisibleMargin = 80f;

        public void OnDrag(PointerEventData eventData)
        {
            var target = Target != null ? Target : (RectTransform)transform;
            var canvas = target.GetComponentInParent<Canvas>();

            // eventData.delta 是屏幕像素；CanvasScaler 缩放后要换算成画布单位。
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            var position = target.anchoredPosition + eventData.delta / scale;

            // 目标在 Canvas 下按中心锚点摆放，所以这里也以画布中心为原点做范围限制。
            var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect != null)
            {
                float halfWidth = canvasRect.rect.width * 0.5f;
                float halfHeight = canvasRect.rect.height * 0.5f;
                position.x = Mathf.Clamp(position.x, -halfWidth + KeepVisibleMargin, halfWidth - KeepVisibleMargin);
                position.y = Mathf.Clamp(position.y, -halfHeight + KeepVisibleMargin, halfHeight - KeepVisibleMargin);
            }

            target.anchoredPosition = position;
        }
    }
}