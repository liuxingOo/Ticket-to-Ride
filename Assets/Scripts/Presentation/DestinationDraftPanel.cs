using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 开局任务卡挑选面板：发 3 张，玩家点选要保留的（至少 2 张），确认后
    /// 保留的进手牌（左下角堆叠区），弃掉的放回任务牌堆底。
    /// 整个面板运行时由代码生成（与 ClaimPanel 同一套做法），无需预制体。
    /// </summary>
    public class DestinationDraftPanel : MonoBehaviour
    {
        private const float CardWidth = 300f;
        private const float CardHeight = 280f;
        private const float CardSpacing = 24f;

        private int _minKeep;
        private string _title;

        private readonly List<DestinationTicketCardView> _cardViews = new List<DestinationTicketCardView>();
        private readonly List<bool> _selectedFlags = new List<bool>();
        private readonly List<Vector2> _basePositions = new List<Vector2>();

        private Text _statusLabel;
        private Button _confirmButton;
        private Text _confirmLabel;

        private List<DestinationTicket> _drawnTickets;
        private Action<List<DestinationTicket>> _onConfirm;

        /// <summary>打开任务卡挑选面板：drawnTickets 为刚从牌堆摸出的任务卡，minKeep 为至少保留张数（开局 2、游戏中 1）。</summary>
        public static DestinationDraftPanel Open(
            Canvas canvas,
            List<DestinationTicket> drawnTickets,
            int minKeep,
            string title,
            Action<List<DestinationTicket>> onConfirm)
        {
            if (canvas == null || drawnTickets == null || drawnTickets.Count == 0)
            {
                return null;
            }

            var go = new GameObject("DestinationDraftPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            var panel = go.AddComponent<DestinationDraftPanel>();
            panel.Build(drawnTickets, minKeep, title, onConfirm);
            return panel;
        }

        private void Build(List<DestinationTicket> drawnTickets, int minKeep, string title, Action<List<DestinationTicket>> onConfirm)
        {
            _drawnTickets = drawnTickets;
            _minKeep = minKeep;
            _title = title;
            _onConfirm = onConfirm;

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
            // 遮罩调淡：挑选任务卡时要能看清地图上点亮的两个城市蓝标。
            blockerImage.color = new Color(0f, 0f, 0f, 0.08f);
            blockerImage.raycastTarget = true;

            // 中央面板。半透明：能透出下面的地图，方便对照城市位置。
            var panelRect = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panelRect.SetParent(root, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(CardWidth * 3f + CardSpacing * 2f + 40f, 500f);
            panelRect.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.72f);

            // 顶部拖动条：只有这一条能拖面板，不和卡片的点击抢事件。
            var titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            titleBar.SetParent(panelRect, false);
            titleBar.anchorMin = new Vector2(0f, 1f);
            titleBar.anchorMax = new Vector2(1f, 1f);
            titleBar.pivot = new Vector2(0.5f, 1f);
            titleBar.anchoredPosition = Vector2.zero;
            titleBar.sizeDelta = new Vector2(0f, 56f);
            var titleBarImage = titleBar.GetComponent<Image>();
            titleBarImage.color = new Color(1f, 1f, 1f, 0.06f); // 很淡的一条，提示这里可以按住拖
            titleBarImage.raycastTarget = true;
            titleBar.gameObject.AddComponent<UiDragHandle>().Target = panelRect;

            var titleLabel = ClaimPanel.CreateLabelPublic(titleBar, "Title", 22, TextAnchor.MiddleCenter);
            titleLabel.text = _title;
            titleLabel.color = Color.white;
            titleLabel.rectTransform.anchorMin = Vector2.zero;
            titleLabel.rectTransform.anchorMax = Vector2.one;
            titleLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
            titleLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);
            titleLabel.raycastTarget = false; // 点击/拖动都交给下面的拖动条处理。

            var dragHint = ClaimPanel.CreateLabelPublic(panelRect, "DragHint", 13, TextAnchor.MiddleCenter);
            dragHint.text = "按住顶部标题栏可拖动本面板";
            dragHint.color = new Color(1f, 1f, 1f, 0.45f);
            dragHint.rectTransform.anchorMin = new Vector2(0f, 1f);
            dragHint.rectTransform.anchorMax = new Vector2(1f, 1f);
            dragHint.rectTransform.pivot = new Vector2(0.5f, 1f);
            dragHint.rectTransform.anchoredPosition = new Vector2(0f, -58f);
            dragHint.rectTransform.sizeDelta = new Vector2(panelRect.sizeDelta.x - 40f, 18f);
            dragHint.raycastTarget = false;

            // 三张任务卡横排。
            var library = DestinationTicketSpriteLibrary.Global;
            int count = _drawnTickets.Count;
            float rowWidth = count * CardWidth + (count - 1) * CardSpacing;

            for (int i = 0; i < count; i++)
            {
                var cardObject = new GameObject($"DraftTicket_{i}", typeof(RectTransform));
                cardObject.transform.SetParent(panelRect, false);

                var view = cardObject.AddComponent<DestinationTicketCardView>();
                var face = library != null ? library.GetFace(_drawnTickets[i].ArtIndex) : null;
                view.Initialize(_drawnTickets[i], face);
                view.Clicked += OnCardClicked;

                var rect = (RectTransform)cardObject.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(CardWidth, CardHeight);

                float x = -rowWidth * 0.5f + CardWidth * 0.5f + i * (CardWidth + CardSpacing);
                rect.anchoredPosition = new Vector2(x, 20f);

                _cardViews.Add(view);
                _selectedFlags.Add(false);
                _basePositions.Add(rect.anchoredPosition);
            }

            _statusLabel = ClaimPanel.CreateLabelPublic(panelRect, "Status", 18, TextAnchor.MiddleCenter);
            _statusLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            _statusLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            _statusLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            _statusLabel.rectTransform.anchoredPosition = new Vector2(0f, 70f);
            _statusLabel.rectTransform.sizeDelta = new Vector2(panelRect.sizeDelta.x - 40f, 28f);

            _confirmButton = ClaimPanel.CreateButtonPublic(panelRect, "确认保留", new Color(0.2f, 0.55f, 0.25f, 1f));
            _confirmButton.onClick.AddListener(OnConfirmClicked);
            var confirmRect = _confirmButton.GetComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.5f, 0f);
            confirmRect.anchorMax = new Vector2(0.5f, 0f);
            confirmRect.pivot = new Vector2(0.5f, 0f);
            confirmRect.anchoredPosition = new Vector2(0f, 16f);
            confirmRect.sizeDelta = new Vector2(160f, 40f);
            _confirmLabel = _confirmButton.GetComponentInChildren<Text>();

            UpdateStatus();
        }

        private void OnCardClicked(DestinationTicketCardView view)
        {
            int index = _cardViews.IndexOf(view);
            if (index < 0)
            {
                return;
            }

            _selectedFlags[index] = !_selectedFlags[index];

            // 选中的卡向上弹一点，取消选中回到原位。
            var rect = (RectTransform)view.transform;
            rect.anchoredPosition = _basePositions[index] + (_selectedFlags[index] ? Vector2.up * 30f : Vector2.zero);

            // 与手牌区一致：点中的卡在地图上标出两个城市，取消就熄灭。
            var highlight = DestinationHighlightView.Global;
            if (_selectedFlags[index])
            {
                highlight?.Show(view.Ticket.StartCity, view.Ticket.EndCity);
            }
            else
            {
                highlight?.Hide();
            }

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            int selectedCount = 0;
            foreach (var flag in _selectedFlags)
            {
                if (flag)
                {
                    selectedCount++;
                }
            }

            bool enough = selectedCount >= _minKeep;
            _statusLabel.text = enough
                ? $"已选 {selectedCount} 张，剩下的会放回牌堆并洗牌"
                : $"已选 {selectedCount} 张，至少要保留 {_minKeep} 张";
            _statusLabel.color = enough ? new Color(0.4f, 1f, 0.4f, 1f) : new Color(1f, 0.85f, 0.3f, 1f);

            _confirmButton.interactable = enough;
            if (_confirmLabel != null)
            {
                _confirmLabel.color = enough ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            }
        }

        private void OnConfirmClicked()
        {
            int selectedCount = 0;
            foreach (var flag in _selectedFlags)
            {
                if (flag)
                {
                    selectedCount++;
                }
            }

            if (selectedCount < _minKeep)
            {
                return;
            }

            DestinationHighlightView.Global?.Hide();

            var kept = new List<DestinationTicket>();
            for (int i = 0; i < _cardViews.Count; i++)
            {
                if (_selectedFlags[i])
                {
                    kept.Add(_drawnTickets[i]);
                }
            }

            _onConfirm?.Invoke(kept);
            Destroy(gameObject);
        }
    }
}
