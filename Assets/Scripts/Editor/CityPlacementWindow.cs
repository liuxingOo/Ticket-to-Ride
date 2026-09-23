using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TicketToRide.Presentation.EditorTools
{
    /// <summary>
    /// 城市批量摆点：按地理顺序逐个提示城市名，在 Scene 视图地图上
    /// 左键点击灰色城市圆点即完成一个，自动切到下一个。
    /// 从 MapGenerator 检查器的“批量摆点标注 36 城…”按钮打开。
    /// </summary>
    public class CityPlacementWindow : EditorWindow
    {
        // 按地图大致“西北 → 东 → 南”的蛇形顺序排列，方便在图上找。
        private static readonly string[] OrderedCities =
        {
            "Vancouver", "Calgary", "Winnipeg",
            "Seattle", "Helena", "Duluth",
            "Portland", "Salt Lake City", "Denver", "Omaha",
            "Chicago", "Sault St. Marie", "Toronto", "Montreal", "Boston",
            "San Francisco", "Las Vegas", "Santa Fe", "Kansas City",
            "Saint Louis", "Pittsburgh", "New York", "Washington",
            "Los Angeles", "Phoenix", "El Paso", "Oklahoma City",
            "Dallas", "Little Rock", "Nashville", "Raleigh",
            "Atlanta", "Charleston",
            "Houston", "New Orleans", "Miami"
        };

        private MapGenerator generator;
        private readonly List<string> remaining = new List<string>();
        private readonly List<string> placedThisSession = new List<string>();
        private Vector2 checklistScroll;
        private bool active;

        public static void Open(MapGenerator generator)
        {
            var window = GetWindow<CityPlacementWindow>(true, "批量摆点标注城市");
            window.generator = generator;
            window.minSize = new Vector2(300f, 420f);
            window.Rescan();
            window.ShowUtility();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            active = false;
        }

        private void Rescan()
        {
            remaining.Clear();
            foreach (var city in OrderedCities)
            {
                if (generator == null || generator.FindCity(city) == null)
                {
                    remaining.Add(city);
                }
            }
        }

        private void OnGUI()
        {
            if (generator == null)
            {
                EditorGUILayout.HelpBox("MapGenerator 已丢失，请重新从检查器打开。", MessageType.Warning);
                return;
            }

            int done = OrderedCities.Length - remaining.Count;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"进度：{done} / {OrderedCities.Length}", EditorStyles.boldLabel);

            if (remaining.Count == 0)
            {
                EditorGUILayout.HelpBox("36 城已全部标注完成，记得 Ctrl+S 保存场景。", MessageType.Info);
                active = false;

                if (GUILayout.Button("重新扫描"))
                {
                    Rescan();
                }

                return;
            }

            string current = remaining[0];
            string chinese = CityNames.TryGet(current, out var zh) ? zh : "";

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("下一个城市", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(current, EditorStyles.whiteLargeLabel);
                EditorGUILayout.LabelField(chinese, EditorStyles.whiteLargeLabel);
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = active ? new Color(1f, 0.7f, 0.4f) : new Color(0.5f, 0.9f, 0.5f);
                if (GUILayout.Button(active ? "暂停摆点" : "开始摆点", GUILayout.Height(28f)))
                {
                    active = !active;
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = oldColor;

                if (GUILayout.Button("跳过该城", GUILayout.Height(28f)))
                {
                    SkipCurrent();
                }
            }

            using (new EditorGUI.DisabledScope(placedThisSession.Count == 0))
            {
                if (GUILayout.Button("撤销上一个（删除并回退）"))
                {
                    UndoLast();
                }
            }

            if (GUILayout.Button("重新扫描（不影响已摆放）"))
            {
                Rescan();
            }

            EditorGUILayout.HelpBox(
                "摆点模式下：\n" +
                "• 在场景视图地图上对灰色圆点按左键 → 放置并自动下一个\n" +
                "• 中键拖拽平移、Alt+左键旋转视角仍然可用\n" +
                "• 放偏了：点“撤销上一个”，或之后拖黄色手柄微调\n" +
                "• 完成后务必 Ctrl+S 保存",
                MessageType.None);

            EditorGUILayout.LabelField("待标注列表", EditorStyles.boldLabel);

            checklistScroll = EditorGUILayout.BeginScrollView(checklistScroll);
            for (int i = 0; i < remaining.Count; i++)
            {
                string id = remaining[i];
                string label = CityNames.TryGet(id, out var cn) ? $"{id}  ({cn})" : id;

                var oldBg = GUI.backgroundColor;
                if (i == 0)
                {
                    GUI.backgroundColor = new Color(1f, 0.92f, 0.4f);
                    EditorGUILayout.LabelField("▶ " + label, EditorStyles.helpBox);
                }
                else
                {
                    EditorGUILayout.LabelField("  " + label);
                }
                GUI.backgroundColor = oldBg;
            }
            EditorGUILayout.EndScrollView();
        }

        private void SkipCurrent()
        {
            if (remaining.Count > 0)
            {
                // 跳过 = 暂时排到队尾，最后再回来标。
                string id = remaining[0];
                remaining.RemoveAt(0);
                remaining.Add(id);
                Repaint();
            }
        }

        private void UndoLast()
        {
            if (placedThisSession.Count == 0)
            {
                return;
            }

            string id = placedThisSession[placedThisSession.Count - 1];
            placedThisSession.RemoveAt(placedThisSession.Count - 1);

            var marker = generator.FindCity(id);
            if (marker != null)
            {
                Undo.DestroyObjectImmediate(marker.gameObject);
            }

            remaining.Insert(0, id);
            Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!active || generator == null || remaining.Count == 0)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                return;
            }

            var mapRect = generator.MapLayer;
            if (mapRect == null)
            {
                return;
            }

            Event e = Event.current;

            // 拦截空白处的左键，避免摆点时误选场景物体。
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            string current = remaining[0];

            // 鼠标位置 → 世界射线 → 与地图平面求交。
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane mapPlane = new Plane(mapRect.rotation * Vector3.forward, mapRect.position);

            if (!mapPlane.Raycast(ray, out float enter))
            {
                return;
            }

            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 localPoint = mapRect.InverseTransformPoint(worldPoint);
            Vector2 pixel = MapPixelSpace.LocalToPixel(mapRect, localPoint);

            bool inside = pixel.x is >= 0f and <= MapPixelSpace.MapWidth
                         && pixel.y is >= 0f and <= MapPixelSpace.MapHeight;

            string chinese = CityNames.TryGet(current, out var zh) ? zh : "";
            Handles.color = inside ? new Color(1f, 0.9f, 0.2f, 0.9f) : new Color(1f, 0.4f, 0.4f, 0.7f);
            Handles.DrawWireDisc(worldPoint, mapRect.rotation * Vector3.forward, 14f);
            Handles.Label(
                worldPoint + mapRect.rotation * new Vector3(18f, 18f, 0f),
                $"▶ {current}  {chinese}");

            if (!inside)
            {
                return;
            }

            // Alt 组合留给视角操作；右键/中键不拦截。
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                PlaceCity(current, pixel);
                e.Use();
            }
        }

        private void PlaceCity(string cityId, Vector2 pixel)
        {
            var marker = generator.CreateCityAt(cityId, pixel);
            if (marker == null)
            {
                return;
            }

            Undo.RegisterCreatedObjectUndo(marker.gameObject, "批量摆放城市");
            placedThisSession.Add(cityId);
            remaining.RemoveAt(0);
            Repaint();

            if (remaining.Count == 0)
            {
                active = false;
                SceneView.RepaintAll();
                ShowNotification(new GUIContent("36 城全部标注完成，Ctrl+S 保存场景！"));
            }
        }
    }
}
