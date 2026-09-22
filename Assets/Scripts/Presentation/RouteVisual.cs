using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 一条路线的视觉表现：沿两个端点按 length 生成一串“车厢”方块，
    /// 一比一覆盖在地图底图印刷的轨道上。
    /// 坐标统一使用 Map.png 的原图像素坐标（左下角为原点，宽 1744、高 1125），
    /// 由本组件自动换算成 UI 局部坐标；地图改尺寸/位置时无需改动数据。
    /// </summary>
    [ExecuteAlways]
    public class RouteVisual : MonoBehaviour, IPointerClickHandler
    {
        public const float MapTextureWidth = MapPixelSpace.MapWidth;
        public const float MapTextureHeight = MapPixelSpace.MapHeight;

        [Header("路线数据（端点为 Map.png 像素坐标，左下角原点）")]
        [SerializeField] private string routeId = "route";
        [SerializeField] private Vector2 startPixel = new Vector2(300f, 900f);
        [SerializeField] private Vector2 endPixel = new Vector2(600f, 900f);
        [SerializeField, Range(1, 6)] private int length = 3;
        [SerializeField] private TrainCarColor routeColor = TrainCarColor.Red;
        [SerializeField] private bool isClaimed;

        [Header("城市绑定（填了城市名，端点自动吸附到城市；留空则用像素手摆）")]
        [SerializeField] private string startCityId = "";
        [SerializeField] private string endCityId = "";

        [Header("点击热区")]
        [Tooltip("勾选后不绘制车厢，只保留一条隐形可点击区域覆盖在印刷轨道上；" +
                 "编辑模式下用半透明青色块预览范围，运行时完全透明但仍可点击。")]
        [SerializeField] private bool invisibleHitArea = true;

        [Tooltip("热区垂直轨道方向的厚度 = 每节长度 × 该系数。拐弯处点不中就调大。")]
        [SerializeField, Range(0.5f, 2f)] private float hitThicknessFactor = 1.25f;

        [Header("车厢外观（取消勾选“隐形热区”时才生效）")]
        [Tooltip("单节车厢沿轨道方向的宽度 = 节长 × 该系数，小于 1 会留出车厢间隙。")]
        [SerializeField, Range(0.5f, 1.2f)] private float gapFactor = 0.92f;
        [Tooltip("车厢厚度（垂直轨道方向）缩放：1 = 按原图宽高比，调大车厢变粗。")]
        [SerializeField, Range(0.4f, 2f)] private float thicknessFactor = 1f;
        [SerializeField] private Sprite carSprite;
        [SerializeField] private Color availableColor = new Color(0.85f, 0.85f, 0.85f, 0.28f);
        [SerializeField] private Color outlineColor = new Color(0.08f, 0.08f, 0.08f, 0.55f);
        [SerializeField] private float outlineWidth = 2f;

        public string RouteId => routeId;
        public Vector2 StartPixel => startPixel;
        public Vector2 EndPixel => endPixel;
        public int Length => length;
        public TrainCarColor RouteColor => routeColor;
        public bool IsClaimed => isClaimed;
        public string StartCityId => startCityId;
        public string EndCityId => endCityId;

        public event System.Action<RouteVisual> RouteClicked;

        private bool _rebuildQueued;

        private void OnEnable()
        {
            SafeRebuild();
        }

        private void OnValidate()
        {
            SafeRebuild();
        }

        /// <summary>
        /// 编辑模式下 Unity 不允许在 OnValidate/OnEnable 里立刻 AddComponent
        /// （会报 SendMessage during OnValidate），用 delayCall 推迟到编辑器空闲时再重建。
        /// </summary>
        private void SafeRebuild()
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                if (_rebuildQueued)
                {
                    return;
                }

                _rebuildQueued = true;
                UnityEditor.EditorApplication.delayCall += DeferredRebuild;
                return;
            }
#endif
            Rebuild();
        }

#if UNITY_EDITOR
        private void DeferredRebuild()
        {
            _rebuildQueued = false;

            if (this == null || gameObject == null)
            {
                return;
            }

            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                return;
            }

            Rebuild();
        }
