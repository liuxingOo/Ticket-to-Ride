using UnityEditor;
using UnityEngine;

namespace TicketToRide.Presentation.EditorTools
{
    /// <summary>
    /// 在 Scene 视图为路线提供两个可拖拽的端点手柄，
    /// 拖动结果直接写回 Map.png 像素坐标，摆位时所见即所得。
    /// 只在对应端点没有绑定城市时出现：绑定了城市的路线每次 Rebuild 都会被
    /// SnapEndpointsToCities 覆盖回城市坐标，拖手柄不会有任何效果。
    /// </summary>
    [CustomEditor(typeof(RouteVisual))]
    public class RouteVisualEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var route = (RouteVisual)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("折线顶点（手动修形）", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("当前顶点数", route.PathPointCount.ToString());

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("由曲线生成初始顶点"))
                {
                    Undo.RecordObject(route, "生成初始折线顶点");
                    route.InitializePathPointFromCurve();
                    EditorUtility.SetDirty(route);
                }

                if (GUILayout.Button("在弧长中点加顶点"))
                {
                    Undo.RecordObject(route, "插入折线顶点");
                    route.InsertPathPointAtMiddle();
                    EditorUtility.SetDirty(route);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(route.PathPointCount == 0))
                {
                    if (GUILayout.Button("删除最后一个顶点"))
                    {
                        Undo.RecordObject(route, "删除折线顶点");
                        route.RemovePathPoint(route.PathPointCount - 1);
                        EditorUtility.SetDirty(route);
                    }

                    if (GUILayout.Button("清空顶点（回到曲线/直线）"))
                    {
                        Undo.RecordObject(route, "清空折线顶点");
                        route.ClearPathPoints();
                        EditorUtility.SetDirty(route);
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "直线路段不用加顶点。弯道先点『由曲线生成初始顶点』拿到 1 个起点，\n" +
                "再到 Scene 视图拖黄色球微调（两端锁定城市，不用动）。\n" +
                "『length』就是节数，直接改即可；车厢/热区/高亮大小全图统一，不受节数影响。\n" +
                "若印刷轨道与城市之间有间隙：取消勾选『Lock Endpoints To Cities』，\n" +
                "再拖绿/红端点手柄贴到印刷轨道的实际起止处。",
                MessageType.Info);

            DrawCarAngleOverrides(route);
        }

        /// <summary>
        /// 逐节车厢角度：默认每节按所在折线段自动取角度帧；
        /// 贴不上的那一节可以单独掰成别的角度帧（只改朝向，位置仍沿折线）。
        /// </summary>
        private void DrawCarAngleOverrides(RouteVisual route)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("逐节车厢角度（单独掰某一节）", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"共 {route.Length} 节；『自动』= 按折线所在段取角度。只改角度，位置不动。",
                EditorStyles.miniLabel);

            for (int i = 0; i < route.Length; i++)
            {
                int current = route.GetCarAngleOverride(i);
                bool isAuto = current < 0;
                int frame = isAuto ? 0 : current;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"第 {i + 1} 节", GUILayout.Width(52f));

                    bool newAuto = GUILayout.Toggle(isAuto, "自动", EditorStyles.miniButton, GUILayout.Width(44f));
                    if (newAuto != isAuto)
                    {
                        Undo.RecordObject(route, "切换车厢角度自动");
                        route.SetCarAngleOverride(i, newAuto ? -1 : frame);
                        EditorUtility.SetDirty(route);
                    }

                    int newFrame;
                    using (new EditorGUI.DisabledScope(isAuto))
                    {
                        newFrame = EditorGUILayout.IntSlider(frame, 0, WagonSpriteMapper.FrameCount - 1);
                    }

                    GUILayout.Label(
                        isAuto ? "（自动）" : $"{frame * 5f:F0}°",
                        EditorStyles.miniLabel,
                        GUILayout.Width(52f));

