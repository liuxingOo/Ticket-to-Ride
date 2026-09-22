using UnityEditor;
using UnityEngine;

namespace TicketToRide.Presentation.EditorTools
{
    /// <summary>
    /// 给 MapGenerator 的 Inspector 加上直观的操作按钮，
    /// 摆位工作流：新增路线 → Scene 视图拖端点 → 改长度/颜色 → 保存场景。
    /// </summary>
    [CustomEditor(typeof(MapGenerator))]
    public class MapGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var generator = (MapGenerator)target;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("路线摆位工具", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "1. 点“新增一条路线”\n" +
                "2. 到 Scene 视图拖绿色/红色手柄对齐地图轨道\n" +
                "3. 在路线物体的 Inspector 调整长度、颜色、厚度\n" +
                "4. Ctrl+S 保存场景",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!UnityEngine.Application.isEditor))
            {
                if (GUILayout.Button("＋ 新增一条路线", GUILayout.Height(28f)))
                {
                    var visual = generator.CreateBlankRoute();
                    if (visual != null)
                    {
                        Undo.RegisterCreatedObjectUndo(visual.gameObject, "新增路线");
                        Selection.activeGameObject = visual.gameObject;
                        EditorSceneManagerMarkDirty(visual.gameObject);
                    }
                }

                EditorGUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("按城市对新建路线…"))
                    {
                        RouteCreateWindow.Open(generator);
                    }

                    if (GUILayout.Button("路线重新吸附城市"))
                    {
                        generator.SnapAllRoutesToCities();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("按下方数据列表生成"))
                    {
                        generator.GenerateRoutes();
                    }

                    if (GUILayout.Button("清空全部路线"))
                    {
                        bool confirm = EditorUtility.DisplayDialog(
                            "清空路线",
                            "确定删除 Routes 容器下的全部路线物体吗？此操作可 Ctrl+Z 撤销。",
                            "清空",
                            "取消");

                        if (confirm)
                        {
                            generator.ClearRoutes();
                        }
                    }
                }

                if (GUILayout.Button("重新对齐路线层到地图"))
                {
                    generator.AlignRouteLayer();
                }
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("城市标注工具", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "1. 点“新增一个城市”，会自动创建 Cities 容器\n" +
                "2. 把 City Id 改成城市英文名（与地图/任务卡一致，如 Vancouver）\n" +
                "3. 在 Scene 视图拖黄色手柄对准灰色城市圆点\n" +
                "4. 重复标注全部城市，最后 Ctrl+S 保存场景",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!UnityEngine.Application.isEditor))
            {
                if (GUILayout.Button("＋ 新增一个城市", GUILayout.Height(28f)))
                {
                    var marker = generator.CreateBlankCity();
                    if (marker != null)
                    {
                        Undo.RegisterCreatedObjectUndo(marker.gameObject, "新增城市");
                        Selection.activeGameObject = marker.gameObject;
                        EditorSceneManagerMarkDirty(marker.gameObject);
                    }
                }

                EditorGUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("创建/对齐城市层"))
                    {
                        generator.AlignCityLayer(autoCreate: true);
                    }

                    if (GUILayout.Button("清空全部城市"))
                    {
                        bool confirm = EditorUtility.DisplayDialog(
                            "清空城市",
                            "确定删除 Cities 容器下的全部城市标记吗？此操作可 Ctrl+Z 撤销。",
                            "清空",
                            "取消");

                        if (confirm)
                        {
                            generator.ClearCities();
                        }
                    }
                }
            }
        }

        private static void EditorSceneManagerMarkDirty(GameObject gameObject)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }
}
