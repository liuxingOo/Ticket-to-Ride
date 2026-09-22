using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    public class TrainCarCardView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image cardImage;
        [SerializeField] private Image borderImage;

        private TrainCarCard _card;
        private GameObject _countBadge;
        private Text _countLabel;
        private RectTransform _countBadgeRect;
        private RectTransform _clickArea;
        private float _visibleWidth;
        private float _visibleHeight;

        public event Action<TrainCarCard> OnCardClicked;

        public TrainCarCard Card => _card;

        public void Initialize(TrainCarCard card, Sprite sprite)
        {
            _card = card;

            if (card == null)
            {
                Debug.LogError("[TrainCarCardView] 不能初始化空车票卡牌。");
                return;
            }

            if (cardImage != null)
            {
                cardImage.sprite = sprite;
                cardImage.preserveAspect = true;

                // 卡牌 Image 的矩形比可见贴图大（横版图上下有大片留白），
                // 让它本身不接收点击，点击交给与可见贴图等大的 ClickArea 子物体。
                cardImage.raycastTarget = false;
                ConfigureClickArea(sprite);
            }
            else
            {
                Debug.LogWarning("[TrainCarCardView] 没有绑定 Image 组件，无法显示卡牌切片。");
            }

            if (borderImage != null)
            {
                borderImage.color = GetBorderColor(card.Color);
            }

            SetCount(1);
        }

        private void ConfigureClickArea(Sprite sprite)
        {
            EnsureClickArea();

            var cardRect = cardImage.rectTransform.rect;

            float clickWidth = cardRect.width;
            float clickHeight = cardRect.height;

            if (sprite != null)
            {
                // 与 Image 在 preserveAspect（等比缩放、居中）下实际显示的区域保持一致。
                Vector2 spriteSize = sprite.bounds.size * sprite.pixelsPerUnit;
                if (spriteSize.x > 0f && spriteSize.y > 0f)
                {
                    float scale = Mathf.Min(cardRect.width / spriteSize.x, cardRect.height / spriteSize.y);
                    clickWidth = spriteSize.x * scale;
                    clickHeight = spriteSize.y * scale;
                }
            }

            _visibleWidth = clickWidth;
            _visibleHeight = clickHeight;

            _clickArea.anchorMin = new Vector2(0.5f, 0.5f);
            _clickArea.anchorMax = new Vector2(0.5f, 0.5f);
            _clickArea.pivot = new Vector2(0.5f, 0.5f);
            _clickArea.anchoredPosition = Vector2.zero;
            _clickArea.sizeDelta = new Vector2(clickWidth, clickHeight);
        }

        private void EnsureClickArea()
        {
            if (_clickArea != null)
            {
                return;
            }

            var clickAreaObject = new GameObject("ClickArea", typeof(RectTransform), typeof(Image));
            clickAreaObject.transform.SetParent(transform, false);

            var image = clickAreaObject.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            _clickArea = (RectTransform)clickAreaObject.transform;
            _clickArea.SetAsFirstSibling();
        }

        public void SetCount(int count)
        {
            if (count <= 1)
            {
                if (_countBadge != null)
                {
                    _countBadge.SetActive(false);
                }

                return;
            }

            EnsureCountBadge();
            UpdateBadgeLayout();
            _countBadge.SetActive(true);
            _countLabel.text = $"×{count}";
        }

        private void UpdateBadgeLayout()
        {
            if (_countBadgeRect == null)
            {
                return;
            }

            var cardRect = (RectTransform)transform;

            // 角标永远放在“观众视角”可见画面的正上方、水平居中，
            // 这样手牌横排时不会被右侧邻牌挡住，市场竖排时也不会互相遮挡。
            float rotationZ = cardRect.localEulerAngles.z;
            float theta = rotationZ * Mathf.Deg2Rad;
            float cos = Mathf.Cos(theta);
            float sin = Mathf.Sin(theta);

            float halfWidth = (_visibleWidth > 0f ? _visibleWidth : cardRect.rect.width) / 2f;
            float halfHeight = (_visibleHeight > 0f ? _visibleHeight : cardRect.rect.height) / 2f;

            // 屏幕正上方在卡牌本地坐标中的方向是 (sinθ, cosθ)。
            // 可见画面顶边中点对应的本地坐标：
            float edgeSignX = AxisSign(sin);
            float edgeSignY = AxisSign(cos);
            float edgeX = halfWidth * edgeSignX;
            float edgeY = halfHeight * edgeSignY;

            // 沿屏幕上方外移 6 像素（换算到本地坐标），让角标浮在画面顶边外。
            const float margin = 6f;
            float offsetX = margin * sin;
            float offsetY = margin * cos;

            _countBadgeRect.pivot = new Vector2(0.5f, 0f);
            _countBadgeRect.anchoredPosition = new Vector2(edgeX + offsetX, edgeY + offsetY);
            _countBadgeRect.localEulerAngles = new Vector3(0f, 0f, -rotationZ);
        }

        private static float AxisSign(float value)
        {
            if (value > 0.001f)
            {
                return 1f;
            }

            if (value < -0.001f)
            {
                return -1f;
            }

            return 0f;
        }

        private void EnsureCountBadge()
        {
            if (_countBadge != null)
            {
                return;
            }

            _countBadge = new GameObject("CountBadge", typeof(RectTransform), typeof(Image));
            _countBadge.transform.SetParent(transform, false);

            var badgeRect = (RectTransform)_countBadge.transform;
            badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = Vector2.zero;
            badgeRect.sizeDelta = new Vector2(52f, 34f);
            _countBadgeRect = badgeRect;

            var badgeImage = _countBadge.GetComponent<Image>();
            badgeImage.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
            badgeImage.raycastTarget = false;

            var labelObject = new GameObject("CountLabel", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(_countBadge.transform, false);

            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            _countLabel = labelObject.GetComponent<Text>();
            _countLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _countLabel.fontSize = 22;
            _countLabel.fontStyle = FontStyle.Bold;
            _countLabel.alignment = TextAnchor.MiddleCenter;
            _countLabel.color = Color.white;
            _countLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _countLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _countLabel.raycastTarget = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_card == null)
            {
                return;
            }

            OnCardClicked?.Invoke(_card);
        }

        private Color GetBorderColor(TrainCarColor color)
        {
            switch (color)
            {
                case TrainCarColor.Red:
                    return new Color(0.85f, 0.25f, 0.25f, 1f);
                case TrainCarColor.Orange:
                    return new Color(0.9f, 0.6f, 0.2f, 1f);
                case TrainCarColor.Yellow:
                    return new Color(0.9f, 0.85f, 0.3f, 1f);
                case TrainCarColor.Green:
                    return new Color(0.2f, 0.75f, 0.35f, 1f);
                case TrainCarColor.Blue:
                    return new Color(0.2f, 0.45f, 0.9f, 1f);
                case TrainCarColor.Purple:
                    return new Color(0.58f, 0.28f, 0.72f, 1f);
                case TrainCarColor.Black:
                    return new Color(0.18f, 0.18f, 0.18f, 1f);
                case TrainCarColor.White:
                    return new Color(0.9f, 0.92f, 0.92f, 1f);
                case TrainCarColor.Locomotive:
                    return new Color(0.6f, 0.12f, 0.12f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
