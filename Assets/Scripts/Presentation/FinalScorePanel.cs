using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 终局结算面板：把每位玩家的路线分、任务卡 ±分、最长连续路径加分和总分列成一张表。
    /// 与 ClaimPanel / PlayerColorSelectPanel 同款，整块由运行时代码生成，无需预制体。
    /// </summary>
    public class FinalScorePanel : MonoBehaviour
    {
        private const float PanelPadding = 24f;
        private const float RowHeight = 34f;
        private const float HeaderHeight = 34f;

        private static readonly (string Title, float Width, TextAnchor Anchor)[] Columns =
        {
            ("名次", 60f, TextAnchor.MiddleCenter),
            ("玩家", 160f, TextAnchor.MiddleLeft),
            ("路线分", 100f, TextAnchor.MiddleCenter),
            ("任务卡", 220f, TextAnchor.MiddleCenter),
            ("最长路径", 120f, TextAnchor.MiddleCenter),
            ("总分", 100f, TextAnchor.MiddleCenter),
        };

        private static readonly Color WinnerColor = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color NormalColor = new Color(1f, 1f, 1f, 0.92f);
        private static readonly Color HeaderColor = new Color(1f, 1f, 1f, 0.55f);

        private static readonly float[] ColumnCenters = BuildColumnCenters();

        /// <summary>
        /// 打开结算面板。数据是协议行：单机由本端算好、联机由主机随快照下发，
        /// 表现层不关心来源，只负责画表。返回 null 表示创建失败。
        /// onRestart 不为 null 时额外显示「再来一局」按钮（点击后回到菜单重新开局）。
        /// </summary>
        public static FinalScorePanel Open(
            Canvas canvas,
            IReadOnlyList<FinalScoreEntry> entries,
            System.Action onRestart = null)
        {
            if (canvas == null || entries == null || entries.Count == 0)
            {
                return null;
            }

            var go = new GameObject("FinalScorePanel", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            var panel = go.AddComponent<FinalScorePanel>();
            panel.Build(entries, onRestart);
            return panel;
        }

        private void Build(IReadOnlyList<FinalScoreEntry> entries, System.Action onRestart)
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            // 全屏半透明遮罩，拦住对地图的点击（面板关闭后地图恢复可看）。
            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            blocker.transform.SetParent(root, false);
            var blockerRect = (RectTransform)blocker.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            var blockerImage = blocker.GetComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.55f);
            blockerImage.raycastTarget = true;

            int rowCount = entries.Count;
            bool hasTie = entries.GroupBy(entry => entry.Total).Any(group => group.Count() > 1);
            float footnoteHeight = hasTie ? 26f : 0f;

            float tableWidth = Columns.Sum(column => column.Width);
            float panelWidth = tableWidth + PanelPadding * 2f;

            float y = -PanelPadding;
            float titleTop = y;
            y -= 36f;
            float winnerTop = y;
            y -= 30f + 8f;
            float headerTop = y;
            y -= HeaderHeight;
            float rowsTop = y;
            y -= rowCount * RowHeight;
            y -= 12f;
            float footnoteTop = y;
            y -= footnoteHeight;
            float buttonTop = y - PanelPadding;

            float panelHeight = Mathf.Abs(buttonTop) + 46f + PanelPadding;

            var panelRect = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panelRect.SetParent(root, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
            panelRect.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.96f);

            var title = ClaimPanel.CreateLabelPublic(panelRect, "Title", 26, TextAnchor.MiddleCenter);
            title.text = "游戏结束";
            title.color = Color.white;
            Place(title.rectTransform, 0f, titleTop, panelWidth - PanelPadding * 2f, 36f);

            var winnerLabel = ClaimPanel.CreateLabelPublic(panelRect, "Winner", 19, TextAnchor.MiddleCenter);
            winnerLabel.text = BuildWinnerText(entries);
            winnerLabel.color = WinnerColor;
            Place(winnerLabel.rectTransform, 0f, winnerTop, panelWidth - PanelPadding * 2f, 30f);

            for (int i = 0; i < Columns.Length; i++)
            {
                var header = ClaimPanel.CreateLabelPublic(panelRect, $"Header_{Columns[i].Title}", 15, Columns[i].Anchor);
                header.text = Columns[i].Title;
                header.color = HeaderColor;
                Place(header.rectTransform, ColumnCenters[i], headerTop, Columns[i].Width, HeaderHeight);
            }

            for (int i = 0; i < rowCount; i++)
            {
                BuildRow(panelRect, entries[i], rowsTop - i * RowHeight);
            }

            if (hasTie)
            {
                var footnote = ClaimPanel.CreateLabelPublic(panelRect, "Footnote", 14, TextAnchor.MiddleCenter);
                footnote.text = "总分相同时，完成任务卡多者获胜";
                footnote.color = new Color(1f, 1f, 1f, 0.5f);
                Place(footnote.rectTransform, 0f, footnoteTop, panelWidth - PanelPadding * 2f, 24f);
            }

            var closeButton = ClaimPanel.CreateButtonPublic(panelRect, "关闭", new Color(0.3f, 0.3f, 0.32f, 1f));
            closeButton.onClick.AddListener(() => Destroy(gameObject));

            const float closeWidth = 140f;
            const float restartWidth = 180f;
            const float buttonGap = 16f;

            if (onRestart == null)
            {
                PlaceBottomButton((RectTransform)closeButton.transform, 0f, closeWidth, PanelPadding);
                return;
            }

            // 两个按钮并排居中：先算整排宽度，再左右各放一个。
            float rowWidth = closeWidth + buttonGap + restartWidth;
            float rowLeft = -rowWidth * 0.5f;
            PlaceBottomButton((RectTransform)closeButton.transform, rowLeft + closeWidth * 0.5f, closeWidth, PanelPadding);

            var restartButton = ClaimPanel.CreateButtonPublic(panelRect, "再来一局", new Color(0.25f, 0.6f, 0.35f, 1f));
            restartButton.onClick.AddListener(() =>
            {
                Destroy(gameObject);
                onRestart();
            });
            PlaceBottomButton(
                (RectTransform)restartButton.transform,
                rowLeft + closeWidth + buttonGap + restartWidth * 0.5f,
                restartWidth,
                PanelPadding);
        }

        /// <summary>底部按钮：以面板底边为基准居中摆放。</summary>
        private static void PlaceBottomButton(RectTransform rect, float centerX, float width, float bottomOffset)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(centerX, bottomOffset);
            rect.sizeDelta = new Vector2(width, 42f);
        }

        private static void BuildRow(Transform parent, FinalScoreEntry entry, float top)
        {
            bool isWinner = entry.Rank == 1;
            var textColor = isWinner ? WinnerColor : NormalColor;

            // 名次数字：真正并列（总分与完成任务卡数都相同）的玩家共用同一个名次。
            var values = new[]
            {
                entry.Rank.ToString(),
                $"{(isWinner ? "★ " : string.Empty)}{entry.PlayerName}（{PlayerColorSelectPanel.GetColorName(entry.Color)}）",
                entry.RouteScore.ToString(),
                $"{FormatSigned(entry.TicketScore)}（完成 {entry.CompletedTickets}/{entry.TotalTickets}）",
                $"{entry.LongestRouteLength} 节{(entry.HasLongestRoute ? $" +{entry.LongestRouteBonus}" : string.Empty)}",
                entry.Total.ToString(),
            };

            for (int i = 0; i < Columns.Length; i++)
            {
                var label = ClaimPanel.CreateLabelPublic(parent, $"Row_{entry.PlayerId}_{Columns[i].Title}", 16, Columns[i].Anchor);
                label.text = values[i];
                label.color = i == 1 ? (isWinner ? WinnerColor : PlayerColorSelectPanel.GetColorValue(entry.Color)) : textColor;
                Place(label.rectTransform, ColumnCenters[i], top, Columns[i].Width, RowHeight);
            }
        }

        private static string BuildWinnerText(IReadOnlyList<FinalScoreEntry> entries)
        {
            // 名次为 1 的就是获胜者，并列获胜时会有多行。
            var winners = entries.Where(entry => entry.Rank == 1).ToList();
            if (winners.Count == 0)
            {
                return string.Empty;
            }

            var names = string.Join("、", winners.Select(winner => winner.PlayerName));
            int score = winners[0].Total;

            return winners.Count > 1
                ? $"并列获胜：{names}（各 {score} 分）"
                : $"获胜：{names}（{score} 分）";
        }

        private static string FormatSigned(int value)
        {
            return value >= 0 ? $"+{value}" : value.ToString();
        }

        private static void Place(RectTransform rect, float centerX, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(centerX, top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static float[] BuildColumnCenters()
        {
            float totalWidth = Columns.Sum(column => column.Width);
            var centers = new float[Columns.Length];
            float cursor = -totalWidth * 0.5f;

            for (int i = 0; i < Columns.Length; i++)
            {
                centers[i] = cursor + Columns[i].Width * 0.5f;
                cursor += Columns[i].Width;
            }

            return centers;
        }
    }
}