using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 任务卡被选中时，在地图上标出起点/终点两座城市（cities.png 的蓝色宝石标）。
    /// 挂在 Canvas/Map 下的空物体上（放在 Cities、Routes 之后，保证渲染在最上层），
    /// Inspector 拖入切好的蓝色宝石 sprite（cities_0）。
    /// </summary>
    public class DestinationHighlightView : MonoBehaviour
    {
        [Header("cities.png 左数第一个蓝色宝石切片")]
        [SerializeField] private Sprite blueMarker;

        [Tooltip("宝石在地图上的显示尺寸（画布像素）。")]
        [SerializeField] private float markerSize = 56f;

        public static DestinationHighlightView Global =>
            FindAnyObjectByType<DestinationHighlightView>();

        private readonly List<Image> _markers = new List<Image>();

        /// <summary>显示两座城市的蓝标；之前的高亮会先清掉。</summary>
        public void Show(string startCityId, string endCityId)
        {
            Hide();

            if (blueMarker == null)
            {
                Debug.LogWarning("[DestinationHighlightView] 没有拖蓝色宝石 sprite，无法显示任务城市。");
                return;
            }

            ShowMarker(startCityId);
            ShowMarker(endCityId);
        }

        /// <summary>隐藏所有蓝标。</summary>
        public void Hide()
        {
            foreach (var marker in _markers)
            {
                if (marker != null)
                {
                    Destroy(marker.gameObject);
                }
            }

            _markers.Clear();
        }

        private void ShowMarker(string cityId)
        {
            var city = FindCity(cityId);
            if (city == null)
            {
                Debug.LogWarning($"[DestinationHighlightView] 地图上找不到城市 {cityId}，检查 CityMarker.cityId 拼写。");
                return;
            }

            var layer = transform.parent as RectTransform;
            if (layer == null)
            {
                Debug.LogError("[DestinationHighlightView] 必须放在 Canvas/Map 之下才能换算像素坐标。");
                return;
            }

            var go = new GameObject($"Highlight_{cityId}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var image = go.GetComponent<Image>();
            image.sprite = blueMarker;
            image.preserveAspect = true;
            image.raycastTarget = false; // 不挡城市悬停/路线点击。

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(markerSize, markerSize);
            rect.anchoredPosition = MapPixelSpace.PixelToLocal(layer, city.PixelPosition);

            _markers.Add(image);
        }

        private static CityMarker FindCity(string cityId)
        {
            if (string.IsNullOrWhiteSpace(cityId))
            {
                return null;
            }

            var markers = FindObjectsByType<CityMarker>(FindObjectsInactive.Include);
            foreach (var marker in markers)
            {
                if (marker.CityId == cityId)
                {
                    return marker;
                }
            }

            return null;
        }
    }
}
