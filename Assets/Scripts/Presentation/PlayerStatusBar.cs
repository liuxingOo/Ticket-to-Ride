using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 右上角玩家状态栏：每个玩家占一横行，从左到右依次显示
    /// 玩家名字（文字颜色 = 该玩家车厢颜色） [分数图标] 得分 [列车图标] 剩余车厢数 [车票图标] 持有车票数 [任务卡图标] 持有任务卡数
    /// 图标 sprite 留空时显示为透明占位方块。
    /// </summary>
    public class PlayerStatusBar : MonoBehaviour
    {
        [Header("剩余车厢数量的列车图标：按玩家车厢颜色取对应颜色（红/黄/蓝/绿/紫五色，切片后逐色拖入，留空则用兜底图标）")]
        [SerializeField] private Sprite redIdIcon;
        [SerializeField] private Sprite yellowIdIcon;
        [SerializeField] private Sprite blueIdIcon;
        [SerializeField] private Sprite greenIdIcon;
        [SerializeField] private Sprite purpleIdIcon;
        [Tooltip("兜底图标：对应颜色槽没拖 sprite 时使用")]
        [SerializeField] private Sprite idIcon;

        [Header("其余三个数据项的图标（长方形素材也可，会按原始比例显示）")]
        [Tooltip("得分前面的图标")]
        [SerializeField] private Sprite scoreIcon;
        [Tooltip("车票数前面的图标")]
        [SerializeField] private Sprite handIcon;
        [Tooltip("任务卡数前面的图标")]
        [SerializeField] private Sprite ticketIcon;

        [Header("布局参数")]
        [SerializeField] private float rowHeight = 44f;
        [Tooltip("图标高度；图标宽度按各自素材宽高比自动计算")]
        [SerializeField] private float iconSize = 24f;
        [SerializeField] private float iconToTextGap = 6f;
        [SerializeField] private float itemSpacing = 16f;
        [SerializeField] private int fontSize = 16;
        [Tooltip("状态栏总宽度（像素）")]
        [SerializeField] private float totalWidth = 520f;

        private RectTransform _container;
        private readonly List<RowData> _rows = new();

        /// <summary>场景里唯一实例（和 HighlightSpriteMapper 同款 getter）。</summary>
        public static PlayerStatusBar Global => FindAnyObjectByType<PlayerStatusBar>();

        /// <summary>延迟获取容器：不依赖 Awake 时序（GameSessionView.Awake 可能先于本组件执行）。</summary>
        private RectTransform Container
        {
            get
            {
                if (_container == null)
                {
                    _container = (RectTransform)transform;
                }
                return _container;
            }
        }

        /// <summary>用一组玩家数据重建整张状态栏。</summary>
        public void Refresh(IReadOnlyList<Player> players)
        {
            if (players == null || Container == null)
            {
                return;
            }

            ClearRows();
            _rows.Clear();

            int count = players.Count;
            float totalHeight = count * rowHeight;

            for (int i = 0; i < count; i++)
            {
                float yOffset = -(i * rowHeight);
                var row = CreateRow(Container, players[i], yOffset);
                _rows.Add(row);
            }

            Container.sizeDelta = new Vector2(totalWidth, totalHeight);
        }

        /// <summary>只更新数据（不重建布局），供频繁刷新用。</summary>
        public void UpdateData(IReadOnlyList<Player> players)
        {
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count && i < _rows.Count; i++)
            {
                UpdateRowData(_rows[i], players[i]);
            }
        }

        private void ClearRows()
        {
            for (int i = Container.childCount - 1; i >= 0; i--)
            {
                Destroy(Container.GetChild(i).gameObject);
            }
        }

        private RowData CreateRow(Transform parent, Player player, float yOffset)
        {
            var rowGo = new GameObject($"PlayerRow_{player.Id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rowGo.transform.SetParent(parent, false);
            var rowRect = (RectTransform)rowGo.transform;
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, yOffset);
            rowRect.sizeDelta = new Vector2(0f, rowHeight);

            // 行背景：很淡的半透明色（用玩家颜色的极低透明度）
            var rowBg = rowGo.GetComponent<Image>();
            rowBg.color = new Color(0.12f, 0.12f, 0.14f, 0.5f);
            rowBg.raycastTarget = false;

            // 边框：用 Outline 组件
            var outline = rowGo.AddComponent<Outline>();
            outline.effectColor = GetPlayerColor(player.Color);
            outline.effectDistance = new Vector2(2f, -2f);

            float cursorX = 0f;

            // --- 玩家名字（排在最前，无前置图标；文字颜色按玩家车厢颜色）---
            var nameLabel = CreateText(rowRect, "Name", fontSize, TextAnchor.MiddleLeft);
            nameLabel.color = GetPlayerColor(player.Color);
            nameLabel.text = player.Name;
            PositionText(nameLabel.rectTransform, ref cursorX);
            cursorX += itemSpacing;

            // --- 得分 ---
            CreateIcon(rowRect, scoreIcon, ref cursorX);
            var scoreLabel = CreateText(rowRect, "Score", fontSize, TextAnchor.MiddleLeft);
            scoreLabel.color = Color.white;
            scoreLabel.text = player.Score.ToString();
            PositionText(scoreLabel.rectTransform, ref cursorX);
            cursorX += itemSpacing;

            // --- 剩余车厢数（列车图标按玩家车厢颜色取对应颜色的列车图）---
            CreateIcon(rowRect, GetTrainIcon(player.Color), ref cursorX);
            var trainLabel = CreateText(rowRect, "Trains", fontSize, TextAnchor.MiddleLeft);
            trainLabel.color = Color.white;
            trainLabel.text = player.TrainCarsRemaining.ToString();
            PositionText(trainLabel.rectTransform, ref cursorX);
            cursorX += itemSpacing;

            // --- 车票数 ---
            CreateIcon(rowRect, handIcon, ref cursorX);
            var handLabel = CreateText(rowRect, "Hand", fontSize, TextAnchor.MiddleLeft);
            handLabel.color = Color.white;
            handLabel.text = player.Hand.Count.ToString();
            PositionText(handLabel.rectTransform, ref cursorX);
            cursorX += itemSpacing;

            // --- 任务卡数 ---
            CreateIcon(rowRect, ticketIcon, ref cursorX);
            var ticketLabel = CreateText(rowRect, "Tickets", fontSize, TextAnchor.MiddleLeft);
            ticketLabel.color = Color.white;
            ticketLabel.text = player.DestinationTickets.Count.ToString();
            PositionText(ticketLabel.rectTransform, ref cursorX);

            return new RowData
            {
                nameLabel = nameLabel,
                scoreLabel = scoreLabel,
                trainLabel = trainLabel,
                handLabel = handLabel,
                ticketLabel = ticketLabel
            };
        }

        /// <summary>在当前 cursor 位置放一个图标（sprite 为 null 时放透明占位方块）。</summary>
        private RectTransform CreateIcon(Transform parent, Sprite sprite, ref float cursorX)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(cursorX, 0f);

            var img = go.GetComponent<Image>();
            float iconHeight = iconSize;
            float iconWidth = iconSize;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
                // 以 iconSize 作为图标高度，宽度按 sprite 原始宽高比计算，长方形素材不再被压成正方形
                if (sprite.rect.height > 0f)
                {
                    iconWidth = iconHeight * (sprite.rect.width / sprite.rect.height);
                }
            }
            else
            {
                // 透明占位方块：只占位不渲染
                img.color = new Color(1f, 1f, 1f, 0f);
            }
            img.raycastTarget = false;

            rect.sizeDelta = new Vector2(iconWidth, iconHeight);
            cursorX += iconWidth + iconToTextGap;
            return rect;
        }

        /// <summary>把文字放在当前 cursor 位置（cursor 已包含前置图标宽度与其间距）。</summary>
        private void PositionText(RectTransform textRect, ref float cursorX)
        {
            textRect.anchorMin = new Vector2(0f, 0.5f);
            textRect.anchorMax = new Vector2(0f, 0.5f);
            textRect.pivot = new Vector2(0f, 0.5f);
            textRect.anchoredPosition = new Vector2(cursorX, 0f);

            // 按实际文字内容宽度排版，避免写死宽度把后面的项挤出状态栏右边界
            float textWidth = 120f;
            var text = textRect.GetComponent<Text>();
            if (text != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
                // 数字通常只有 1~2 位，保底 20 即可，避免白占宽度把最后一项挤出 400px
                textWidth = Mathf.Max(20f, text.preferredWidth);
            }
            textRect.sizeDelta = new Vector2(textWidth, rowHeight);

            cursorX += textWidth;
        }

        private static Text CreateText(Transform parent, string name, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void UpdateRowData(RowData row, Player player)
        {
            if (row.nameLabel != null)
            {
                row.nameLabel.text = player.Name;
                row.nameLabel.color = GetPlayerColor(player.Color);
            }

            if (row.scoreLabel != null)
            {
                row.scoreLabel.text = player.Score.ToString();
            }

            if (row.trainLabel != null)
            {
                row.trainLabel.text = player.TrainCarsRemaining.ToString();
            }

            if (row.handLabel != null)
            {
                row.handLabel.text = player.Hand.Count.ToString();
            }

            if (row.ticketLabel != null)
            {
                row.ticketLabel.text = player.DestinationTickets.Count.ToString();
            }
        }

        /// <summary>剩余车厢数的列车图标：优先按玩家车厢颜色取对应颜色，该颜色槽没拖时回退到兜底 idIcon。</summary>
        private Sprite GetTrainIcon(PlayerTrainColor color)
        {
            Sprite colored;
            switch (color)
            {
                case PlayerTrainColor.Red: colored = redIdIcon; break;
                case PlayerTrainColor.Yellow: colored = yellowIdIcon; break;
                case PlayerTrainColor.Blue: colored = blueIdIcon; break;
                case PlayerTrainColor.Green: colored = greenIdIcon; break;
                case PlayerTrainColor.Purple: colored = purpleIdIcon; break;
                default: colored = null; break;
            }

            return colored != null ? colored : idIcon;
        }

        private static Color GetPlayerColor(PlayerTrainColor color)
        {
            switch (color)
            {
                case PlayerTrainColor.Red: return new Color(0.85f, 0.2f, 0.2f, 1f);
                case PlayerTrainColor.Yellow: return new Color(0.95f, 0.85f, 0.2f, 1f);
                case PlayerTrainColor.Blue: return new Color(0.2f, 0.45f, 0.9f, 1f);
                case PlayerTrainColor.Green: return new Color(0.25f, 0.7f, 0.3f, 1f);
                case PlayerTrainColor.Purple: return new Color(0.6f, 0.25f, 0.85f, 1f);
                default: return Color.gray;
            }
        }

        private class RowData
        {
            public Text nameLabel;
            public Text scoreLabel;
            public Text trainLabel;
            public Text handLabel;
            public Text ticketLabel;
        }
    }
}
