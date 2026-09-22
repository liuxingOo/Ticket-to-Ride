using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TicketToRide.Domain;

namespace TicketToRide.Presentation.EditorTools
{
    /// <summary>
    /// 选两个城市 + 长度 + 颜色，一键生成端点吸附到城市的路线。
    /// 从 MapGenerator 检查器的“按城市对新建路线…”按钮打开。
    /// </summary>
    public class RouteCreateWindow : EditorWindow
    {
        private static readonly TrainCarColor[] RouteColors =
        {
            TrainCarColor.Red,
            TrainCarColor.Orange,
            TrainCarColor.Yellow,
            TrainCarColor.Green,
            TrainCarColor.Blue,
            TrainCarColor.Purple,
            TrainCarColor.Black,
            TrainCarColor.White
        };

        private static readonly string[] RouteColorLabels =
        {
            "红色", "橙色", "黄色", "绿色", "蓝色", "紫色", "黑色", "白色"
        };

        private MapGenerator generator;
        private List<string> cityIds = new List<string>();

        private int startIndex;
        private int endIndex = 1;
        private int length = 3;
        private int colorIndex;

        public static void Open(MapGenerator generator)
        {
            var window = GetWindow<RouteCreateWindow>(true, "按城市对新建路线");
            window.generator = generator;
            window.RefreshCities();
            window.minSize = new Vector2(320f, 200f);
            window.ShowUtility();
        }

        private void OnFocus()
        {
            RefreshCities();
        }

        private void RefreshCities()
        {
            if (generator == null)
            {
                return;
            }

            cityIds = generator.GetCityIds();
            startIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, cityIds.Count - 1));
            endIndex = Mathf.Clamp(endIndex, 0, Mathf.Max(0, cityIds.Count - 1));
        }

        private void OnGUI()
        {
            if (generator == null)
            {
                EditorGUILayout.HelpBox("MapGenerator 已丢失，请重新从检查器打开。", MessageType.Warning);
                return;
            }

            if (cityIds.Count < 2)
            {
                EditorGUILayout.HelpBox(
                    "当前至少需要标注 2 个城市。\n请先用“城市标注工具”新增城市并改好 City Id。",
                    MessageType.Warning);

                if (GUILayout.Button("刷新"))
                {
                    RefreshCities();
                }

                return;
            }

            EditorGUILayout.Space(6f);

            startIndex = EditorGUILayout.Popup("起点城市", startIndex, cityIds.ToArray());
            endIndex = EditorGUILayout.Popup("终点城市", endIndex, cityIds.ToArray());
            length = EditorGUILayout.IntSlider("路线长度（车厢数）", length, 1, 6);
            colorIndex = EditorGUILayout.Popup("路线颜色", colorIndex, RouteColorLabels);

            EditorGUILayout.Space(6f);

            if (startIndex == endIndex)
            {
                EditorGUILayout.HelpBox("起点和终点不能是同一座城市。", MessageType.Error);
            }

            using (new EditorGUI.DisabledScope(startIndex == endIndex))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("创建", GUILayout.Height(26f)))
                {
                    CreateRoute();
                    Close();
                }

                if (GUILayout.Button("创建并继续", GUILayout.Height(26f)))
                {
                    CreateRoute();
                    RefreshCities();
                }
            }

            if (GUILayout.Button("取消"))
            {
                Close();
            }
        }

        private void CreateRoute()
        {
            string startId = cityIds[startIndex];
            string endId = cityIds[endIndex];

            var visual = generator.CreateRouteBetweenCities(
                startId,
                endId,
                length,
                RouteColors[colorIndex]);

            if (visual != null)
            {
                Undo.RegisterCreatedObjectUndo(visual.gameObject, "按城市对新建路线");
                Selection.activeGameObject = visual.gameObject;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(visual.gameObject.scene);
            }
        }
    }
}
