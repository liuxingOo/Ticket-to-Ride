using UnityEditor;
using UnityEngine;

namespace TicketToRide.Presentation.EditorTools
{
    /// <summary>
    /// 在 Scene 视图为路线提供两个可拖拽的端点手柄，
    /// 拖动结果直接写回 Map.png 像素坐标，摆位时所见即所得。
    /// </summary>
    [CustomEditor(typeof(RouteVisual))]
    public class RouteVisualEditor : Editor
    {
        private void OnSceneGUI()
        {
            var route = (RouteVisual)target;
            var layer = route.transform.parent as RectTransform;

            if (layer == null)
            {
                return;
            }

            DrawEndpointHandle(route, layer, route.StartPixel, "起点", true);
            DrawEndpointHandle(route, layer, route.EndPixel, "终点", false);
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
