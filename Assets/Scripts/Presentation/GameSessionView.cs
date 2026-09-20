using UnityEngine;
using UnityEngine.UI;
using TicketToRide.Application;
using TicketToRide.Domain;
using System.Linq;

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

        private void Awake()
        {
            _gameSession = GameSession.CreateDemoSession();

            if (drawTrainCardButton != null)
            {
                drawTrainCardButton.onClick.AddListener(OnDrawTrainCardClicked);
            }

            if (trainCardDeckView != null)
            {
                trainCardDeckView.OnDeckClicked += OnDeckClicked;
            }

            if (trainCardMarketView != null)
            {
                trainCardMarketView.Bind(_gameSession);
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
                RefreshViews();
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
                var taken = _gameSession.TurnManager.AcquireTrainCards(new System.Collections.Generic.List<TrainCarCard>(), 1);
                Debug.Log($"[GameSessionView] 点击牌堆抽到: {string.Join(", ", taken.Select(c => c.ToString()))}");
                RefreshViews();
                if (trainCardMarketView != null)
                {
                    trainCardMarketView.Refresh();
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[GameSessionView] 牌堆点击抽牌失败: {exception.Message}");
            }
        }

        private void RefreshViews()
        {
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

            if (trainCardMarketView != null)
            {
                trainCardMarketView.Refresh();
            }
        }
    }
}
