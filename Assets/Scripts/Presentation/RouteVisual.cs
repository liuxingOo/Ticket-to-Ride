using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 一条路线的视觉表现：沿两个端点按 length 生成一串"车厢"方块，
    /// 一比一覆盖在地图底图印刷的轨道上。
    /// 坐标统一使用 Map.png 的原图像素坐标（左下角为原点，宽 1744、高 1125），
    /// 由本组件自动换算成 UI 局部坐标；地图改尺寸/位置时无需改动数据。
    /// </summary>
    [ExecuteAlways]
    public class RouteVisual : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float MapTextureWidth = MapPixelSpace.MapWidth;
        public const float MapTextureHeight = MapPixelSpace.MapHeight;

        /// <summary>弯道贝塞尔曲线的采样段数（同时决定端点按弧长裁剪的精度）。</summary>
        private const int CurveSamples = 24;

        [Header("路线数据（端点为 Map.png 像素坐标，左下角原点）")]
        [SerializeField] private string routeId = "route";
        [SerializeField] private Vector2 startPixel = new Vector2(300f, 900f);
        [SerializeField] private Vector2 endPixel = new Vector2(600f, 900f);
        [Tooltip("节数：1~6。改这里即改该路线的车厢节数，会自动重建；" +
                 "车厢/热区/高亮大小全图统一，不受节数影响。")]
        [SerializeField, Range(1, 6)] private int length = 3;
        [SerializeField] private TrainCarColor routeColor = TrainCarColor.Red;
        [Tooltip("灰色万能路线：不挑车厢颜色，任意同色卡均可认领。勾选后 routeColor 仅作占位。")]
        [SerializeField] private bool isGreyRoute;
        [SerializeField] private bool isClaimed;
        [Tooltip("认领后显示的玩家颜色（由认领控制器写入）。")]
        [SerializeField] private PlayerTrainColor claimedPlayerColor = PlayerTrainColor.Red;
        [Tooltip("鼠标悬停时热区的高亮色（编辑模式青色预览会被此色覆盖）。")]
        [SerializeField] private Color hoverColor = new Color(1f, 0.92f, 0.3f, 0.35f);

        [Header("城市绑定（默认端点强制吸附城市；取消锁定时用像素手摆）")]
        [SerializeField] private string startCityId = "";
        [SerializeField] private string endCityId = "";

        [Tooltip("勾选（默认）：端点强制吸附到城市中心，路线自动跟随城市。" +
                 "取消勾选：端点改用 startPixel/endPixel，可在 Scene 视图拖到印刷轨道的实际起止位置。" +
                 "地图上有些印刷轨道并没有连到城市（中间有间隙），这类路线必须取消勾选才能对齐。" +
                 "城市绑定仍然保留，只影响端点像素位置与游戏逻辑无关。")]
        [SerializeField] private bool lockEndpointsToCities = true;

        [Tooltip("平行双线专用：整条轨道沿垂直方向平移的距离（Map.png 像素单位）。" +
                 "一对双线分别取负值/正值，避免两条隐形热区完全重叠误点。")]
        [SerializeField] private float sideOffsetPixels;

        [Header("弯道形状")]
        [Tooltip("线路弯曲程度：0=直线；正负表示向轨道两侧弯曲，绝对值越大越弯（±0.9 为极限）。" +
                 "整条线路为一条二次贝塞尔曲线，只需调这一个滑块，无需摆放任何路径点。")]
        [SerializeField, Range(-0.9f, 0.9f)] private float curveAmount;

        [Header("路径顶点（折线，Map.png 像素坐标；留空则用上面的曲线/直线）")]
        [Tooltip("折线内部顶点：起点城市 → 这些顶点依次连接 → 终点城市。" +
                 "留空时按 curveAmount 生成弧线。双线路线会自动整体平移 sideOffsetPixels，" +
                 "所以这里填「基准车道」的坐标即可。用 Inspector 上的按钮生成/清空。")]
        [SerializeField] private List<Vector2> pathPointsPixels = new List<Vector2>();

        [Header("逐节车厢角度覆盖（贴不上时单独掰某一节）")]
        [Tooltip("按节号（0 起）覆盖该节车厢使用的 wagon 角度帧：0~35 对应 0°~175°，每帧 5°；" +
                 "-1 = 自动（沿折线取所在段的角度）。只覆盖角度，不覆盖位置。" +
                 "一般不用手改这里，用下方 Inspector 的逐节滑条。")]
        [SerializeField] private List<int> carAngleOverrides = new List<int>();

        [Header("点击热区")]
        [Tooltip("勾选后不绘制车厢，只保留一条隐形可点击区域覆盖在印刷轨道上；" +
                 "编辑模式下用半透明青色块预览范围，运行时完全透明但仍可点击。")]
        [SerializeField] private bool invisibleHitArea = true;

        [Tooltip("热区垂直轨道方向的厚度 = 每节长度 × 该系数。0.65 按实测印刷格宽（约44/68）刚好盖住轨道，不挡城市 marker 与相邻双轨。")]
        [SerializeField, Range(0.3f, 2f)] private float hitThicknessFactor = 0.65f;

        [Header("车厢外观（取消勾选『隐形热区』时才生效）")]
        [Tooltip("单节车厢沿轨道方向的宽度 = 节长 × 该系数，小于 1 会留出车厢间隙。")]
        [SerializeField, Range(0.5f, 1.2f)] private float gapFactor = 0.92f;
        [Tooltip("车厢厚度（垂直轨道方向）缩放：1 = 按原图宽高比，调大车厢变粗。")]
        [SerializeField, Range(0.4f, 2f)] private float thicknessFactor = 1f;
        [SerializeField] private Sprite carSprite;
        [SerializeField] private Color availableColor = new Color(0.85f, 0.85f, 0.85f, 0.28f);
        [SerializeField] private Color outlineColor = new Color(0.08f, 0.08f, 0.08f, 0.55f);
        [SerializeField] private float outlineWidth = 2f;

        [Header("认领后 wagon 素材（WagonSpriteMapper 在场时生效）")]
        [Tooltip("车体长度相对每节轨道格的覆盖系数：1=相邻车厢首尾恰好相接；>1（如1.03）轻微重叠盖住印刷边框；<1 留缝。")]
        [SerializeField, Range(0.8f, 1.2f)] private float wagonCoverScale = 1.02f;
        [Tooltip("角度帧随路线角增长的方向：+1=逆时针；若斜向车厢朝向反了改成 -1。")]
        [SerializeField] private float wagonAngleSign = 1f;

        public string RouteId => routeId;
        public Vector2 StartPixel => startPixel;
        public Vector2 EndPixel => endPixel;
        public int Length => length;
        public TrainCarColor RouteColor => routeColor;
        public bool IsGreyRoute => isGreyRoute;
        public float SideOffsetPixels => sideOffsetPixels;
        /// <summary>线路弯曲程度（0=直线）。</summary>
        public float CurveAmount => curveAmount;

        /// <summary>折线内部顶点数量（0 表示按 curveAmount 曲线/直线生成）。</summary>
        public int PathPointCount => pathPointsPixels != null ? pathPointsPixels.Count : 0;

        public bool HasPathPoints => PathPointCount > 0;

        /// <summary>取第 index 个折线顶点（Map.png 像素，未含双线平移）。</summary>
        public Vector2 GetPathPoint(int index)
        {
            return pathPointsPixels[index];
        }

        /// <summary>设置第 index 个折线顶点并重建（坐标未含双线平移）。</summary>
        public void SetPathPoint(int index, Vector2 pixel)
        {
            if (pathPointsPixels == null || index < 0 || index >= pathPointsPixels.Count)
            {
                return;
            }

            pathPointsPixels[index] = ClampToMap(pixel);
            SafeRebuild();
        }

        /// <summary>在末尾追加一个折线顶点并重建。</summary>
        public void AddPathPoint(Vector2 pixel)
        {
            if (pathPointsPixels == null)
            {
                pathPointsPixels = new List<Vector2>();
            }

            pathPointsPixels.Add(ClampToMap(pixel));
            SafeRebuild();
        }

        /// <summary>删除第 index 个折线顶点并重建。</summary>
        public void RemovePathPoint(int index)
        {
            if (pathPointsPixels == null || index < 0 || index >= pathPointsPixels.Count)
            {
                return;
            }

            pathPointsPixels.RemoveAt(index);
            SafeRebuild();
        }

        /// <summary>清空所有折线顶点，回到曲线/直线模式。</summary>
        public void ClearPathPoints()
        {
            if (pathPointsPixels == null)
            {
                pathPointsPixels = new List<Vector2>();
            }

            pathPointsPixels.Clear();
            SafeRebuild();
        }

        /// <summary>
        /// 已有顶点时不动作；没有顶点时由当前 curveAmount 生成 1 个初始顶点
        /// （曲线控制点位置），方便在此基础上拖动微调，而不是从零开始摆。
        /// </summary>
        public void InitializePathPointFromCurve()
        {
            if (HasPathPoints)
            {
                return;
            }

            Vector2 chord = endPixel - startPixel;
            float chordLen = chord.magnitude;
            Vector2 mid = (startPixel + endPixel) * 0.5f;
            Vector2 vertex = mid;
            if (Mathf.Abs(curveAmount) > 0.001f && chordLen > 1f)
            {
                Vector2 normal = new Vector2(-chord.y, chord.x) / chordLen;
                vertex = mid + normal * (curveAmount * chordLen * 0.5f);
            }

            AddPathPoint(vertex);
        }

        /// <summary>在当前路径的弧长中点插入一个顶点（用于把一段长直线掰成两段）。</summary>
        public void InsertPathPointAtMiddle()
        {
            var pts = new List<Vector2>();
            BuildPathPixels(pts);
            float[] cum = CumulativeLengths(pts);
            Vector2 mid = PointAtArc(pts, cum, cum[cum.Length - 1], cum[cum.Length - 1] * 0.5f);
            AddPathPoint(mid - SideShift());
        }

        /// <summary>一节路线最多几节车厢（与 length 的 Range 上限一致）。</summary>
        public const int MaxCarCount = 6;

        /// <summary>
        /// 第 index 节的覆盖角度帧（0~35）；返回 -1 表示自动（未设置或越界）。
        /// 帧 ←→ 角度：帧 × 5°。
        /// </summary>
        public int GetCarAngleOverride(int index)
        {
            if (carAngleOverrides == null || index < 0 || index >= carAngleOverrides.Count)
            {
                return -1;
            }

            int frame = carAngleOverrides[index];
            return frame >= 0 && frame < WagonSpriteMapper.FrameCount ? frame : -1;
        }

        /// <summary>是否存在至少一节被手动指定了角度帧。</summary>
        public bool HasAnyCarAngleOverride
        {
            get
            {
                for (int i = 0; i < MaxCarCount; i++)
                {
                    if (GetCarAngleOverride(i) >= 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 指定第 index 节使用的角度帧（0~35，帧 × 5°）；传 -1 恢复自动。
        /// 只改朝向，不改该节在折线上的位置。
        /// </summary>
        public void SetCarAngleOverride(int index, int frame)
        {
            if (index < 0 || index >= MaxCarCount)
            {
                return;
            }

            if (carAngleOverrides == null)
            {
                carAngleOverrides = new List<int>();
            }

            while (carAngleOverrides.Count <= index)
            {
                carAngleOverrides.Add(-1);
            }

            carAngleOverrides[index] = frame < 0
                ? -1
                : Mathf.Clamp(frame, 0, WagonSpriteMapper.FrameCount - 1);
            SafeRebuild();
        }

        /// <summary>清除所有逐节角度覆盖，全部恢复自动。</summary>
        public void ClearCarAngleOverrides()
        {
            if (carAngleOverrides == null || carAngleOverrides.Count == 0)
            {
                return;
            }

            for (int i = 0; i < carAngleOverrides.Count; i++)
            {
                carAngleOverrides[i] = -1;
            }

            SafeRebuild();
        }

        private static Vector2 ClampToMap(Vector2 pixel)
        {
            pixel.x = Mathf.Clamp(pixel.x, 0f, MapTextureWidth);
            pixel.y = Mathf.Clamp(pixel.y, 0f, MapTextureHeight);
            return pixel;
        }

        /// <summary>双线整体平移向量（Map.png 像素，起点→终点方向逆时针 90°）。</summary>
        private Vector2 SideShift()
        {
            Vector2 overall = endPixel - startPixel;
            if (Mathf.Abs(sideOffsetPixels) <= 0.01f || overall.sqrMagnitude <= 0.0001f)
            {
                return Vector2.zero;
            }

            return new Vector2(-overall.y, overall.x).normalized * sideOffsetPixels;
        }

        public bool IsClaimed => isClaimed;
        public string StartCityId => startCityId;
        public string EndCityId => endCityId;

        /// <summary>端点是否锁定到城市中心；false 时端点用 startPixel/endPixel 手摆。</summary>
        public bool LockEndpointsToCities => lockEndpointsToCities;

        public event System.Action<RouteVisual> RouteClicked;

        private bool _rebuildQueued;
        private readonly List<Image> _highlightImages = new();

        private void OnEnable()
        {
            SafeRebuild();
        }

        private void OnValidate()
        {
            SafeRebuild();
        }

        /// <summary>
        /// 编辑模式用 EditorApplication.delayCall 推迟 Rebuild（避开 SendMessage during OnValidate 警告）；
        /// Play 模式直接 Rebuild（无此警告）。
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
            // Play 模式：直接重建，不延迟。
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
            isGreyRoute = data.isGreyRoute;
            sideOffsetPixels = data.sideOffsetPixels;
            length = Mathf.Clamp(data.length, 1, 6);
            isClaimed = data.isClaimed;
            SafeRebuild();
        }

        public void SetStartPixel(Vector2 pixel)
        {
            startPixel = pixel;
            SafeRebuild();
        }

        public void SetEndPixel(Vector2 pixel)
        {
            endPixel = pixel;
            SafeRebuild();
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
            SafeRebuild();
        }

        /// <summary>
        /// 设置路线性质：isGrey=灰色万能路线；sideOffsetPx 为平行双线的垂直平移量
        /// （Map.png 像素单位，一对双线分别传负值/正值）。
        /// </summary>
        public void SetAppearance(bool isGrey, float sideOffsetPx)
        {
            isGreyRoute = isGrey;
            sideOffsetPixels = sideOffsetPx;
            SafeRebuild();
        }

        /// <summary>设置线路弯曲程度（0=直线，正负分别向两侧弯）并重建。</summary>
        public void SetCurveAmount(float value)
        {
            curveAmount = Mathf.Clamp(value, -0.9f, 0.9f);
            SafeRebuild();
        }

        /// <summary>
        /// 在 Canvas 下按城市名查找 CityMarker，把端点像素坐标更新为城市坐标。
        /// 找不到同名城市时该端保持原样。返回是否至少吸附了一端。
        /// </summary>
        public bool SnapEndpointsToCities()
        {
            // 取消端点锁定后，端点坐标完全由 startPixel/endPixel 决定，不再被城市覆盖。
            if (!lockEndpointsToCities)
            {
                return false;
            }

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

        private void TrimCarAngleOverrides()
        {
            if (carAngleOverrides == null || carAngleOverrides.Count <= length)
            {
                return;
            }

            carAngleOverrides.RemoveRange(length, carAngleOverrides.Count - length);
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

            // 节数调小后清掉越界的角度覆盖：这些节已经不存在，留着只会在节数改回去时
            // 突然复现旧角度（且 Inspector 上不显示，很难排查）。
            TrimCarAngleOverrides();

            // 绑定了城市的路线永远以城市坐标为准（拖动手柄无效，需先清空城市绑定）。
            SnapEndpointsToCities();

            // 预制体根节点上的旧 Image（一整条白条）不再使用。
            var rootImage = GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.enabled = false;
            }

            if (length < 1)
            {
                return;
            }

            // —— 1) Map.png 像素空间生成整条路径：起点 → 曲线采样 → 终点，无任何路径点 ——
            var pxPoints = new List<Vector2>();
            BuildPathPixels(pxPoints);

            // —— 2) 两端沿路径弧长向内缩 8px（沿曲线裁剪，弯道同样准确），
            //         避免热区盖到城市 marker 上导致中文名不显示 ——
            var pxCum = CumulativeLengths(pxPoints);
            float pxTotal = pxCum[pxCum.Length - 1];
            if (pxTotal < 2f)
            {
                return;
            }

            float insetPx = Mathf.Min(8f, pxTotal * 0.2f);
            var pxTrimmed = ResampleByArc(pxPoints, pxCum, insetPx, pxTotal - insetPx, CurveSamples);

            // —— 3) 像素点 → 路线层局部坐标 ——
            var path = new List<Vector2>(pxTrimmed.Count);
            foreach (var px in pxTrimmed)
            {
                path.Add(PixelToLocal(px));
            }

            // 各折线段长度、角度与累计弧长。
            PathMetrics(path, out _, out float[] segAng, out float[] cum, out float distance);

            if (distance < 1f)
            {
                return;
            }

            // 根物体放在两端点中点但不再整体旋转：弯道每节朝向不同，
            // 选帧/旋转全部下放到每节车厢自身。
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            Vector2 rootPos = (path[0] + path[path.Count - 1]) * 0.5f;
            root.anchoredPosition = rootPos;
            root.localRotation = Quaternion.identity;

            // —— 4) 车厢格尺寸：全图统一，取自 WagonSpriteMapper 的全局格尺寸 ——
            // 旧写法 step = distance / length（由两端城市间距决定），短线车厢会明显比长线大，已废弃。
            var layer = transform.parent as RectTransform;
            float pxScale = layer != null && layer.rect.width > 0f ? layer.rect.width / MapTextureWidth : 1f;
            var wagonMapper = WagonSpriteMapper.Global;
            float cellStd = (wagonMapper != null ? wagonMapper.CellPixels : WagonSpriteMapper.DefaultCellPixels) * pxScale;

            // —— 高亮：直线 1 段，弯道切成少量段近似曲线（不给每一小段都建 Image，省开销）——
            // 厚度固定为印刷格宽（cellStd × 热区系数），与段长无关：
            // 旧写法厚度随段长膨胀，长路线高亮会变成巨大色块。
            int highlightSteps = HasPathPoints
                ? Mathf.Clamp(PathPointCount + 1, 1, 6)
                : (Mathf.Abs(curveAmount) > 0.001f ? 6 : 1);
            float highlightThickness = cellStd * hitThicknessFactor;
            BuildHighlightSegments(root, ResampleByArc(path, cum, 0f, distance, highlightSteps), rootPos, highlightThickness);

            // 全图统一节长：任何路线、任何节数都用同一个 cellStd。
            // 旧写法在「节长×节数 > 实际路径长」时把该路线单独缩小（cellUsed = distance/length），
            // 导致节数不同的路线车厢/热区大小不一致，已移除。
            // 路径偏短造成的溢出属于路径形状问题，应由折线顶点修正，而不是改车厢大小。
            float cellUsed = cellStd;
            float block = cellStd * length;

            // 车厢块沿路径居中，两端各留一段空隙 → 第一节不再贴着城市端点。
            float pad = (distance - block) * 0.5f;
            Sprite fallbackSprite = carSprite != null
                ? carSprite
                : Resources.Load<Sprite>("Routes/train-car");
            if (fallbackSprite == null && rootImage != null)
            {
                fallbackSprite = rootImage.sprite;
            }

            for (int i = 0; i < length; i++)
            {
                // 第 i 节中心落在"留白 + (i + 0.5) 格"的弧长处，位置与角度取自所在折线段。
                float arc = pad + (i + 0.5f) * cellUsed;
                SamplePolyline(path, segAng, cum, arc, out Vector2 carWorld, out float carAngle);

                // 逐节角度覆盖：该节若指定了角度帧，就用帧号替换自动角度
                // （帧同时决定 wagon 贴图与烘焙朝向，位置仍来自上面的折线弧长采样）。
                int overrideFrame = GetCarAngleOverride(i);
                if (overrideFrame >= 0)
                {
                    carAngle = overrideFrame * 5f * wagonAngleSign;
                }

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
                carRect.anchoredPosition = carWorld - rootPos;

                var image = carObject.GetComponent<Image>();
                // 已认领路线不可再点击：必须把它的车厢排除出射线检测，
                // 否则实体车厢图会压住相邻路线的隐形热区，导致相邻路线点不动。
                image.raycastTarget = !isClaimed;

                if (isClaimed)
                {
                    // 已认领：优先用按该节角度取的 wagon 预渲染帧（每节不再额外旋转）。
                    // 该节被手动指定过角度帧时直接用它，避免角度→帧的来回换算。
                    int wagonFrame = overrideFrame >= 0 ? overrideFrame : WagonFrameForAngle(carAngle);
                    Sprite wagon = wagonMapper != null
                        ? wagonMapper.GetWagon(claimedPlayerColor, wagonFrame)
                        : null;

                    if (wagon != null)
                    {
                        // 每帧车体在 160 切格中的实际像素长度不同（透视渲染所致），
                        // 按该帧实测长度等比缩放，使车体沿轨道方向恰好占满一个节长（×覆盖系数），
                        // 相邻车厢首尾相接、盖住印刷轨道边框。
                        WagonSpriteMapper.GetFrameMetrics(wagonFrame, out float bodyLen, out float cAlong, out float cPerp);
                        float cell = cellUsed * (160f / Mathf.Max(1f, bodyLen)) * wagonCoverScale;
                        float pxToLocal = cell / 160f;
                        carRect.sizeDelta = new Vector2(cell, cell);
                        carRect.localRotation = Quaternion.identity;

                        // 美术在切格中不居中：把实测的车体中心偏移（沿/垂直车体固有朝向）
                        // 换算到路线层局部坐标并反向补偿，车体中心才会落在轨道中心线上。
                        float bakedRad = wagonFrame * 5f * Mathf.Deg2Rad;
                        Vector2 axisAlong = new Vector2(Mathf.Cos(bakedRad), Mathf.Sin(bakedRad));
                        Vector2 axisPerp = new Vector2(-Mathf.Sin(bakedRad), Mathf.Cos(bakedRad));
                        Vector2 centerOffset = (axisAlong * cAlong + axisPerp * cPerp) * pxToLocal;
                        carRect.anchoredPosition = carWorld - rootPos - centerOffset;

                        image.sprite = wagon;
                        image.color = Color.white;
                        continue;
                    }

                    // 该颜色 wagon 素材缺失时回退：玩家颜色方块 + 该节角度旋转。
                    carRect.sizeDelta = new Vector2(cellUsed * gapFactor, cellUsed * hitThicknessFactor);
                    carRect.localRotation = Quaternion.Euler(0f, 0f, carAngle);
                    image.color = GetClaimedColor(claimedPlayerColor);

                    var claimedOutline = carObject.AddComponent<Outline>();
                    claimedOutline.effectColor = outlineColor;
                    claimedOutline.effectDistance = new Vector2(outlineWidth, outlineWidth);
                    continue;
                }

                if (invisibleHitArea)
                {
                    // 热区沿该节方向铺满（不留缝，整段都能点）；垂直方向按系数加厚。
                    carRect.sizeDelta = new Vector2(cellUsed, cellUsed * hitThicknessFactor);
                    carRect.localRotation = Quaternion.Euler(0f, 0f, carAngle);

                    if (UnityEngine.Application.isPlaying)
                    {
                        // 游戏运行时：完全透明，但 raycastTarget 还在（可点）。
                        image.color = new Color(1f, 1f, 1f, 0f);
                    }
                    else
                    {
                        // 编辑模式：半透明青色预览，方便摆位。
                        image.color = new Color(0.15f, 0.9f, 1f, 0.22f);
                    }
                    continue;
                }

                // —— 可见车厢模式（编辑预览用）——
                float carWidth = cellUsed * gapFactor;
                float carHeight = carWidth * thicknessFactor;
                if (fallbackSprite != null && fallbackSprite.rect.height > 0f)
                {
                    float spriteAspect = fallbackSprite.rect.width / fallbackSprite.rect.height;
                    carHeight = carWidth / spriteAspect * thicknessFactor;
                }

                carRect.sizeDelta = new Vector2(carWidth, carHeight);
                carRect.localRotation = Quaternion.Euler(0f, 0f, carAngle);
                image.sprite = fallbackSprite;
                image.color = availableColor;

                var outline = carObject.AddComponent<Outline>();
                outline.effectColor = outlineColor;
                outline.effectDistance = new Vector2(outlineWidth, outlineWidth);
            }
        }

        /// <summary>按累计弧长在折线上采样：返回该位置点与所在段角度（屏幕空间，y 向上，角度逆时针为正）。</summary>
        private static void SamplePolyline(
            List<Vector2> path,
            float[] segAng,
            float[] cum,
            float arc,
            out Vector2 position,
            out float angle)
        {
            float total = cum[cum.Length - 1];
            position = PointAtArc(path, cum, total, arc);

            float clamped = Mathf.Clamp(arc, 0f, Mathf.Max(0f, total - 0.0001f));
            angle = segAng[segAng.Length - 1];
            for (int s = 0; s < segAng.Length; s++)
            {
                if (clamped <= cum[s + 1] || s == segAng.Length - 1)
                {
                    angle = segAng[s];
                    break;
                }
            }
        }

        /// <summary>
        /// 计算折线的各段长度、各段角度（度）与累计弧长。长度 = 段数 + 1。
        /// </summary>
        private static void PathMetrics(
            List<Vector2> points,
            out float[] segLen,
            out float[] segAng,
            out float[] cum,
            out float distance)
        {
            int segCount = points.Count - 1;
            segLen = new float[segCount];
            segAng = new float[segCount];
            cum = new float[segCount + 1];
            distance = 0f;

            for (int s = 0; s < segCount; s++)
            {
                Vector2 segDir = points[s + 1] - points[s];
                segLen[s] = segDir.magnitude;
                segAng[s] = segLen[s] > 0.0001f ? Mathf.Atan2(segDir.y, segDir.x) * Mathf.Rad2Deg : 0f;
                distance += segLen[s];
                cum[s + 1] = distance;
            }
        }

        /// <summary>
        /// 在 Map.png 像素空间生成整条路径：
        /// 有折线顶点 = 起点 → 顶点… → 终点（双线整体平移 sideOffsetPixels）；
        /// 无顶点 = 两点直线，curveAmount 非零时改用一条二次贝塞尔曲线
        /// （控制点在中点法线方向偏移 curveAmount × 弦长 × 0.5）。
        /// </summary>
        private void BuildPathPixels(List<Vector2> outPoints)
        {
            outPoints.Clear();

            Vector2 shift = SideShift();
            Vector2 a = startPixel + shift;
            Vector2 b = endPixel + shift;

            outPoints.Add(a);

            if (pathPointsPixels != null && pathPointsPixels.Count > 0)
            {
                for (int i = 0; i < pathPointsPixels.Count; i++)
                {
                    outPoints.Add(pathPointsPixels[i] + shift);
                }

                outPoints.Add(b);
                return;
            }

            Vector2 chord = b - a;
            float chordLen = chord.magnitude;
            if (Mathf.Abs(curveAmount) > 0.001f && chordLen > 1f)
            {
                Vector2 normal = new Vector2(-chord.y, chord.x) / chordLen;
                Vector2 control = (a + b) * 0.5f + normal * (curveAmount * chordLen * 0.5f);

                for (int i = 1; i < CurveSamples; i++)
                {
                    outPoints.Add(QuadraticBezier(a, control, b, i / (float)CurveSamples));
                }
            }

            outPoints.Add(b);
        }

        private static Vector2 QuadraticBezier(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }

        /// <summary>折线各顶点的累计弧长（长度 = 顶点数）。</summary>
        private static float[] CumulativeLengths(List<Vector2> points)
        {
            var cum = new float[points.Count];
            for (int i = 1; i < points.Count; i++)
            {
                cum[i] = cum[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            }

            return cum;
        }

        /// <summary>按累计弧长把折线重采样成等距折线：steps 段 → steps + 1 个顶点。</summary>
        private static List<Vector2> ResampleByArc(
            List<Vector2> points,
            float[] cum,
            float fromArc,
            float toArc,
            int steps)
        {
            steps = Mathf.Max(1, steps);
            float total = cum[cum.Length - 1];

            var result = new List<Vector2>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                result.Add(PointAtArc(points, cum, total, Mathf.Lerp(fromArc, toArc, i / (float)steps)));
            }

            return result;
        }

        /// <summary>折线上按累计弧长取一点。</summary>
        private static Vector2 PointAtArc(List<Vector2> points, float[] cum, float total, float arc)
        {
            arc = Mathf.Clamp(arc, 0f, total);

            for (int i = 0; i < cum.Length - 1; i++)
            {
                if (arc <= cum[i + 1] || i == cum.Length - 2)
                {
                    float segLen = cum[i + 1] - cum[i];
                    float t = segLen > 0.0001f ? (arc - cum[i]) / segLen : 0f;
                    return Vector2.Lerp(points[i], points[i + 1], t);
                }
            }

            return points[points.Count - 1];
        }

        /// <summary>
        /// 路线角度（度）→ wagon 帧号：36 帧覆盖 180°（每 5° 一帧，车厢头尾 180° 对称）。
        /// </summary>
        private int WagonFrameForAngle(float angleDeg)
        {
            float normalized = Mathf.Repeat(angleDeg * wagonAngleSign, 180f);
            int frame = Mathf.RoundToInt(normalized / 5f);
            if (frame >= WagonSpriteMapper.FrameCount)
            {
                frame = 0; // 恰好 180° 与 0° 同帧。
            }

            return frame;
        }

        private void ClearCars(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                string n = child.name;
                if (n.StartsWith("Car_") || n.StartsWith("Highlight"))
                {
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
            _highlightImages.Clear();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isClaimed)
            {
                return;
            }

            RouteClicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isClaimed || _highlightImages.Count == 0)
            {
                return;
            }

            SetHighlights(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isClaimed || _highlightImages.Count == 0)
            {
                return;
            }

            SetHighlights(false);
        }

        private void SetHighlights(bool enabled)
        {
            foreach (var image in _highlightImages)
            {
                if (image != null)
                {
                    image.enabled = enabled;
                }
            }
        }

        /// <summary>
        /// 标记路线已被某位玩家认领，用该玩家颜色重绘车厢。
        /// </summary>
        public void Claim(PlayerTrainColor playerColor)
        {
            if (isClaimed)
            {
                return;
            }

            isClaimed = true;
            claimedPlayerColor = playerColor;
            SafeRebuild();
        }

        /// <summary>
        /// 沿路径铺一条悬停高亮（原图 151×93 九宫格，按段长拉伸）。
        /// 直线路线只有 1 段；弯道切成少量段近似曲线，避免为每一小段都建 Image 拖慢场景。
        /// </summary>
        private void BuildHighlightSegments(RectTransform root, List<Vector2> polyline, Vector2 rootPos, float thickness)
        {
            _highlightImages.Clear();

            var mapper = HighlightSpriteMapper.Global;
            if (mapper == null)
            {
                return;
            }

            Sprite sprite = mapper.GetHighlightFor(isGreyRoute ? null : (TrainCarColor?)routeColor);
            if (sprite == null)
            {
                return;
            }

            PathMetrics(polyline, out float[] segLen, out float[] segAng, out _, out _);

            for (int s = 0; s < segLen.Length; s++)
            {
                if (segLen[s] < 1f)
                {
                    continue;
                }

                var go = new GameObject($"Highlight_{s}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(root, false);
                go.transform.SetAsFirstSibling(); // 压在车厢层下面。

                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = (polyline[s] + polyline[s + 1]) * 0.5f - rootPos;
                rt.localRotation = Quaternion.Euler(0f, 0f, segAng[s]);
                rt.sizeDelta = new Vector2(segLen[s], thickness);

                var image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.color = Color.white;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
                image.raycastTarget = false;
                image.enabled = false; // 默认隐藏，OnPointerEnter 时整条折线一起显示。
                _highlightImages.Add(image);
            }
        }

        private Color GetClaimedColor(PlayerTrainColor color)
        {
            switch (color)
            {
                case PlayerTrainColor.Red:
                    return new Color(0.85f, 0.2f, 0.2f, 1f);
                case PlayerTrainColor.Yellow:
                    return new Color(0.95f, 0.85f, 0.2f, 1f);
                case PlayerTrainColor.Blue:
                    return new Color(0.2f, 0.45f, 0.9f, 1f);
                case PlayerTrainColor.Green:
                    return new Color(0.25f, 0.7f, 0.3f, 1f);
                case PlayerTrainColor.Purple:
                    return new Color(0.6f, 0.25f, 0.85f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
