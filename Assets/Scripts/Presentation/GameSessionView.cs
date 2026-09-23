using UnityEngine;
using UnityEngine.UI;
using TicketToRide.Application;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    public class GameSessionView : MonoBehaviour
    {
        [SerializeField] private DeckCountView deckCountView;
        [SerializeField] private PlayerHandView playerHandView;
        [SerializeField] private TrainCardMarketView trainCardMarketView;
        [SerializeField] private TrainCardDeckView trainCardDeckView;
        [SerializeField] private Button drawTrainCardButton;

        private GameSession _gameSession;

        /// <summary>当前游戏会话（供认领控制器等其它表现层组件使用）。</summary>
        public GameSession Session => _gameSession;

        private void Awake()
        {
            _gameSession = GameSession.CreateDemoSession();

            // Inspector 没有手动拖线时，自动在场景中查找，保证当前开发阶段直接能运行。
            if (trainCardMarketView == null)
            {
                trainCardMarketView = FindAnyObjectByType<TrainCardMarketView>();
                if (trainCardMarketView != null)
                {
                    Debug.LogWarning("[GameSessionView] trainCardMarketView 未在 Inspector 指定，已自动绑定场景中的市场视图。建议之后手动拖线。");
                }
            }

            if (trainCardDeckView == null)
            {
                trainCardDeckView = FindAnyObjectByType<TrainCardDeckView>();
                if (trainCardDeckView != null)
                {
                    Debug.LogWarning("[GameSessionView] trainCardDeckView 未在 Inspector 指定，已自动绑定场景中的牌堆视图。建议之后手动拖线。");
                }
            }

            if (drawTrainCardButton != null)
            {
                drawTrainCardButton.onClick.AddListener(OnDrawTrainCardClicked);
            }

            if (trainCardDeckView != null)
            {
                trainCardDeckView.OnDeckClicked += OnDeckClicked;
            }

            if (deckCountView != null)
            {
                deckCountView.OnDeckClicked += OnDeckClicked;
            }

            if (trainCardMarketView != null)
            {
                trainCardMarketView.Bind(_gameSession);
                trainCardMarketView.CardsAcquired += OnCardsAcquired;
            }

            // 高亮映射组件需要手动挂在场景里（运行时创建的话 Play→Stop 就丢了）。
            if (FindAnyObjectByType<HighlightSpriteMapper>() == null)
            {
                Debug.LogWarning("[GameSessionView] 场景里没找到 HighlightSpriteMapper！" +
                                 "请在 Hierarchy 里右键 → UI → 空物体，挂 HighlightSpriteMapper 组件，" +
                                 "把 road-highlight 的 9 个切片拖进去后 Ctrl+S 保存场景。");
            }

            // 自动挂载认领控制器，省去手动拖组件的步骤。
            if (FindAnyObjectByType<RouteClaimController>() == null)
            {
                var controllerObject = new GameObject("RouteClaimController");
                controllerObject.transform.SetParent(transform, false);
                controllerObject.AddComponent<RouteClaimController>();
            }

            RefreshViews();
        }

        private void OnDestroy()
        {
            if (drawTrainCardButton != null)
            {
                drawTrainCardButton.onClick.RemoveListener(OnDrawTrainCardClicked);
            }

            if (trainCardDeckView != null)
            {
                trainCardDeckView.OnDeckClicked -= OnDeckClicked;
            }

            if (deckCountView != null)
            {
                deckCountView.OnDeckClicked -= OnDeckClicked;
            }

            if (trainCardMarketView != null)
            {
                trainCardMarketView.CardsAcquired -= OnCardsAcquired;
            }
        }

        public void OnDrawTrainCardClicked()
        {
            if (_gameSession == null)
            {
                Debug.LogError("[GameSessionView] 游戏尚未初始化。");
                return;
            }

            try
            {
                var card = _gameSession.TurnManager.DrawTrainCard();
                Debug.Log($"[GameSessionView] 当前玩家抽到: {card}");
                RefreshPlayerInfo();
            }
            catch (System.InvalidOperationException exception)
            {
                Debug.LogWarning($"[GameSessionView] 无法抽牌: {exception.Message}");
            }
        }

        private void OnDeckClicked()
        {
            if (_gameSession == null)
            {
                return;
            }

            try
            {
                var card = _gameSession.TurnManager.DrawTrainCard();
                Debug.Log($"[GameSessionView] 点击牌堆抽到: {card}");
                RefreshPlayerInfo();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[GameSessionView] 牌堆点击抽牌失败: {exception.Message}");
            }
        }

        private void OnCardsAcquired()
        {
            RefreshPlayerInfo();
        }

        private void RefreshViews()
        {
            RefreshPlayerInfo();

            if (trainCardMarketView != null)
            {
                trainCardMarketView.Refresh();
            }
        }

        private void RefreshPlayerInfo()
        {
            if (_gameSession == null)
            {
                return;
            }

            if (deckCountView != null)
            {
                deckCountView.Refresh(_gameSession.GameState.TrainCarDeck);
            }
            else
            {
                Debug.LogWarning("[GameSessionView] 没有绑定 DeckCountView。");
            }

            if (playerHandView != null)
            {
                playerHandView.Refresh(_gameSession.GameState.CurrentPlayer);
            }
            else
            {
                Debug.LogWarning("[GameSessionView] 没有绑定 PlayerHandView。");
            }
        }

        /// <summary>公开刷新接口：认领路线后手牌变化时由外部调用。</summary>
        public void RefreshHand()
        {
            RefreshPlayerInfo();
        }
    }
}

