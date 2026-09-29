using UnityEngine;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 车厢素材库（场景永久组件）：按玩家颜色 × 角度帧提供预渲染的车厢 sprite。
    /// 素材为 6×6 切片的 36 帧旋转序列，帧 N 对应角度 N×5°（覆盖 0~175°，头尾 180° 对称）。
    /// 另含全局统一的"车厢格尺寸"，保证全地图车厢大小一致。
    /// </summary>
    public class WagonSpriteMapper : MonoBehaviour
    {
        /// <summary>每种颜色的角度帧数（180° / 5°）。</summary>
        public const int FrameCount = 36;

        /// <summary>Map.png 像素空间中一节印刷轨道的标准长度（全图统一，实测得出）。</summary>
        public const float DefaultCellPixels = 68f;

        [Header("五色车厢 36 帧（右键组件标题 → 按命名自动填充）")]
        [SerializeField] private Sprite[] red = new Sprite[FrameCount];
        [SerializeField] private Sprite[] yellow = new Sprite[FrameCount];
        [SerializeField] private Sprite[] blue = new Sprite[FrameCount];
        [SerializeField] private Sprite[] green = new Sprite[FrameCount];
        [SerializeField] private Sprite[] purple = new Sprite[FrameCount];

        [Header("车厢格尺寸（全局统一，改这一处即全图生效）")]
        [Tooltip("Map.png 像素空间中每节车厢沿轨道方向的长度。所有路线共用，保证全图车厢一样大。")]
        [SerializeField] private float cellPixels = DefaultCellPixels;

        /// <summary>全局统一的每节车厢长度（Map.png 像素）。</summary>
        public float CellPixels => cellPixels > 1f ? cellPixels : DefaultCellPixels;

        /// <summary>场景里唯一实例（getter 动态查找，不依赖 Awake 时序）。</summary>
        public static WagonSpriteMapper Global => FindAnyObjectByType<WagonSpriteMapper>();

        /// <summary>取某玩家颜色、某角度帧的车厢 sprite；缺失返回 null（调用方自行回退）。</summary>
        public Sprite GetWagon(PlayerTrainColor color, int frame)
        {
            Sprite[] set = GetSet(color);
            if (set == null || set.Length == 0)
            {
                return null;
            }

            if (frame < 0)
            {
                frame = 0;
            }

            if (frame >= set.Length)
            {
                return null;
            }

            Sprite result = set[frame];

            // 运行时保险：旧数组可能误存了整张 960×960 主图（未重新填充前），
            // 这种 sprite 绝不能返回，否则某节车厢会显示整张大图。
            if (result != null && (result.rect.width > 200f || result.rect.height > 200f))
            {
                Debug.LogWarning($"[WagonSpriteMapper] {color} 第 {frame} 帧存的是 {result.rect.width:F0}×{result.rect.height:F0} 的非切格 Sprite（整张主图），已忽略。请在组件上右键『按命名自动填充五色 36 帧』。");
                return null;
            }

            return result;
        }

        private Sprite[] GetSet(PlayerTrainColor color)
        {
            switch (color)
            {
                case PlayerTrainColor.Red: return red;
                case PlayerTrainColor.Yellow: return yellow;
                case PlayerTrainColor.Blue: return blue;
                case PlayerTrainColor.Green: return green;
                case PlayerTrainColor.Purple: return purple;
                default: return null;
            }
        }

        // —— 36 帧几何指标（五色渲染几何完全一致，按 wagons-red.png 逐像素测得）——
        // 坐标系：160×160 切格中心为原点、屏幕坐标（y 向上）。
        // BodyLength：车体沿该帧固有朝向（帧号 × 5°）的像素长度，用于按节长等比缩放；
        // CenterAlong/CenterPerp：车体中心相对切格中心、沿车体长轴 / 垂直长轴的偏移。
        // 美术在切格中并不居中（部分帧偏 8~12px），放置时必须反向补偿，否则整节偏离轨道中心线。
        private static readonly float[] BodyLength =
        {
            113.0f, 115.2f, 114.5f, 117.4f, 116.9f, 118.4f, 129.6f, 115.3f, 114.0f, 113.8f,
            110.2f, 114.1f, 114.1f, 111.6f, 111.3f, 107.4f, 105.3f, 108.0f, 107.0f, 103.4f,
            106.5f, 112.7f, 107.0f, 104.0f, 111.4f, 111.3f, 110.2f, 113.1f, 114.0f, 117.0f,
            116.2f, 118.4f, 117.0f, 117.4f, 114.4f, 115.3f
        };

        private static readonly float[] CenterAlong =
        {
              7.0f,   1.0f,   3.4f,  -0.5f,   5.8f,   2.6f,   2.6f,   2.0f,   1.2f,  -0.4f,
              4.0f,   6.3f,   1.0f,   1.5f,   0.7f,  -0.1f,  -4.1f,  -0.6f,  -2.0f,  -4.1f,
             -1.4f,   0.4f,  -2.8f,   5.3f,  -0.4f,   3.7f,   1.3f,   1.4f,  -2.1f,  -0.9f,
             -1.5f,  -0.8f,  -4.9f,  -1.4f,  -4.8f,  -1.4f
        };

        private static readonly float[] CenterPerp =
        {
              3.5f,   6.4f,  -0.5f,   6.7f,   0.7f,   8.9f,   4.4f,   0.7f,   0.6f,  12.0f,
              1.8f,  -0.4f,   2.5f,   0.8f,  -4.5f,   0.2f,  -3.2f,  -3.7f,  -5.0f,  -2.5f,
             -3.7f,  -3.6f,  -6.8f,  -2.9f,  -3.8f,  -5.6f,  -3.5f,  -6.4f,  -5.9f,  -8.5f,
             -2.8f,  -3.3f,  -6.2f,  -6.4f,  -3.8f,  -6.0f
        };

        /// <summary>
        /// 取某帧的几何指标（车体像素长度、中心沿长轴 / 垂直长轴的像素偏移）。
        /// 偏移基轴为该帧固有朝向 frame×5°：沿轴 =(cos,sin)、垂直轴 =(-sin,cos)，屏幕坐标 y 向上。
        /// </summary>
        public static void GetFrameMetrics(int frame, out float bodyLength, out float centerAlong, out float centerPerp)
        {
            int index = Mathf.Clamp(frame, 0, FrameCount - 1);
            bodyLength = BodyLength[index];
            centerAlong = CenterAlong[index];
            centerPerp = CenterPerp[index];
        }

#if UNITY_EDITOR
        /// <summary>
        /// 按 wagons-&lt;颜色&gt; 贴图自动填充五色各 36 帧。
        /// 不依赖子精灵命名：按 6×6 切格的行列位置排序（左上角为 0 号），与角度序列一致。
        /// </summary>
        [ContextMenu("按命名自动填充五色 36 帧")]
        private void AutoFillFromNames()
        {
            Fill("红色", ref red, "wagons-red");
            Fill("黄色", ref yellow, "wagons-yellow");
            Fill("蓝色", ref blue, "wagons-blue");
            Fill("绿色", ref green, "wagons-green");
            Fill("紫色", ref purple, "wagons-purple");
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private static void Fill(string label, ref Sprite[] target, string textureName)
        {
            string path = FindTexturePath(textureName);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[WagonSpriteMapper] 未找到贴图 {textureName}.png，{label} 跳过。");
                return;
            }

            var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);

            // 切格边长 = 贴图宽 / 6。自动填充只能收 160×160 的单格切片，
            // 绝不能把整张 960×960 主 Sprite 收进来（否则某一帧会显示整张大图）。
            int cellSize = 160;
            foreach (var obj in all)
            {
                if (obj is Texture2D tex)
                {
                    cellSize = Mathf.RoundToInt(tex.width / 6f);
                    break;
                }
            }

            var list = new System.Collections.Generic.List<Sprite>();
            int rejected = 0;
            foreach (var obj in all)
            {
                if (obj is Sprite s && s.rect.width > 1f && s.rect.height > 1f)
                {
                    if (Mathf.Abs(s.rect.width - cellSize) <= 3f && Mathf.Abs(s.rect.height - cellSize) <= 3f)
                    {
                        list.Add(s);
                    }
                    else
                    {
                        rejected++;
                    }
                }
            }

            // 按 6×6 网格排序：rect 原点在左下 → y 大的行在前，同行 x 小的在前。
            list.Sort((p, q) =>
            {
                int rowCompare = q.rect.y.CompareTo(p.rect.y);
                return rowCompare != 0 ? rowCompare : p.rect.x.CompareTo(q.rect.x);
            });

            if (rejected > 0)
            {
                Debug.LogWarning($"[WagonSpriteMapper] {textureName}：已排除 {rejected} 个非 {cellSize}×{cellSize} 切格的 Sprite（通常是整张主图，绝不能混入帧数组）。");
            }

            if (list.Count < FrameCount)
            {
                Debug.LogWarning($"[WagonSpriteMapper] {textureName} 只切出 {list.Count} 个 {cellSize}×{cellSize} 切片（需要 {FrameCount} 个）。请检查 Sprite Editor 的 6×6 切片。");
            }

            target = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount && i < list.Count; i++)
            {
                target[i] = list[i];
            }

            // 明确报告缺帧槽位，缺帧时调用方会回退成色块，不能静默。
            int missing = 0;
            for (int i = 0; i < FrameCount; i++)
            {
                if (target[i] == null)
                {
                    missing++;
                }
            }

            if (missing > 0)
            {
                Debug.LogWarning($"[WagonSpriteMapper] {textureName}：填充 {FrameCount - missing}/{FrameCount} 帧，缺 {missing} 帧，请重新切片后再填充。");
            }
            else
            {
                Debug.Log($"[WagonSpriteMapper] {label}：填充 {FrameCount}/{FrameCount} 帧（{textureName}）。");
            }
        }

        private static string FindTexturePath(string textureName)
        {
            var guids = UnityEditor.AssetDatabase.FindAssets($"{textureName} t:Texture2D");
            foreach (var guid in guids)
            {
                string p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == textureName)
                {
                    return p;
                }
            }

            return null;
        }
#endif
    }
}