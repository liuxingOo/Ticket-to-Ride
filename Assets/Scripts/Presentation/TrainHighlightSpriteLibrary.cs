using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// road-highlight.png 切出的 9 帧高亮条，按 TrainCarColor 映射索引。
    /// 帧顺序（左→右）：红(0) 橙(1) 黄(2) 绿(3) 蓝(4) 粉(5) 黑(6) 白(7) 车头(8)。
    /// </summary>
    public static class TrainHighlightSpriteLibrary
    {
        private static Dictionary<TrainCarColor, Sprite> _sprites;
        private static Sprite _greySprite;
        private static Sprite _locomotiveSprite;

        /// <summary>路径色 → 高亮帧映射（灰色路线走白色边框）。</summary>
        public static Sprite GetForColor(TrainCarColor? color)
        {
            if (_sprites == null)
            {
                LoadAll();
            }

            if (!color.HasValue)
            {
                return _greySprite;
            }

            return _sprites.TryGetValue(color.Value, out var s) ? s : _greySprite;
        }

        public static Sprite GetLocomotive()
        {
            if (_sprites == null)
            {
                LoadAll();
            }

            return _locomotiveSprite;
        }

        private static void LoadAll()
        {
            _sprites = new Dictionary<TrainCarColor, Sprite>();
            // 素材在 Assets/Art/Road/road-highlight.png（没移 Resources），AssetDatabase 直接加载。
            var all = AssetLoader.LoadAll<Sprite>("Art/Road/road-highlight");

            // 帧顺序：红(0) 橙(1) 黄(2) 绿(3) 蓝(4) 粉(5) 黑(6) 白(7) 车头(8)
            _sprites[TrainCarColor.Red] = Find(all, "road-highlight_0");
            _sprites[TrainCarColor.Orange] = Find(all, "road-highlight_1");
            _sprites[TrainCarColor.Yellow] = Find(all, "road-highlight_2");
            _sprites[TrainCarColor.Green] = Find(all, "road-highlight_3");
            _sprites[TrainCarColor.Blue] = Find(all, "road-highlight_4");
            _sprites[TrainCarColor.Pink] = Find(all, "road-highlight_5");
            _sprites[TrainCarColor.Black] = Find(all, "road-highlight_6");
            _sprites[TrainCarColor.White] = Find(all, "road-highlight_7");
            _locomotiveSprite = Find(all, "road-highlight_8");

            // 灰色路线：用白色边框高亮（通用）。
            _greySprite = _sprites[TrainCarColor.White];

            Debug.Log($"[TrainHighlightSpriteLibrary] road-highlight 加载完成，共 {all.Length} 个 Sprite，" +
                      $"红={(_sprites[TrainCarColor.Red] != null ? "OK" : "缺失")}，" +
                      $"车头={(_locomotiveSprite != null ? "OK" : "缺失")}");
        }

        private static Sprite Find(IEnumerable<Sprite> all, string name)
        {
            // 精确匹配
            var exact = all.FirstOrDefault(s => s != null && s.name == name);
            if (exact != null)
            {
                return exact;
            }

            // Unity Sprite Editor 自动命名可能是 road-highlight_0_0，我们兜底用 StartsWith
            return all.FirstOrDefault(s => s != null && s.name.StartsWith(name));
        }
    }

    /// <summary>Resources / AssetDatabase 统一加载入口，避免 Presentation 层直接依赖 AssetDatabase。</summary>
    internal static class AssetLoader
    {
        public static T Load<T>(string path) where T : Object
        {
            var full = path;
            if (typeof(Sprite) == typeof(T))
            {
                // road-highlight.png 切成多帧后，直接按路径+name 查
                return Resources.Load<T>(path);
            }

            return Resources.Load<T>(path);
        }

        public static T[] LoadAll<T>(string folder) where T : Object
        {
#if UNITY_EDITOR
            // 素材直接留在 Assets/Art/ 下，AssetDatabase.FindAssets 按 t:Sprite 搜。
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets" });
            var list = new List<T>();
            foreach (var g in guids)
            {
                var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                // 只取包含目标文件夹的（Art/Road 或 Art/Train）。
                if (!string.IsNullOrEmpty(folder) && !p.Contains(folder))
                {
                    continue;
                }

                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(p);
                foreach (var a in assets)
                {
                    if (a is T typed && !(a is Texture2D))
                    {
                        list.Add(typed);
                    }
                }
            }
            return list.ToArray();
#else
            // 运行时：先试 Resources，再兜底 AssetDatabase（包内也能带 Assets/ 路径）。
            var res = Resources.LoadAll<T>(folder);
            if (res != null && res.Length > 0)
            {
                return res;
            }
            return System.Array.Empty<T>();
#endif
        }
    }
}
