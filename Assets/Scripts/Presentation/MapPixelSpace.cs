using UnityEngine;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// Map.png 像素坐标系与地图 UI 层局部坐标的统一换算。
    /// 像素坐标约定：左下角为原点，宽 1744、高 1125。
    /// 路线、城市等所有叠在地图上的元素都走这一套换算。
    /// </summary>
    public static class MapPixelSpace
    {
        public const float MapWidth = 1744f;
        public const float MapHeight = 1125f;

        /// <summary>Map.png 像素坐标 → 地图层（铺满地图的 RectTransform）局部坐标。</summary>
        public static Vector2 PixelToLocal(RectTransform layer, Vector2 pixel)
        {
            float width = layer != null && layer.rect.width > 0f ? layer.rect.width : MapWidth;
            float height = layer != null && layer.rect.height > 0f ? layer.rect.height : MapHeight;

            return new Vector2(
                pixel.x / MapWidth * width - width * 0.5f,
                pixel.y / MapHeight * height - height * 0.5f);
        }

        /// <summary>地图层局部坐标 → Map.png 像素坐标。</summary>
        public static Vector2 LocalToPixel(RectTransform layer, Vector2 local)
        {
            float width = layer != null && layer.rect.width > 0f ? layer.rect.width : MapWidth;
            float height = layer != null && layer.rect.height > 0f ? layer.rect.height : MapHeight;

            return new Vector2(
                (local.x + width * 0.5f) / width * MapWidth,
                (local.y + height * 0.5f) / height * MapHeight);
        }
    }
}
