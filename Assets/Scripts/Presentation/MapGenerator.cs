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
        public RouteVisual CreateRouteBetweenCities(string startCityId, string endCityId, int length, TrainCarColor color)
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

            var visual = routeObject.GetComponent<RouteVisual>();
            if (visual == null)
            {
                visual = routeObject.AddComponent<RouteVisual>();
            }

            visual.Initialize(data);
            visual.BindCities(startCityId, endCityId);

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
    }

    [System.Serializable]
    public class RouteVisualData
    {
        [Tooltip("Map.png 像素坐标，左下角原点（宽 1744、高 1125）。")]
        public string routeId;
        public Vector2 startPosition;
        public Vector2 endPosition;
        public TrainCarColor routeColor;
        [Range(1, 6)] public int length = 1;
        public bool isClaimed;
    }
}
