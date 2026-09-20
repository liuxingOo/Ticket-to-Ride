using System.Collections.Generic;
using UnityEngine;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    public class MapGenerator : MonoBehaviour
    {
        [SerializeField] private GameObject routePrefab;
        [SerializeField] private Transform routeParent;
        [SerializeField] private List<RouteVisualData> routeVisualData = new List<RouteVisualData>();

        private readonly List<RouteVisual> _spawnedRoutes = new List<RouteVisual>();

        private void Start()
        {
            Debug.Log($"[MapGenerator] Start 执行。预制体为空: {routePrefab == null}，父对象为空: {routeParent == null}，路线数量: {routeVisualData.Count}");
            GenerateRoutes();
        }

        public void GenerateRoutes()
        {
            Debug.Log("[MapGenerator] GenerateRoutes 开始执行。");
            ClearRoutes();

            if (routePrefab == null)
            {
                Debug.LogError("[MapGenerator] Route Prefab 为空，无法生成路线。");
                return;
            }

            if (routeParent == null)
            {
                Debug.LogError("[MapGenerator] Route Parent 为空。UI 路线必须挂在 Canvas 下的 Routes 对象中，已停止生成。");
                return;
            }

            if (routeParent is not RectTransform)
            {
                Debug.LogError("[MapGenerator] Route Parent 不是 RectTransform。请把 Canvas 下的 Routes 对象拖入 Route Parent。");
                return;
            }

            foreach (var data in routeVisualData)
            {
                var routeObject = Instantiate(routePrefab, routeParent);
                Debug.Log($"[MapGenerator] 已生成实例: {routeObject.name}，路线: {data.routeId}");
                var visual = routeObject.GetComponent<RouteVisual>();

                if (visual == null)
                {
                    visual = routeObject.AddComponent<RouteVisual>();
                }

                visual.Initialize(data);
                _spawnedRoutes.Add(visual);
            }
        }

        public void ClearRoutes()
        {
            for (int i = _spawnedRoutes.Count - 1; i >= 0; i--)
            {
                if (_spawnedRoutes[i] != null)
                {
                    Destroy(_spawnedRoutes[i].gameObject);
                }
            }

            _spawnedRoutes.Clear();
        }
    }

    [System.Serializable]
    public class RouteVisualData
    {
        public string routeId;
        public Vector2 startPosition;
        public Vector2 endPosition;
        public TrainCarColor routeColor;
        public int length;
        public bool isClaimed;
    }
}
