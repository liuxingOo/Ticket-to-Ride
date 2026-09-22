using UnityEditor;
using UnityEngine;

namespace TicketToRide.Presentation.EditorTools
{
    /// <summary>
    /// 城市标记的 Scene 视图工具：
    /// 选中城市时显示黄色 PositionHandle 和城市名标签；
    /// 未选中的城市也常驻一个黄色小圈，方便整体核对。
    /// </summary>
    [CustomEditor(typeof(CityMarker))]
    public class CityMarkerEditor : Editor
    {
        private static readonly Color HandleColor = new Color(1f, 0.82f, 0.15f, 1f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.HelpBox(
                "城市英文名必须与任务卡、路线数据中的名字完全一致。\n" +
                "在 Scene 视图拖动黄色手柄，把标记中心对准地图上的灰色城市圆点。",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            var city = (CityMarker)target;
            var layer = city.transform.parent as RectTransform;

            if (layer == null)
            {
                return;
            }

            Vector3 worldPosition = layer.TransformPoint(city.PixelToLocal(city.PixelPosition));

            Handles.color = HandleColor;
            Handles.DrawSolidDisc(worldPosition, Vector3.forward, 7f);
            Handles.Label(worldPosition + new Vector3(14f, -18f, 0f), city.CityId);

            EditorGUI.BeginChangeCheck();

            Vector3 moved = Handles.PositionHandle(worldPosition, Quaternion.identity);
            moved.z = worldPosition.z; // UI 平面内移动，锁死 Z 轴。

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Vector3 localPosition = layer.InverseTransformPoint(moved);
            Vector2 newPixel = city.LocalToPixel(localPosition);

            newPixel.x = Mathf.Clamp(newPixel.x, 0f, MapPixelSpace.MapWidth);
            newPixel.y = Mathf.Clamp(newPixel.y, 0f, MapPixelSpace.MapHeight);

            Undo.RecordObject(city, "移动城市标记");
            city.SetPixel(newPixel);
            EditorUtility.SetDirty(city);
        }

        /// <summary>所有城市（含未选中）在 Scene 视图常驻一个黄色圈。</summary>
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        private static void DrawCityGizmo(CityMarker city, GizmoType gizmoType)
        {
            var rect = city.transform as RectTransform;

            if (rect == null)
            {
                return;
            }

            Gizmos.color = HandleColor;
            Gizmos.DrawWireSphere(rect.position, 8f);
        }
    }
}
