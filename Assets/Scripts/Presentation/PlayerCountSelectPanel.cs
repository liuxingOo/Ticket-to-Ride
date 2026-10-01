using System;
using UnityEngine;
using UnityEngine.UI;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 开局前的玩家人数选择面板（2–5 人）。
    /// 与 PlayerColorSelectPanel / ClaimPanel 同款：整块由运行时代码生成，无需预制体。
    /// 选完人数后由 GameSessionView 创建对应数量的玩家，再逐个走选色流程。
    /// </summary>
    public class PlayerCountSelectPanel : MonoBehaviour
    {
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 90f;
        private const float ButtonSpacing = 28f;

        private static readonly int[] Choices = { 2, 3, 4, 5 };

        private Action<int> _onSelected;
        private bool _finished;

        public static PlayerCountSelectPanel Open(Canvas canvas, Action<int> onSelected)
        {
            if (canvas == null)
            {
                return null;
            }

            var go = new GameObject("PlayerCountSelectPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            var panel = go.AddComponent<PlayerCountSelectPanel>();
            panel.Build(onSelected);
            return panel;
        }

        private void Build(Action<int> onSelected)
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
            var blockerImage = blocker.GetComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.45f);
            blockerImage.raycastTarget = true;

            float rowWidth = Choices.Length * ButtonWidth + (Choices.Length - 1) * ButtonSpacing;
            float panelWidth = rowWidth + 60f;
            const float PanelHeight = 300f;

            var panelRect = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panelRect.SetParent(root, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(panelWidth, PanelHeight);
            panelRect.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.96f);

            var titleLabel = ClaimPanel.CreateLabelPublic(panelRect, "Title", 24, TextAnchor.MiddleCenter);
            titleLabel.text = "选择玩家人数";
            titleLabel.color = Color.white;
            titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleLabel.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            titleLabel.rectTransform.sizeDelta = new Vector2(panelWidth - 40f, 36f);

            var hintLabel = ClaimPanel.CreateLabelPublic(panelRect, "Hint", 16, TextAnchor.MiddleCenter);
            hintLabel.text = "行动顺序按 红 → 黄 → 蓝 → 绿 → 紫 排列";
            hintLabel.color = new Color(1f, 1f, 1f, 0.6f);
            hintLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            hintLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            hintLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            hintLabel.rectTransform.anchoredPosition = new Vector2(0f, -62f);
            hintLabel.rectTransform.sizeDelta = new Vector2(panelWidth - 40f, 24f);

            float startX = -rowWidth * 0.5f + ButtonWidth * 0.5f;

            for (int i = 0; i < Choices.Length; i++)
            {
                int count = Choices[i]; // 闭包捕获当前迭代值
                var button = ClaimPanel.CreateButtonPublic(panelRect, $"{count} 人", new Color(0.2f, 0.45f, 0.9f, 1f));
                button.onClick.AddListener(() => OnCountChosen(count));

                var rect = (RectTransform)button.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(startX + i * (ButtonWidth + ButtonSpacing), -20f);
                rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            }
        }

        private void OnCountChosen(int count)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            var callback = _onSelected;
            _onSelected = null;
            Destroy(gameObject);
            callback?.Invoke(count);
        }
    }
}