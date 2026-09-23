using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 路线认领选卡面板：点击路线后弹出，玩家手动挑选要打出的车厢卡。
    /// · 有色路线：只能选路线颜色 + 火车头；
    /// · 灰色路线：任选一种非火车头颜色 + 火车头（选了一种后其它色自动禁用）；
    /// · 选满路线长度才能确认；手牌不够时直接显示缺卡提示。
    /// 整个面板在运行时由代码生成，无需手动摆预制体。
    /// </summary>
    public class ClaimPanel : MonoBehaviour
    {
        private const float PanelWidth = 520f;

        private RectTransform _root;
        private RectTransform _panel;
        private Text _titleLabel;
        private Text _statusLabel;
        private Transform _availableRoot;
        private Transform _selectedRoot;
        private Button _confirmButton;
        private Text _confirmLabel;

        private Route _route;
        private Player _player;
        private Action<List<TrainCarCard>> _onConfirm;
        private Action _onCancel;

        private Dictionary<TrainCarColor, int> _availableByColor;
        private readonly Dictionary<TrainCarColor, int> _selectedByColor = new();
        private TrainCarColor? _greyChosenColor;

        /// <summary>
        /// 打开认领面板。返回 null 表示面板创建失败。
        /// </summary>
        public static ClaimPanel Open(
            Canvas canvas,
            Route route,
            Player player,
            Action<List<TrainCarCard>> onConfirm,
            Action onCancel)
        {
            if (canvas == null)
            {
                return null;
            }

            var go = new GameObject("ClaimPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            var panel = go.AddComponent<ClaimPanel>();
            panel.Build(canvas, route, player, onConfirm, onCancel);
            return panel;
        }

        private void Build(
            Canvas canvas,
            Route route,
            Player player,
            Action<List<TrainCarCard>> onConfirm,
            Action onCancel)
        {
            _route = route;
            _player = player;
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            _availableByColor = player.Hand.GetCards()
                .Where(c => c != null)
                .GroupBy(c => c.Color)
                .ToDictionary(g => g.Key, g => g.Count());

            _root = (RectTransform)transform;
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            // 全屏半透明遮罩，拦住对地图的点击。
            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            blocker.transform.SetParent(_root, false);
            var blockerRect = (RectTransform)blocker.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            var blockerImage = blocker.GetComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.45f);
            blockerImage.raycastTarget = true;

            // 中央面板。
            _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            _panel.SetParent(_root, false);
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(PanelWidth, 420f);
            var panelImage = _panel.GetComponent<Image>();
            panelImage.color = new Color(0.12f, 0.12f, 0.14f, 0.96f);
            panelImage.raycastTarget = true;

            _titleLabel = CreateLabel(_panel, "Title", 22, TextAnchor.MiddleCenter);
            _titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _titleLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            _titleLabel.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            _titleLabel.rectTransform.sizeDelta = new Vector2(PanelWidth - 40f, 30f);
            _titleLabel.color = Color.white;

            _statusLabel = CreateLabel(_panel, "Status", 17, TextAnchor.MiddleCenter);
            _statusLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            _statusLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            _statusLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            _statusLabel.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            _statusLabel.rectTransform.sizeDelta = new Vector2(PanelWidth - 40f, 30f);

            // 可选卡牌区。
            var availLabel = CreateLabel(_panel, "AvailLabel", 15, TextAnchor.MiddleLeft);
            availLabel.text = "可选卡牌（点击选取）";
            availLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            availLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            availLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            availLabel.rectTransform.anchoredPosition = new Vector2(0f, -58f);
            availLabel.rectTransform.sizeDelta = new Vector2(PanelWidth - 40f, 22f);
            availLabel.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            _availableRoot = new GameObject("Available", typeof(RectTransform)).transform;
            _availableRoot.SetParent(_panel, false);
            var availRect = (RectTransform)_availableRoot;
            availRect.anchorMin = new Vector2(0f, 1f);
            availRect.anchorMax = new Vector2(1f, 1f);
            availRect.pivot = new Vector2(0.5f, 1f);
            availRect.anchoredPosition = new Vector2(0f, -84f);
            availRect.sizeDelta = new Vector2(PanelWidth - 40f, 70f);

            // 已选卡牌区。
            var selLabel = CreateLabel(_panel, "SelLabel", 15, TextAnchor.MiddleLeft);
            selLabel.text = "已选卡牌";
            selLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            selLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            selLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            selLabel.rectTransform.anchoredPosition = new Vector2(0f, -166f);
            selLabel.rectTransform.sizeDelta = new Vector2(PanelWidth - 40f, 22f);
            selLabel.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            _selectedRoot = new GameObject("Selected", typeof(RectTransform)).transform;
            _selectedRoot.SetParent(_panel, false);
            var selRect = (RectTransform)_selectedRoot;
            selRect.anchorMin = new Vector2(0f, 1f);
            selRect.anchorMax = new Vector2(1f, 1f);
            selRect.pivot = new Vector2(0.5f, 1f);
            selRect.anchoredPosition = new Vector2(0f, -192f);
            selRect.sizeDelta = new Vector2(PanelWidth - 40f, 70f);

            // 取消 / 确认按钮。
            var cancelButton = CreateButton(_panel, "取消", new Color(0.3f, 0.3f, 0.32f, 1f));
            cancelButton.onClick.AddListener(OnCancelClicked);
            LayoutButton(cancelButton.GetComponent<RectTransform>(), -140f);

            _confirmButton = CreateButton(_panel, "确认认领", new Color(0.2f, 0.55f, 0.25f, 1f));
            _confirmButton.onClick.AddListener(OnConfirmClicked);
            _confirmLabel = _confirmButton.GetComponentInChildren<Text>();
            LayoutButton(_confirmButton.GetComponent<RectTransform>(), 140f);

            BuildAvailableButtons();
            UpdateSelectionView();
        }

        /// <summary>
        /// 构建可选卡牌按钮：有色路线只显示路线色 + 火车头；
        /// 灰色路线显示全部颜色。
        /// </summary>
        private void BuildAvailableButtons()
        {
            foreach (Transform child in _availableRoot)
            {
                Destroy(child.gameObject);
            }

            IEnumerable<TrainCarColor> colors;

            if (_route.Color.HasValue)
            {
                colors = new[] { _route.Color.Value, TrainCarColor.Locomotive };
            }
            else
            {
                colors = Enum.GetValues(typeof(TrainCarColor)).Cast<TrainCarColor>();
            }

            var colorList = colors.ToList();
            int count = colorList.Count;
            float btnWidth = 90f;
            float spacing = 12f;
            float totalWidth = count * btnWidth + (count - 1) * spacing;

            for (int i = 0; i < count; i++)
            {
                var color = colorList[i];
                int avail = _availableByColor.TryGetValue(color, out var c) ? c : 0;
                if (avail == 0)
                {
                    continue;
                }

                var btn = CreateButton(_availableRoot, "", GetColorSwatch(color));
                btn.name = $"ColorBtn_{color}";
                var btnRect = btn.GetComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(btnWidth, 64f);
                btnRect.anchorMin = new Vector2(0.5f, 0.5f);
                btnRect.anchorMax = new Vector2(0.5f, 0.5f);
                btnRect.pivot = new Vector2(0.5f, 0.5f);
                float x = -totalWidth * 0.5f + btnWidth * 0.5f + i * (btnWidth + spacing);
                btnRect.anchoredPosition = new Vector2(x, 0f);

                // 按钮上叠加颜色名 + 数量文字。
                var nameText = CreateLabel(btn.transform, "Name", 14, TextAnchor.MiddleCenter);
                nameText.text = ColorName(color);
                nameText.color = Color.white;
                var nameRect = nameText.rectTransform;
                nameRect.anchorMin = Vector2.zero;
                nameRect.anchorMax = Vector2.one;
                nameRect.offsetMin = new Vector2(0f, 14f);
                nameRect.offsetMax = Vector2.zero;

                var countText = CreateLabel(btn.transform, "Count", 16, TextAnchor.MiddleCenter);
                countText.text = $"×{avail}";
                countText.color = new Color(1f, 0.92f, 0.3f, 1f);
                var countRect = countText.rectTransform;
                countRect.anchorMin = Vector2.zero;
                countRect.anchorMax = Vector2.one;
                countRect.offsetMin = Vector2.zero;
                countRect.offsetMax = new Vector2(0f, -14f);

                var captured = color;
                btn.onClick.AddListener(() => OnColorSelected(captured));
            }
        }

        private void OnColorSelected(TrainCarColor color)
        {
            int avail = _availableByColor.TryGetValue(color, out var c) ? c : 0;
            int selected = _selectedByColor.TryGetValue(color, out var s) ? s : 0;

            if (selected >= avail)
            {
                return;
            }

            // 灰色路线：选了一种非火车头颜色后，其它非火车头颜色不能再选。
            if (!_route.Color.HasValue && color != TrainCarColor.Locomotive)
            {
                if (_greyChosenColor.HasValue && _greyChosenColor.Value != color)
                {
                    return;
                }

                _greyChosenColor = color;
            }

            _selectedByColor[color] = selected + 1;
            UpdateSelectionView();
        }

        private void OnSelectedRemoved(TrainCarColor color)
        {
            if (!_selectedByColor.TryGetValue(color, out var selected) || selected <= 0)
            {
                return;
            }

            selected--;
            if (selected <= 0)
            {
                _selectedByColor.Remove(color);
                if (!_route.Color.HasValue && _greyChosenColor == color)
                {
                    _greyChosenColor = null;
                }
            }
            else
            {
                _selectedByColor[color] = selected;
            }

            UpdateSelectionView();
        }

        private void UpdateSelectionView()
        {
            foreach (Transform child in _selectedRoot)
            {
                Destroy(child.gameObject);
            }

            int totalSelected = _selectedByColor.Values.Sum();
            string colorDesc = _route.Color.HasValue ? _route.Color.Value.ToString() : "灰色(任选)";
            _titleLabel.text = $"认领路线：{_route.StartCity} → {_route.EndCity}（{_route.Length} 节，{colorDesc}）";

            bool canClaim = totalSelected == _route.Length;
            bool hasEnough = HasEnoughCards();

            if (!hasEnough)
            {
                _statusLabel.text = $"手牌不足：缺 {_route.Length - MaxClaimable()} 张可用卡";
                _statusLabel.color = new Color(1f, 0.4f, 0.4f, 1f);
            }
            else if (totalSelected == 0)
            {
                _statusLabel.text = $"请选择 {_route.Length} 张卡";
                _statusLabel.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            }
            else if (canClaim)
            {
                _statusLabel.text = "已选满，可以认领";
                _statusLabel.color = new Color(0.4f, 1f, 0.4f, 1f);
            }
            else
            {
                _statusLabel.text = $"已选 {totalSelected}/{_route.Length}，还差 {_route.Length - totalSelected} 张";
                _statusLabel.color = new Color(1f, 0.85f, 0.3f, 1f);
            }

            // 渲染已选卡牌（按颜色分组，可点击移除）。
            float cardWidth = 54f;
            float spacing = 8f;
            int selCount = _selectedByColor.Values.Sum();
            float totalWidth = selCount * cardWidth + Mathf.Max(0, selCount - 1) * spacing;
            int idx = 0;

            foreach (var pair in _selectedByColor)
            {
                for (int n = 0; n < pair.Value; n++)
                {
                    var btn = CreateButton(_selectedRoot, "", GetColorSwatch(pair.Key));
                    btn.name = $"Sel_{pair.Key}_{n}";
                    var r = btn.GetComponent<RectTransform>();
                    r.sizeDelta = new Vector2(cardWidth, 60f);
                    r.anchorMin = new Vector2(0.5f, 0.5f);
                    r.anchorMax = new Vector2(0.5f, 0.5f);
                    r.pivot = new Vector2(0.5f, 0.5f);
                    float x = -totalWidth * 0.5f + cardWidth * 0.5f + idx * (cardWidth + spacing);
                    r.anchoredPosition = new Vector2(x, 0f);

                    var label = CreateLabel(btn.transform, "L", 13, TextAnchor.MiddleCenter);
                    label.text = ColorName(pair.Key);
                    label.color = Color.white;
                    var lr = label.rectTransform;
                    lr.anchorMin = Vector2.zero;
                    lr.anchorMax = Vector2.one;
                    lr.offsetMin = Vector2.zero;
                    lr.offsetMax = Vector2.zero;

                    var captured = pair.Key;
                    btn.onClick.AddListener(() => OnSelectedRemoved(captured));
                    idx++;
                }
            }

            _confirmButton.interactable = canClaim;
            _confirmLabel.color = canClaim ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }

        /// <summary>玩家手牌最多能凑出多少张可用卡（用于判断是否缺卡）。</summary>
        private int MaxClaimable()
        {
            int locos = _availableByColor.TryGetValue(TrainCarColor.Locomotive, out var l) ? l : 0;

            if (_route.Color.HasValue)
            {
                int same = _availableByColor.TryGetValue(_route.Color.Value, out var c) ? c : 0;
                return same + locos;
            }

            int maxNonLoco = 0;
            foreach (var pair in _availableByColor)
            {
                if (pair.Key == TrainCarColor.Locomotive)
                {
                    continue;
                }

                if (pair.Value > maxNonLoco)
                {
                    maxNonLoco = pair.Value;
                }
            }

            return maxNonLoco + locos;
        }

        private bool HasEnoughCards()
        {
            return MaxClaimable() >= _route.Length;
        }

        private void OnConfirmClicked()
        {
            if (_selectedByColor.Values.Sum() != _route.Length)
            {
                return;
            }

            var cards = BuildSelectedCards();
            if (cards == null)
            {
                return;
            }

            _onConfirm?.Invoke(cards);
            Close();
        }

        private void OnCancelClicked()
        {
            _onCancel?.Invoke();
            Close();
        }

        private void Close()
        {
            Destroy(gameObject);
        }

        /// <summary>根据选中的颜色计数，从手牌里取出对应数量的真实卡牌对象。</summary>
        private List<TrainCarCard> BuildSelectedCards()
        {
            var result = new List<TrainCarCard>();
            var hand = _player.Hand.GetCards().ToList();

            foreach (var pair in _selectedByColor)
            {
                int need = pair.Value;
                var matching = hand.Where(c => c != null && c.Color == pair.Key).Take(need).ToList();

                if (matching.Count < need)
                {
                    return null;
                }

                result.AddRange(matching);
            }

            return result;
        }

        // —————————————— UI 辅助方法 ——————————————

        private static Text CreateLabel(Transform parent, string name, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string labelText, Color bgColor)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = bgColor;

            var label = CreateLabel(go.transform, "Label", 16, TextAnchor.MiddleCenter);
            label.text = labelText;
            label.color = Color.white;
            var lr = label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        private static void LayoutButton(RectTransform rect, float xOffset)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(xOffset, 12f);
            rect.sizeDelta = new Vector2(120f, 38f);
        }

        private static Color GetColorSwatch(TrainCarColor color)
        {
            switch (color)
            {
                case TrainCarColor.Red: return new Color(0.85f, 0.2f, 0.2f, 1f);
                case TrainCarColor.Orange: return new Color(0.95f, 0.55f, 0.15f, 1f);
                case TrainCarColor.Yellow: return new Color(0.95f, 0.85f, 0.2f, 1f);
                case TrainCarColor.Green: return new Color(0.25f, 0.7f, 0.3f, 1f);
                case TrainCarColor.Blue: return new Color(0.2f, 0.45f, 0.9f, 1f);
                case TrainCarColor.Pink: return new Color(0.9f, 0.4f, 0.7f, 1f);
                case TrainCarColor.Black: return new Color(0.18f, 0.18f, 0.2f, 1f);
                case TrainCarColor.White: return new Color(0.9f, 0.92f, 0.92f, 1f);
                case TrainCarColor.Locomotive: return new Color(0.6f, 0.12f, 0.12f, 1f);
                default: return Color.gray;
            }
        }

        private static string ColorName(TrainCarColor color)
        {
            return color switch
            {
                TrainCarColor.Red => "红",
                TrainCarColor.Orange => "橙",
                TrainCarColor.Yellow => "黄",
                TrainCarColor.Green => "绿",
                TrainCarColor.Blue => "蓝",
                TrainCarColor.Pink => "粉",
                TrainCarColor.Black => "黑",
                TrainCarColor.White => "白",
                TrainCarColor.Locomotive => "车头",
                _ => color.ToString()
            };
        }
    }
}