                    if (!isAuto && newFrame != frame)
                    {
                        Undo.RecordObject(route, "设置车厢角度");
                        route.SetCarAngleOverride(i, newFrame);
                        EditorUtility.SetDirty(route);
                    }
                }
            }

            using (new EditorGUI.DisabledScope(!route.HasAnyCarAngleOverride))
            {
                if (GUILayout.Button("全部恢复自动", GUILayout.Width(140f)))
                {
                    Undo.RecordObject(route, "清除逐节车厢角度");
                    route.ClearCarAngleOverrides();
                    EditorUtility.SetDirty(route);
                }
            }
        }

        private void OnSceneGUI()
        {
            var route = (RouteVisual)target;
            var layer = route.transform.parent as RectTransform;

            if (layer == null)
            {
                return;
            }

            if (!route.LockEndpointsToCities)
            {
                // 端点已解锁：无论是否绑定城市，都显示可拖的起止手柄。
                DrawEndpointHandle(route, layer, route.StartPixel, "起点（可拖）", true);
                DrawEndpointHandle(route, layer, route.EndPixel, "终点（可拖）", false);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(route.StartCityId))
                {
                    DrawEndpointHandle(route, layer, route.StartPixel, "起点", true);
                }

                if (string.IsNullOrWhiteSpace(route.EndCityId))
                {
                    DrawEndpointHandle(route, layer, route.EndPixel, "终点", false);
                }
            }

            DrawVertexHandles(route, layer);
        }

        /// <summary>折线内部顶点手柄：自由拖动，写回 Map.png 像素坐标。</summary>
        private void DrawVertexHandles(RouteVisual route, RectTransform layer)
        {
            for (int i = 0; i < route.PathPointCount; i++)
            {
                Vector2 pixel = route.GetPathPoint(i);
                Vector3 world = layer.TransformPoint(route.PixelToLocal(pixel));

                Handles.color = new Color(1f, 0.75f, 0.15f);
                Handles.Label(world + new Vector3(0f, 22f, 0f), $"顶点 {i + 1}");

                float size = HandleUtility.GetHandleSize(world) * 0.16f;

                EditorGUI.BeginChangeCheck();

                Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
                moved.z = world.z;

                if (!EditorGUI.EndChangeCheck())
                {
                    continue;
                }

                Vector3 local = layer.InverseTransformPoint(moved);
                Vector2 newPixel = ClampToMap(route.LocalToPixel(local));

                Undo.RecordObject(route, "移动折线顶点");
                route.SetPathPoint(i, newPixel);
                EditorUtility.SetDirty(route);
            }
        }

        private static Vector2 ClampToMap(Vector2 pixel)
        {
            pixel.x = Mathf.Clamp(pixel.x, 0f, RouteVisual.MapTextureWidth);
            pixel.y = Mathf.Clamp(pixel.y, 0f, RouteVisual.MapTextureHeight);
            return pixel;
        }

        private void DrawEndpointHandle(RouteVisual route, RectTransform layer, Vector2 pixel, string label, bool isStart)
        {
            Vector3 worldPosition = layer.TransformPoint(route.PixelToLocal(pixel));

            Handles.color = isStart ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.9f, 0.35f, 0.3f);
            Handles.Label(worldPosition + new Vector3(0f, 24f, 0f), label);

            EditorGUI.BeginChangeCheck();

            Vector3 moved = Handles.PositionHandle(worldPosition, Quaternion.identity);
            moved.z = worldPosition.z; // UI 在平面内移动，不允许拖出 Z 轴。

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Vector3 localPosition = layer.InverseTransformPoint(moved);
            Vector2 newPixel = route.LocalToPixel(localPosition);

            // 钳制在地图像素范围内。
            newPixel.x = Mathf.Clamp(newPixel.x, 0f, RouteVisual.MapTextureWidth);
            newPixel.y = Mathf.Clamp(newPixel.y, 0f, RouteVisual.MapTextureHeight);

            Undo.RecordObject(route, "移动路线端点");

            if (isStart)
            {
                route.SetStartPixel(newPixel);
            }
            else
            {
                route.SetEndPixel(newPixel);
            }

            EditorUtility.SetDirty(route);
        }
    }
}
