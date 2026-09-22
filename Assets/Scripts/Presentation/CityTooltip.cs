using UnityEngine;
using UnityEngine.UI;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 城市悬停提示框：运行时在 Canvas 下自动创建一次（无需手动摆 UI），
    /// 深色圆角底 + 白色中文名，跟随鼠标，移开城市即隐藏。
    /// </summary>
    public class CityTooltip : MonoBehaviour
    {
        private static CityTooltip _instance;

        public static bool Exists => _instance != null;
        public static CityTooltip Instance => _instance;

        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _panel;
        private Text _label;

        private readonly Vector2 _panelSize = new Vector2(180f, 52f);
        private const int FontSize = 28;

        public static CityTooltip Get(Canvas canvas)
        {
            if (_instance != null)
            {
                return _instance;
            }

            var go = new GameObject("CityTooltip", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            var root = (RectTransform)go.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;

            _instance = go.AddComponent<CityTooltip>();
            _instance.Build(canvas, root);
            return _instance;
        }

        private void Build(Canvas canvas, RectTransform root)
        {
            _canvas = canvas;
            _root = root;

            // 底板（raycastTarget 必须关，否则提示框自己会挡住城市射线）。
            var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(root, false);
            _panel = (RectTransform)panelObject.transform;
            _panel.sizeDelta = _panelSize;

            var panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.12f, 0.12f, 0.14f, 0.88f);
            panelImage.raycastTarget = false;

            // 文字。
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(_panel, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 6f);
            labelRect.offsetMax = new Vector2(-10f, -6f);

            _label = labelObject.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_label.font == null)
            {
                _label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            _label.fontSize = FontSize;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;

            gameObject.SetActive(false);
        }

        public void Show(string text)
        {
            _label.text = text;

            // 按字数自适应宽度，最少 4 个字宽。
            float width = Mathf.Max(4, text.Length) * FontSize + 32f;
            _panel.sizeDelta = new Vector2(width, _panelSize.y);

            gameObject.SetActive(true);
            UpdatePosition();
        }

        public void Hide()
        {
            if (_instance != null)
            {
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            // 显示期间持续跟随鼠标。
            if (gameObject.activeSelf)
            {
                UpdatePosition();
            }
        }

        private void UpdatePosition()
        {
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _root, Input.mousePosition, camera, out var local))
            {
                return;
            }

            // 出现在鼠标右下方；靠近屏幕边缘时翻转到另一侧，避免超出画面。
            const float gap = 18f;
            Vector2 size = _panel.sizeDelta;
            Rect rect = _root.rect;

            float offsetX = gap + size.x * 0.5f;
            float offsetY = -(gap + size.y * 0.5f);

            if (local.x + offsetX + size.x * 0.5f > rect.xMax)
            {
                offsetX = -(gap + size.x * 0.5f);
            }

            if (local.y + offsetY - size.y * 0.5f < rect.yMin)
            {
                offsetY = gap + size.y * 0.5f;
            }

            _panel.anchoredPosition = local + new Vector2(offsetX, offsetY);
        }
    }
}
