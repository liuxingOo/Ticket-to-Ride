using System.Collections.Generic;
using UnityEngine;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 路线层的对齐与批量生成入口。
    /// 运行/编辑时都会把 Routes 容器拉伸对齐到地图 Image，
    /// 使路线的像素坐标与 Map.png 始终保持 1:1。
    /// 路线数据在摆好后随场景保存，运行时不再重复生成。
    /// </summary>
    [ExecuteAlways]
    public class MapGenerator : MonoBehaviour
    {
        [SerializeField] private GameObject routePrefab;
        [SerializeField] private RectTransform mapRect;
        [SerializeField] private Transform routeParent;
        [SerializeField] private List<RouteVisualData> routeVisualData = new List<RouteVisualData>();

        [Header("城市")]
        [SerializeField] private GameObject cityPrefab;
        [SerializeField] private Transform cityParent;

        [Header("官方路线批量生成")]
        [Tooltip("平行双线两条轨道中心线的间距（Map.png 像素单位）。" +
                 "热区对不齐印刷双轨时调这个值，改完重新点“一键生成”（已有双线需先清空）。")]
        [SerializeField] private float doubleRoutePixelGap = 20f;

        /// <summary>平行双线路由之间的中心间距（像素），供外部读取。</summary>
        public float DoubleRoutePixelGap => doubleRoutePixelGap;

        private void OnEnable()
        {
            AlignRouteLayer();
            AlignCityLayer(autoCreate: false);
        }

        private void OnValidate()
        {
            AlignRouteLayer();
            AlignCityLayer(autoCreate: false);
        }

        private void Start()
        {
            AlignRouteLayer();
            AlignCityLayer(autoCreate: false);
        }

        public void AlignRouteLayer()
        {
            AutoBindReferences();

            if (mapRect == null || routeParent == null)
            {
                return;
            }

            var layer = routeParent as RectTransform;
            if (layer == null)
            {
                Debug.LogError("[MapGenerator] Route Parent 不是 RectTransform，请使用 Canvas 下的 Routes 对象。");
                return;
            }

            bool changed = false;

            if (layer.parent != mapRect)
            {
                layer.SetParent(mapRect, false);
                changed = true;
            }

            if (layer.anchorMin != Vector2.zero
                || layer.anchorMax != Vector2.one
                || layer.offsetMin != Vector2.zero
                || layer.offsetMax != Vector2.zero
                || layer.localScale != Vector3.one)
            {
                // 拉伸铺满地图，路线层与地图图像素一一对应。
                layer.anchorMin = Vector2.zero;
                layer.anchorMax = Vector2.one;
                layer.pivot = new Vector2(0.5f, 0.5f);
                layer.offsetMin = Vector2.zero;
                layer.offsetMax = Vector2.zero;
                layer.sizeDelta = Vector2.zero;
                layer.localScale = Vector3.one;
                layer.localRotation = Quaternion.identity;
                changed = true;
            }

            if (changed)
            {
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(layer);
#endif
            }
        }

        private void AutoBindReferences()
        {
            if (mapRect == null)
            {
                var mapObject = GameObject.Find("Canvas/Map");
                if (mapObject != null)
                {
                    mapRect = mapObject.transform as RectTransform;
                }
            }

            if (routeParent == null)
            {
                var routesObject = GameObject.Find("Canvas/Routes") ?? GameObject.Find("Canvas/Map/Routes");
                if (routesObject != null)
                {
                    routeParent = routesObject.transform;
                }
            }

            if (cityParent == null)
            {
                var citiesObject = GameObject.Find("Canvas/Map/Cities") ?? GameObject.Find("Canvas/Cities");
                if (citiesObject != null)
                {
                    cityParent = citiesObject.transform;
                }
            }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/MapGenerator/根据路线数据生成路线物体（编辑器摆位用）")]
        private static void GenerateFromDataMenu(UnityEditor.MenuCommand command)
        {
            ((MapGenerator)command.context).GenerateRoutes();
        }

        [UnityEditor.MenuItem("CONTEXT/MapGenerator/清空所有路线物体")]
        private static void ClearRoutesMenu(UnityEditor.MenuCommand command)
        {
            ((MapGenerator)command.context).ClearRoutes();
        }
#endif

        /// <summary>
        /// 直接新建一条默认路线（不依赖 routeVisualData），
        /// 供编辑器里“边摆边加”使用。返回新路线的视图组件。
        /// </summary>
        public RouteVisual CreateBlankRoute()
        {
            AlignRouteLayer();

            if (routePrefab == null)
            {
                Debug.LogError("[MapGenerator] Route Prefab 为空，无法新建路线。");
                return null;
            }

            if (routeParent == null || routeParent is not RectTransform)
            {
                Debug.LogError("[MapGenerator] Route Parent 未就绪：请确认 Canvas 下存在 Routes 对象。");
                return null;
            }

            int index = 1;
            string id;
            do
            {
                id = $"route_{index++}";
            } while (routeParent.Find(id) != null);

            var data = new RouteVisualData
            {
                routeId = id,
                startPosition = new Vector2(250f, 900f),
                endPosition = new Vector2(550f, 900f),
                routeColor = TrainCarColor.Red,
                length = 3,
                isClaimed = false
            };

            var routeObject = Instantiate(routePrefab, routeParent);
            routeObject.name = id;

            var visual = routeObject.GetComponent<RouteVisual>();
            if (visual == null)
            {
                visual = routeObject.AddComponent<RouteVisual>();
            }

            visual.Initialize(data);
            return visual;
        }

        public void GenerateRoutes()
        {
            AlignRouteLayer();

            if (routePrefab == null)
            {
                Debug.LogError("[MapGenerator] Route Prefab 为空，无法生成路线。");
                return;
            }

            if (routeParent == null || routeParent is not RectTransform)
            {
                Debug.LogError("[MapGenerator] Route Parent 未就绪：请把 Canvas 下的 Routes 对象拖入 Route Parent。");
                return;
            }

            for (int i = 0; i < routeVisualData.Count; i++)
            {
                var data = routeVisualData[i];
                var routeObject = Instantiate(routePrefab, routeParent);
                routeObject.name = string.IsNullOrWhiteSpace(data.routeId) ? $"Route_{i + 1}" : data.routeId;

                var visual = routeObject.GetComponent<RouteVisual>();
                if (visual == null)
                {
                    visual = routeObject.AddComponent<RouteVisual>();
                }

                visual.Initialize(data);
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(routeParent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(routeParent.gameObject.scene);
#endif

            Debug.Log($"[MapGenerator] 已生成 {routeVisualData.Count} 条路线物体，可在 Scene 视图拖动端点摆位。");
        }

        public void ClearRoutes()
        {
            if (routeParent == null)
            {
                return;
            }

            for (int i = routeParent.childCount - 1; i >= 0; i--)
            {
                var child = routeParent.GetChild(i);

                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
#if UNITY_EDITOR
                    // 走 Undo 系统，误删后可以 Ctrl+Z 找回。
                    UnityEditor.Undo.DestroyObjectImmediate(child.gameObject);
#else
                    DestroyImmediate(child.gameObject);
#endif
                }
            }
        }

        // ———————————————————— 城市 ————————————————————

        public void AlignCityLayer()
        {
            AlignCityLayer(autoCreate: false);
        }

        /// <summary>
        /// 把 Cities 容器拉伸对齐到地图；autoCreate 为真且容器不存在时新建一个。
        /// （OnValidate 里绝不自动建物体，避免 Unity 报 SendMessage 警告。）
        /// </summary>
        public void AlignCityLayer(bool autoCreate)
        {
            AutoBindReferences();

            if (mapRect == null)
            {
                return;
            }

            if (cityParent == null)
            {
                if (!autoCreate)
                {
                    return;
                }

                var citiesObject = new GameObject("Cities", typeof(RectTransform));

#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(citiesObject, "创建城市层");
#endif
                citiesObject.transform.SetParent(mapRect, false);
                cityParent = citiesObject.transform;

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }

            var layer = cityParent as RectTransform;
            if (layer == null)
            {
                Debug.LogError("[MapGenerator] City Parent 不是 RectTransform，请使用 Canvas 下的 Cities 对象。");
                return;
            }

            bool changed = false;

            if (layer.parent != mapRect)
            {
                layer.SetParent(mapRect, false);
                changed = true;
            }

            if (layer.anchorMin != Vector2.zero
                || layer.anchorMax != Vector2.one
                || layer.offsetMin != Vector2.zero
                || layer.offsetMax != Vector2.zero
                || layer.localScale != Vector3.one)
            {
                layer.anchorMin = Vector2.zero;
                layer.anchorMax = Vector2.one;
                layer.pivot = new Vector2(0.5f, 0.5f);
                layer.offsetMin = Vector2.zero;
                layer.offsetMax = Vector2.zero;
                layer.sizeDelta = Vector2.zero;
                layer.localScale = Vector3.one;
                layer.localRotation = Quaternion.identity;
                changed = true;
            }

            if (changed)
            {
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(layer);
#endif
            }

            // 城市层排在 Routes 之下：端点重叠时路线点击优先，城市只在不挡路线的区域响应悬停。
            layer.SetAsFirstSibling();
        }

        /// <summary>
        /// 新建一个城市标记：优先用 City Prefab，未指定时自动从
        /// Resources/Prefabs/CityMarker 加载；再没有就纯代码建一个。
        /// </summary>
        public CityMarker CreateBlankCity()
        {
            AlignCityLayer(autoCreate: true);

            if (cityParent == null || cityParent is not RectTransform)
            {
                Debug.LogError("[MapGenerator] City Parent 未就绪：请确认 Canvas/Map 下存在 Cities 对象。");
                return null;
            }

            int index = 1;
            string id;
            do
            {
                id = $"city_{index++}";
            } while (cityParent.Find(id) != null);

            GameObject prefab = cityPrefab != null ? cityPrefab : Resources.Load<GameObject>("Prefabs/CityMarker");

            GameObject cityObject;
            CityMarker marker;

            if (prefab != null)
            {
                cityObject = Instantiate(prefab, cityParent);
                marker = cityObject.GetComponent<CityMarker>();
                if (marker == null)
                {
                    marker = cityObject.AddComponent<CityMarker>();
                }
            }
            else
            {
                cityObject = new GameObject(id, typeof(RectTransform));
                cityObject.transform.SetParent(cityParent, false);
                marker = cityObject.AddComponent<CityMarker>();
            }

            cityObject.name = id;
            marker.Initialize(id, new Vector2(MapPixelSpace.MapWidth * 0.5f, MapPixelSpace.MapHeight * 0.5f));

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(cityParent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cityObject.scene);
#endif
            return marker;
        }

        /// <summary>地图 RectTransform（批量摆点工具做射线投影用）。</summary>
        public RectTransform MapLayer
        {
            get
            {
                if (mapRect == null)
                {
                    AlignRouteLayer();
                }

                return mapRect;
            }
        }

        /// <summary>
        /// 在指定 Map.png 像素位置创建城市；同名城市已存在则只更新位置。
        /// 供批量摆点窗口逐城点击放置。
        /// </summary>
        public CityMarker CreateCityAt(string cityId, Vector2 pixel)
        {
            AlignCityLayer(autoCreate: true);

            if (cityParent is not RectTransform)
            {
                Debug.LogError("[MapGenerator] City Parent 未就绪。");
                return null;
            }

            if (string.IsNullOrWhiteSpace(cityId))
            {
                return null;
            }

            cityId = cityId.Trim();
            var existing = FindCity(cityId);
            if (existing != null)
            {
                existing.SetPixel(pixel);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(existing);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
#endif
                return existing;
            }

            GameObject prefab = cityPrefab != null ? cityPrefab : Resources.Load<GameObject>("Prefabs/CityMarker");

            GameObject cityObject;
            CityMarker marker;

            if (prefab != null)
            {
                cityObject = Instantiate(prefab, cityParent);
                marker = cityObject.GetComponent<CityMarker>();
                if (marker == null)
                {
                    marker = cityObject.AddComponent<CityMarker>();
                }
            }
            else
            {
                cityObject = new GameObject(cityId, typeof(RectTransform));
                cityObject.transform.SetParent(cityParent, false);
                marker = cityObject.AddComponent<CityMarker>();
            }

            marker.Initialize(cityId, pixel);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(cityParent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cityObject.scene);
#endif
            return marker;
        }

        public void ClearCities()
        {
            if (cityParent == null)
            {
                return;
            }

            for (int i = cityParent.childCount - 1; i >= 0; i--)
            {
                var child = cityParent.GetChild(i);

                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
#if UNITY_EDITOR
                    UnityEditor.Undo.DestroyObjectImmediate(child.gameObject);
#else
                    DestroyImmediate(child.gameObject);
#endif
                }
            }
        }

        // —————————————— 城市 ↔ 路线联动 ——————————————

        /// <summary>返回 Cities 容器下全部城市名（按字母排序），供编辑器下拉使用。</summary>
        public List<string> GetCityIds()
        {
            var result = new List<string>();

            if (cityParent != null)
            {
                foreach (Transform child in cityParent)
                {
                    var marker = child.GetComponent<CityMarker>();
                    if (marker != null && !string.IsNullOrWhiteSpace(marker.CityId))
                    {
                        result.Add(marker.CityId);
                    }
                }
            }

            result.Sort(System.StringComparer.Ordinal);
            return result;
        }

        public CityMarker FindCity(string cityId)
        {
            if (cityParent == null || string.IsNullOrWhiteSpace(cityId))
            {
                return null;
            }

            foreach (Transform child in cityParent)
            {
                var marker = child.GetComponent<CityMarker>();
                if (marker != null && marker.CityId == cityId)
                {
                    return marker;
                }
            }

            return null;
        }

        /// <summary>
        /// 按两个城市创建一条路线，端点自动吸附到城市坐标。
        /// 平行双线会自动得到 _2、_3 后缀的唯一 id。
        /// </summary>
        public RouteVisual CreateRouteBetweenCities(
            string startCityId,
            string endCityId,
            int length,
            TrainCarColor color,
            bool isGrey = false,
            float sideOffsetPixels = 0f)
        {
            AlignRouteLayer();
            AlignCityLayer(autoCreate: true);

            var start = FindCity(startCityId);
            var end = FindCity(endCityId);

            if (start == null || end == null)
            {
                Debug.LogError($"[MapGenerator] 找不到城市：{startCityId} 或 {endCityId}，请先标注城市。");
                return null;
            }

            if (startCityId == endCityId)
            {
                Debug.LogError("[MapGenerator] 路线的两个端点不能是同一座城市。");
                return null;
            }

            if (routePrefab == null || routeParent == null)
            {
                Debug.LogError("[MapGenerator] Route Prefab 或 Route Parent 未就绪。");
                return null;
            }

            string baseId = $"route_{startCityId}_{endCityId}";
            string id = baseId;
            int suffix = 2;
            while (routeParent.Find(id) != null)
            {
                id = $"{baseId}_{suffix++}";
            }

            var data = new RouteVisualData
            {
                routeId = id,
                startPosition = start.PixelPosition,
                endPosition = end.PixelPosition,
                routeColor = color,
                length = Mathf.Clamp(length, 1, 6),
                isClaimed = false
            };

            var routeObject = Instantiate(routePrefab, routeParent);
            routeObject.name = id;

#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(routeObject, "按城市对创建路线");
#endif

            var visual = routeObject.GetComponent<RouteVisual>();
            if (visual == null)
            {
                visual = routeObject.AddComponent<RouteVisual>();
            }

            visual.Initialize(data);
            visual.BindCities(startCityId, endCityId);
            visual.SetAppearance(isGrey, sideOffsetPixels);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(routeParent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(routeObject.scene);
#endif
            return visual;
        }

        /// <summary>城市标记挪动后，让所有绑定城市的路线重新吸附。</summary>
        public void SnapAllRoutesToCities()
        {
            if (routeParent == null)
            {
                return;
            }

            foreach (Transform child in routeParent)
            {
                var visual = child.GetComponent<RouteVisual>();
                if (visual != null && visual.SnapEndpointsToCities())
                {
                    visual.Rebuild();
                }
            }
        }

        // —————————————— 官方路线表批量生成 ——————————————

        /// <summary>
        /// 按美版官方路线表（78 个城市对 / 100 条轨道）批量生成路线。
        /// 幂等：场景里已经存在的城市对自动跳过，只补缺的，可反复点。
        /// </summary>
        public OfficialRouteReport GenerateOfficialRoutes()
        {
            AlignRouteLayer();
            AlignCityLayer(autoCreate: false);

            var report = new OfficialRouteReport();

            if (routePrefab == null || routeParent == null)
            {
                report.Notes.Add("Route Prefab 或 Route Parent 未就绪，未生成任何路线。");
                return report;
            }

            float halfGap = doubleRoutePixelGap * 0.5f;

            foreach (var def in OfficialUsRouteTable.All)
            {
                if (FindCity(def.City1) == null || FindCity(def.City2) == null)
                {
                    string missing = FindCity(def.City1) == null ? def.City1 : def.City2;
                    if (!report.MissingCities.Contains(missing))
                    {
                        report.MissingCities.Add(missing);
                    }
                    continue;
                }

                var existing = FindRoutesBetweenCities(def.City1, def.City2);
                int expected = def.TrackCount;

                if (existing.Count >= expected)
                {
                    report.SkippedPairs++;
                    continue;
                }

                if (!def.IsDouble)
                {
                    // 单线（彩色或灰色）。
                    CreateRouteBetweenCities(
                        def.City1, def.City2, def.Length,
                        def.IsGrey ? TrainCarColor.Red : def.Color,
                        def.IsGrey, 0f);
                    report.CreatedTracks++;
                    continue;
                }

                // 平行双线：逐轨补齐，左右各偏移半轨距，颜色按官方表分配。
                for (int track = 0; track < 2; track++)
                {
                    float offset = track == 0 ? -halfGap : halfGap;

                    // 若这一对已经有 1 条（部分补建），跳过已占用的偏移侧。
                    if (existing.Count > 0)
                    {
                        bool sideTaken = false;
                        int takenColorSlots = 0;
                        foreach (var v in existing)
                        {
                            if (Mathf.Abs(v.SideOffsetPixels - offset) < 0.5f)
                            {
                                sideTaken = true;
                            }

                            if (!def.IsGrey && v.RouteColor == def.Color)
                            {
                                takenColorSlots++;
                            }
                        }

                        if (sideTaken)
                        {
                            continue;
                        }

                        // 彩色双线补第二条时选没被用过的颜色。
                        if (!def.IsGrey)
                        {
                            TrainCarColor needed = takenColorSlots == 0 ? def.Color : def.Color2;
                            CreateRouteBetweenCities(def.City1, def.City2, def.Length, needed, false, offset);
                            report.CreatedTracks++;
                            continue;
                        }
                    }

                    TrainCarColor color = def.IsGrey
                        ? TrainCarColor.Red
                        : def.GetTrackColor(track);

                    CreateRouteBetweenCities(
                        def.City1, def.City2, def.Length, color,
                        def.IsGrey, offset);
                    report.CreatedTracks++;
                }
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(routeParent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(routeParent.gameObject.scene);
#endif

            return report;
        }

        /// <summary>
        /// 只核对不生成：统计场景里相对官方表还缺哪些城市对/轨道，
        /// 以及哪些官方城市没摆。
        /// </summary>
        public OfficialRouteReport AuditOfficialRoutes()
        {
            var report = new OfficialRouteReport();

            foreach (var def in OfficialUsRouteTable.All)
            {
                bool city1Missing = FindCity(def.City1) == null;
                bool city2Missing = FindCity(def.City2) == null;

                if (city1Missing && !report.MissingCities.Contains(def.City1))
                {
                    report.MissingCities.Add(def.City1);
                }

                if (city2Missing && !report.MissingCities.Contains(def.City2))
                {
                    report.MissingCities.Add(def.City2);
                }

                if (city1Missing || city2Missing)
                {
                    continue;
                }

                int count = FindRoutesBetweenCities(def.City1, def.City2).Count;
                int expected = def.TrackCount;

                if (count >= expected)
                {
                    report.SkippedPairs++;
                }
                else
                {
                    string colorText = def.IsGrey
                        ? "灰"
                        : (def.IsDouble ? $"{def.Color}/{def.Color2}" : def.Color.ToString());
                    report.MissingPairs.Add(
                        $"{def.City1} — {def.City2}（{def.Length} 节，{colorText}，" +
                        $"缺 {expected - count}/{expected} 条）");
                }
            }

            return report;
        }

        /// <summary>找出场景里绑定在指定两个城市之间（方向不分先后）的全部路线。</summary>
        private List<RouteVisual> FindRoutesBetweenCities(string city1, string city2)
        {
            var result = new List<RouteVisual>();

            if (routeParent == null)
            {
                return result;
            }

            foreach (Transform child in routeParent)
            {
                var visual = child.GetComponent<RouteVisual>();
                if (visual == null)
                {
                    continue;
                }

                bool forward = visual.StartCityId == city1 && visual.EndCityId == city2;
                bool reverse = visual.StartCityId == city2 && visual.EndCityId == city1;

                if (forward || reverse)
                {
                    result.Add(visual);
                }
            }

            return result;
        }
    }

    /// <summary>官方路线批量生成/核对的结果报告。</summary>
    public class OfficialRouteReport
    {
        /// <summary>本次新建的轨道条数。</summary>
        public int CreatedTracks;

        /// <summary>已存在而跳过的城市对数。</summary>
        public int SkippedPairs;

        /// <summary>官方城市中场景里没找到的（摆点遗漏）。</summary>
        public List<string> MissingCities = new List<string>();

        /// <summary>核对时发现缺失的城市对描述（仅 Audit 使用）。</summary>
        public List<string> MissingPairs = new List<string>();

        /// <summary>其它提示/错误。</summary>
        public List<string> Notes = new List<string>();
    }

    [System.Serializable]
    public class RouteVisualData
    {
        [Tooltip("Map.png 像素坐标，左下角原点（宽 1744、高 1125）。")]
        public string routeId;
        public Vector2 startPosition;
        public Vector2 endPosition;
        public TrainCarColor routeColor;
        [Tooltip("灰色万能路线：任意同色车厢卡均可认领。")]
        public bool isGreyRoute;
        [Tooltip("平行双线垂直平移量（Map.png 像素单位）。")]
        public float sideOffsetPixels;
        [Range(1, 6)] public int length = 1;
        public bool isClaimed;
    }
}
