using UnityEngine;
using UnityEngine.UI;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    public class RouteVisual : MonoBehaviour
    {
        private RouteVisualData _data;

        public void Initialize(RouteVisualData data)
        {
            _data = data;
            Debug.Log($"[RouteVisual] 初始化: {data.routeId}，起点: {data.startPosition}，终点: {data.endPosition}");
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            var direction = _data.endPosition - _data.startPosition;
            float distance = direction.magnitude;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var midpoint = (_data.startPosition + _data.endPosition) * 0.5f;

            var image = GetComponent<Image>();
            var rectTransform = transform as RectTransform;

            if (image != null && rectTransform != null)
            {
                Debug.Log($"[RouteVisual] UI 图像存在，Source Image 为空: {image.sprite == null}");
                rectTransform.anchoredPosition = midpoint;
                rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                rectTransform.sizeDelta = new Vector2(distance, rectTransform.sizeDelta.y);
                image.color = GetColorFor(_data.routeColor);
                image.raycastTarget = true;
            }
            else
            {
                var spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    Debug.LogWarning($"[RouteVisual] 使用旧的 SpriteRenderer 路径。Sprite 为空: {spriteRenderer.sprite == null}");
                    transform.localPosition = midpoint;
                    transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                    spriteRenderer.color = GetColorFor(_data.routeColor);
                    spriteRenderer.sortingOrder = 10;
                    transform.localScale = new Vector3(distance, 1f, 1f);
                }
                else
                {
                    Debug.LogError($"[RouteVisual] {gameObject.name} 没有 UI 图像或 SpriteRenderer，无法显示路线。");
                }
            }

            if (rectTransform == null)
            {
                var boxCollider = GetComponent<BoxCollider2D>();
                if (boxCollider != null)
                {
                    var size = boxCollider.size;
                    size.x = distance;
                    boxCollider.size = size;
                }
            }

            Debug.Log($"[RouteVisual] {gameObject.name} 起点: {_data.startPosition}，终点: {_data.endPosition}，中点: {midpoint}，长度: {distance}");
        }

        private Color GetColorFor(TrainCarColor color)
        {
            switch (color)
            {
                case TrainCarColor.Red:
                    return Color.red;
                case TrainCarColor.Orange:
                    return new Color(1f, 0.5f, 0f);
                case TrainCarColor.Yellow:
                    return Color.yellow;
                case TrainCarColor.Green:
                    return Color.green;
                case TrainCarColor.Blue:
                    return Color.blue;
                case TrainCarColor.Purple:
                    return new Color(0.5f, 0f, 0.8f);
                case TrainCarColor.Black:
                    return Color.black;
                case TrainCarColor.White:
                    return Color.white;
                case TrainCarColor.Locomotive:
                    return Color.gray;
                default:
                    return Color.white;
            }
        }
    }
}