#endif

        /// <summary>
        /// 旧的 MapGenerator 数据入口，坐标语义同样是 Map.png 像素。
        /// </summary>
        public void Initialize(RouteVisualData data)
        {
            routeId = data.routeId;
            startPixel = data.startPosition;
            endPixel = data.endPosition;
            routeColor = data.routeColor;
            length = Mathf.Clamp(data.length, 1, 6);
            isClaimed = data.isClaimed;
            Rebuild();
        }

        public void SetStartPixel(Vector2 pixel)
        {
            startPixel = pixel;
            Rebuild();
        }

        public void SetEndPixel(Vector2 pixel)
        {
            endPixel = pixel;
            Rebuild();
        }

        /// <summary>
        /// 把路线两端绑定到两个城市：端点立即吸附，以后城市标记移动时
        /// 路线会自动跟随（Rebuild 时重新吸附）。传空字符串可解除绑定。
        /// </summary>
        public void BindCities(string startCity, string endCity)
        {
            startCityId = startCity ?? string.Empty;
            endCityId = endCity ?? string.Empty;
            SnapEndpointsToCities();
            Rebuild();
        }

        /// <summary>
        /// 在 Canvas 下按城市名查找 CityMarker，把端点像素坐标更新为城市坐标。
        /// 找不到同名城市时该端保持原样。返回是否至少吸附了一端。
        /// </summary>
        public bool SnapEndpointsToCities()
        {
            if (string.IsNullOrWhiteSpace(startCityId) && string.IsNullOrWhiteSpace(endCityId))
            {
                return false;
            }

            var canvas = GetComponentInParent<Canvas>();
            var searchRoot = canvas != null ? canvas.transform : transform.root;
            var cities = searchRoot.GetComponentsInChildren<CityMarker>(true);

            bool changed = false;

            foreach (var city in cities)
            {
                if (!string.IsNullOrWhiteSpace(startCityId) && city.CityId == startCityId)
                {
                    startPixel = city.PixelPosition;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(endCityId) && city.CityId == endCityId)
                {
                    endPixel = city.PixelPosition;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>Map.png 像素坐标 → 路线层（父容器）UI 局部坐标。</summary>
        public Vector2 PixelToLocal(Vector2 pixel)
        {
            return MapPixelSpace.PixelToLocal(transform.parent as RectTransform, pixel);
        }

        /// <summary>路线层 UI 局部坐标 → Map.png 像素坐标。</summary>
        public Vector2 LocalToPixel(Vector2 local)
        {
            return MapPixelSpace.LocalToPixel(transform.parent as RectTransform, local);
        }

        public void Rebuild()
        {
#if UNITY_EDITOR
            // 在预制体编辑窗口里打开预制体时不要生成一堆车厢污染预制体资源。
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                return;
            }
#endif

            var root = transform as RectTransform;
            if (root == null)
            {
                return;
            }

            ClearCars(root);

            // 绑定了城市的路线永远以城市坐标为准（拖动手柄无效，需先清空城市绑定）。
            SnapEndpointsToCities();

            // 预制体根节点上的旧 Image（一整条白条）不再使用。
            var rootImage = GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.enabled = false;
            }

            var startLocal = PixelToLocal(startPixel);
            var endLocal = PixelToLocal(endPixel);
            Vector2 direction = endLocal - startLocal;
            float distance = direction.magnitude;

            if (distance < 1f || length < 1)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // 根物体固定在路线层中心并整体旋转，车厢沿本地 +X 排列。
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.localRotation = Quaternion.Euler(0f, 0f, angle);

            float step = distance / length;
            Sprite sprite = carSprite != null
                ? carSprite
                : Resources.Load<Sprite>("Routes/train-car");
            if (sprite == null && rootImage != null)
            {
                sprite = rootImage.sprite;
            }

            for (int i = 0; i < length; i++)
            {
                var carObject = new GameObject(
                    $"Car_{i + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                carObject.transform.SetParent(root, false);

                var carRect = (RectTransform)carObject.transform;
                carRect.anchorMin = new Vector2(0.5f, 0.5f);
                carRect.anchorMax = new Vector2(0.5f, 0.5f);
                carRect.pivot = new Vector2(0.5f, 0.5f);
                carRect.anchoredPosition = new Vector2((i - (length - 1) * 0.5f) * step, 0f);

                var image = carObject.GetComponent<Image>();
                image.raycastTarget = true;

                if (invisibleHitArea)
                {
                    // 热区沿轨道方向铺满每一节（不留缝，整段都能点）；
                    // 垂直方向加厚，拐弯路线也容易点中。
                    carRect.sizeDelta = new Vector2(step, step * hitThicknessFactor);

#if UNITY_EDITOR
                    // 编辑模式：半透明青色预览，方便摆位；游戏里完全透明。
                    image.color = new Color(0.15f, 0.9f, 1f, 0.22f);
#else
                    image.color = new Color(1f, 1f, 1f, 0f);
#endif
                    continue;
                }

                // —— 可见车厢模式 ——
                float carWidth = step * gapFactor;
                float carHeight = carWidth * thicknessFactor;
                if (sprite != null && sprite.rect.height > 0f)
                {
                    float spriteAspect = sprite.rect.width / sprite.rect.height;
                    carHeight = carWidth / spriteAspect * thicknessFactor;
                }

                carRect.sizeDelta = new Vector2(carWidth, carHeight);
                image.sprite = sprite;
                image.color = isClaimed ? GetClaimedColor(routeColor) : availableColor;

                var outline = carObject.AddComponent<Outline>();
                outline.effectColor = outlineColor;
                outline.effectDistance = new Vector2(outlineWidth, outlineWidth);
            }
        }

        private void ClearCars(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (!child.name.StartsWith("Car_"))
                {
                    continue;
                }

                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isClaimed)
            {
                return;
            }

            RouteClicked?.Invoke(this);
            Debug.Log($"[RouteVisual] 点击路线 {routeId}（{routeColor}，{length} 节），后续在此接入认领逻辑。");
        }

        private Color GetClaimedColor(TrainCarColor color)
        {
            switch (color)
            {
                case TrainCarColor.Red:
                    return new Color(0.85f, 0.2f, 0.2f, 1f);
                case TrainCarColor.Orange:
                    return new Color(0.95f, 0.55f, 0.15f, 1f);
                case TrainCarColor.Yellow:
                    return new Color(0.95f, 0.85f, 0.2f, 1f);
                case TrainCarColor.Green:
                    return new Color(0.25f, 0.7f, 0.3f, 1f);
                case TrainCarColor.Blue:
                    return new Color(0.2f, 0.45f, 0.9f, 1f);
                case TrainCarColor.Purple:
                    return new Color(0.58f, 0.28f, 0.72f, 1f);
                case TrainCarColor.Black:
                    return new Color(0.15f, 0.15f, 0.15f, 1f);
                case TrainCarColor.White:
                    return new Color(0.92f, 0.92f, 0.9f, 1f);
                case TrainCarColor.Locomotive:
                    return new Color(0.6f, 0.12f, 0.12f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
