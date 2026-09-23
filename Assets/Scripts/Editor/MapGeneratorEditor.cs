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

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("官方路线（美版 100 条轨道 / 78 个城市对）", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "城市 36 个全部摆好后：\n" +
                "1. 点“一键生成官方路线”，城市之间会自动出现半透明青色热区（双线自动左右分开）\n" +
                "2. 可反复点，已存在的城市对会跳过，只补缺的\n" +
                "3. 点“核对缺漏”检查是否 100 条齐全\n" +
                "4. 双轨热区没对准印刷轨道时，调上方 MapGenerator 的“Double Route Pixel Gap”后清空重建",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!UnityEngine.Application.isEditor))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("一键生成官方路线", GUILayout.Height(30f)))
                {
                    var report = generator.GenerateOfficialRoutes();

                    foreach (var note in report.Notes)
                    {
                        Debug.LogWarning($"[官方路线] {note}");
                    }

                    if (report.MissingCities.Count > 0)
                    {
                        Debug.LogWarning(
                            "[官方路线] 以下城市在场景中找不到，相关路线未生成：\n" +
                            string.Join("、", report.MissingCities));
                    }

                    Debug.Log(
                        $"[官方路线] 新建 {report.CreatedTracks} 条轨道，" +
                        $"跳过已存在的 {report.SkippedPairs} 个城市对。");

                    EditorUtility.DisplayDialog(
                        "官方路线生成完成",
                        $"新建 {report.CreatedTracks} 条轨道\n" +
                        $"跳过已存在城市对 {report.SkippedPairs} 个\n" +
                        (report.MissingCities.Count > 0
                            ? $"⚠ {report.MissingCities.Count} 个城市缺失（见 Console）"
                            : "36 城全部识别成功"),
                        "好");
                }

                if (GUILayout.Button("核对缺漏", GUILayout.Height(30f)))
                {
                    var report = generator.AuditOfficialRoutes();

                    foreach (var pair in report.MissingPairs)
                    {
                        Debug.LogWarning($"[官方路线缺漏] {pair}");
                    }

                    if (report.MissingCities.Count > 0)
                    {
                        Debug.LogWarning(
                            "[官方路线] 场景中缺失城市：\n" +
                            string.Join("、", report.MissingCities));
                    }

                    // SkippedPairs 表示已集齐的城市对数。
                    Debug.Log($"[官方路线核对] 已集齐 {report.SkippedPairs}/78 个城市对。");

                    EditorUtility.DisplayDialog(
                        "官方路线核对",
                        $"已集齐城市对：{report.SkippedPairs}/78\n" +
                        $"缺失城市对：{report.MissingPairs.Count}\n" +
                        (report.MissingCities.Count > 0
                            ? $"缺失城市：{report.MissingCities.Count} 个\n"
                            : "") +
                        "详细清单见 Console",
                        "好");
                }
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("城市标注工具", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "推荐：点“批量摆点标注 36 城…”，按提示在地图上逐个左键点击即可。\n" +
                "也可点“新增一个城市”手动创建，改 City Id 后拖黄色手柄对准灰色圆点。\n" +
                "完成后 Ctrl+S 保存场景。",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!UnityEngine.Application.isEditor))
            {
                if (GUILayout.Button("批量摆点标注 36 城…", GUILayout.Height(30f)))
                {
                    generator.AlignCityLayer(autoCreate: true);
                    CityPlacementWindow.Open(generator);
                }

                EditorGUILayout.Space(4f);

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
