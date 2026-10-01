using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 开局前列车颜色选择面板：当前玩家在 红/黄/蓝/绿/紫 五种颜色里选一种，
    /// 已被前面玩家选走的颜色会置灰禁用。所有玩家依次选完后，
    /// 由 GameSessionView 发初始车票并打开任务卡挑选面板。
    /// 整个面板运行时代码生成（与 ClaimPanel / DestinationDraftPanel 同款），无需预制体。
    /// </summary>
    public class PlayerColorSelectPanel : MonoBehaviour
    {
        private const float ButtonSize = 120f;
        private const float ButtonSpacing = 28f;

        private static readonly PlayerTrainColor[] AllColors =
        {
            PlayerTrainColor.Red,
            PlayerTrainColor.Yellow,
            PlayerTrainColor.Blue,
            PlayerTrainColor.Green,
            PlayerTrainColor.Purple,
        };

        private Action<PlayerTrainColor> _onSelected;
        private bool _finished;

        /// <summary>
        /// 为指定玩家打开选色面板。takenColors 为已被其他玩家选走的颜色（按钮置灰不可点）。
        /// </summary>
        public static PlayerColorSelectPanel Open(
            Canvas canvas,
            Player player,
            ISet<PlayerTrainColor> takenColors,
            Action<PlayerTrainColor> onSelected)
        {
            if (canvas == null || player == null)
            {
                return null;
            }

            var go = new GameObject("PlayerColorSelectPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            var panel = go.AddComponent<PlayerColorSelectPanel>();
            panel.Build(player, takenColors ?? new HashSet<PlayerTrainColor>(), onSelected);
            return panel;
        }

        private void Build(Player player, ISet<PlayerTrainColor> takenColors, Action<PlayerTrainColor> onSelected)
        {
            _onSelected = onSelected;

            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            // 全屏半透明遮罩，拦住对地图的点击。
            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            blocker.transform.SetParent(root, false);
            var blockerRect = (RectTransform)blocker.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            blocker.GetComponent<Image>().raycastTarget = true;

            // 中央面板：标题 + 5 个颜色按钮横排。
            float panelWidth = AllColors.Length * ButtonSize + (AllColors.Length - 1) * ButtonSpacing + 60f;
            const float PanelHeight = 280f;

            var panelRect = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panelRect.SetParent(root, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(panelWidth, PanelHeight);
            panelRect.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.96f);

            var titleLabel = ClaimPanel.CreateLabelPublic(panelRect, "Title", 24, TextAnchor.MiddleCenter);
            titleLabel.text = $"{player.Name}（{player.Id}）选择列车颜色";
            titleLabel.color = Color.white;
            titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleLabel.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            titleLabel.rectTransform.sizeDelta = new Vector2(panelWidth - 40f, 36f);

            var hintLabel = ClaimPanel.CreateLabelPublic(panelRect, "Hint", 16, TextAnchor.MiddleCenter);
            hintLabel.text = "灰色为已被其他玩家选走的颜色";
            hintLabel.color = new Color(1f, 1f, 1f, 0.6f);
            hintLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            hintLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            hintLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            hintLabel.rectTransform.anchoredPosition = new Vector2(0f, -62f);
            hintLabel.rectTransform.sizeDelta = new Vector2(panelWidth - 40f, 24f);

            float rowWidth = AllColors.Length * ButtonSize + (AllColors.Length - 1) * ButtonSpacing;
            float startX = -rowWidth * 0.5f + ButtonSize * 0.5f;

            for (int i = 0; i < AllColors.Length; i++)
            {
                var color = AllColors[i];
                bool taken = takenColors.Contains(color);
                CreateColorButton(panelRect, color, new Vector2(startX + i * (ButtonSize + ButtonSpacing), -10f), taken);
            }
        }

        private void CreateColorButton(Transform parent, PlayerTrainColor color, Vector2 anchoredPos, bool taken)
        {
            var go = new GameObject($"ColorButton_{color}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);

            var image = go.GetComponent<Image>();
            var button = go.GetComponent<Button>();

            image.color = taken ? new Color(0.35f, 0.35f, 0.35f, 1f) : GetColorValue(color);
            image.raycastTarget = true;

            // 颜色中文名压在按钮上。
            var label = ClaimPanel.CreateLabelPublic(rect, "Label", 22, TextAnchor.MiddleCenter);
            label.text = GetColorName(color);
            label.color = Color.white;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            if (taken)
            {
                button.interactable = false;
                return;
            }

            var chosen = color; // 闭包捕获当前迭代值
            button.onClick.AddListener(() => OnColorChosen(chosen));
        }

        private void OnColorChosen(PlayerTrainColor color)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            var callback = _onSelected;
            _onSelected = null;
            Destroy(gameObject);
            callback?.Invoke(color);
        }

        public static string GetColorName(PlayerTrainColor color)
        {
            switch (color)
            {
                case PlayerTrainColor.Red: return "红";
                case PlayerTrainColor.Yellow: return "黄";
                case PlayerTrainColor.Blue: return "蓝";
                case PlayerTrainColor.Green: return "绿";
                case PlayerTrainColor.Purple: return "紫";
                default: return color.ToString();
            }
        }

        /// <summary>供其它运行时面板（如 FinalScorePanel）复用的玩家色值。</summary>
        public static Color GetColorValue(PlayerTrainColor color)
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
    }
}
