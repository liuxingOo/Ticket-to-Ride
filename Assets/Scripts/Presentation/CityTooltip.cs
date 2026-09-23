using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 城市悬停提示：运行时在 Canvas 下自动创建一次（无需手动摆 UI）。
    /// 无底板的小号中文名，仅靠一圈深色细描边保证在浅色/深色地图上都能看清，
    /// 跟随鼠标，移开城市即隐藏。任何子物体都不拦截射线。
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

        private const int FontSize = 24;
        private const float PanelHeight = 32f;

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

            // 纯定位容器，不加 Image：提示没有底板。
            var panelObject = new GameObject("Panel", typeof(RectTransform));
            panelObject.transform.SetParent(root, false);
            _panel = (RectTransform)panelObject.transform;
            _panel.sizeDelta = new Vector2(80f, PanelHeight);

            // 文字。
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(_panel, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            _label = labelObject.GetComponent<Text>();
            _label.font = LoadChineseFont();
            _label.fontSize = FontSize;
            _label.alignment = TextAnchor.MiddleCenter;
            // 黑色正文：浅色地图上直接可读；经过彩色/深色轨道时靠白色细描边托底。
            _label.color = Color.black;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;

            // 白色细描边：比深色描边轻，淡色地图上几乎察觉不到，只在字压到深色轨道时显出轮廓。
            var outline = labelObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.7f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 优先用系统中文字体（微软雅黑等），避免内置字体不支持中文显示成方块。
        /// </summary>
        private static Font LoadChineseFont()
        {
            string[] preferred =
            {
                "Microsoft YaHei",
                "SimHei",
                "PingFang SC",
                "Noto Sans CJK SC",
                "Source Han Sans SC"
            };

            var font = Font.CreateDynamicFontFromOSFont(preferred, FontSize);
            if (font != null)
            {
                return font;
            }

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        public void Show(string text)
        {
            _label.text = text;

            // 中文全角字宽约等于字号，按字数自适应，留 2px 余量避免描边被裁。
            float width = Mathf.Max(2, text.Length) * FontSize + 4f;
            _panel.sizeDelta = new Vector2(width, PanelHeight);

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

        /// <summary>
        /// 兼容两种输入后端：项目启用旧 Input Manager 时用 Input.mousePosition；
        /// 仅启用新版 Input System 时用 Mouse.current（否则每帧抛 InvalidOperationException）。
        /// </summary>
        private static Vector2 GetMousePosition()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#elif ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Vector2.zero;
#endif
        }

        private void UpdatePosition()
        {
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _root, GetMousePosition(), camera, out var local))
            {
                return;
            }

            // 出现在鼠标右上方一点，尽量不挡住正在看的城市圆点；
            // 靠近屏幕边缘时翻转到另一侧，避免超出画面。
            const float gap = 10f;
            Vector2 size = _panel.sizeDelta;
            Rect rect = _root.rect;

            float offsetX = gap + size.x * 0.5f;
            float offsetY = gap + size.y * 0.5f + 4f;

            if (local.x + offsetX + size.x * 0.5f > rect.xMax)
            {
                offsetX = -(gap + size.x * 0.5f);
            }

            if (local.y + offsetY + size.y * 0.5f > rect.yMax)
            {
                offsetY = -(gap + size.y * 0.5f);
            }

            _panel.anchoredPosition = local + new Vector2(offsetX, offsetY);
        }
    }
}
