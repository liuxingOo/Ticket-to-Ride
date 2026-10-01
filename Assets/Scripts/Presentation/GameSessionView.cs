using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TicketToRide.Application;
using TicketToRide.Application.Net.Protocol;
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
        [SerializeField] private DestinationHandView destinationHandView;
        [SerializeField] private DestinationDeckView destinationDeckView;
        [SerializeField] private PlayerStatusBar playerStatusBar;

        private GameSession _gameSession;
        private IGameController _controller;
        private bool _destinationPanelOpen;
        private bool _preGameSetupDone;
        private bool _finalScoreShown;

        private RectTransform _turnHudRoot;
        private Text _turnLabel;
        private Text _turnPhaseLabel;

        /// <summary>当前游戏会话（供认领控制器等其它表现层组件使用）。</summary>
        public GameSession Session => _gameSession;

        private void Awake()
        {
            // 会话不在这里创建：开局要先让玩家选人数（Start 里弹面板），
            // 选完才 CreateSession，然后逐个选色、发牌、选任务卡。
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
                trainCardMarketView.CardsAcquired += OnCardsAcquired;
            }

            if (destinationHandView == null)
            {
                destinationHandView = FindAnyObjectByType<DestinationHandView>();
            }

            if (destinationDeckView == null)
            {
                destinationDeckView = FindAnyObjectByType<DestinationDeckView>();
            }

            if (playerStatusBar == null)
            {
                playerStatusBar = FindAnyObjectByType<PlayerStatusBar>();
            }

            if (destinationDeckView != null)
            {
                destinationDeckView.OnDeckClicked += OnDestinationDeckClicked;
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
        }

        private void Start()
        {
            // 开局流程：选人数 → 逐个选列车颜色 → 发初始车票 → 每位玩家依次选任务卡 → 进入第一回合。
            // 放在 Start：此时场景里手动创建的 SpriteLibrary / DestinationHandView 等都已 Awake。
            OpenPlayerCountSelect();
        }

        /// <summary>开局第一步：选择玩家人数（2–5 人）。</summary>
        private void OpenPlayerCountSelect()
        {
            var canvas = GetComponentInParent<Canvas>();
            var panel = PlayerCountSelectPanel.Open(canvas, BeginSessionSetup);

            if (panel == null)
            {
                Debug.LogError("[GameSessionView] 玩家人数选择面板创建失败（canvas 为空？），默认按 4 人开局。");
                BeginSessionSetup(4);
            }
        }

        /// <summary>选定人数后真正创建会话，并把表现层组件接到新会话上。</summary>
        private void BeginSessionSetup(int playerCount)
        {
            _gameSession = GameSession.CreateSession(playerCount);
            Debug.Log($"[GameSessionView] 已创建 {playerCount} 人局：{string.Join("、", _gameSession.GameState.Players.ConvertAll(p => p.Name))}");

            // 动作与回合推进都交给控制器：表现层不再自己跑协程换人。
            _controller = new LocalHotseatController(_gameSession);
            _controller.Changed += OnControllerChanged;
            gameObject.AddComponent<GameLoopDriver>().Initialize(_controller);

            if (trainCardMarketView != null)
            {
                trainCardMarketView.Bind(_gameSession);
            }

            var claimController = FindAnyObjectByType<RouteClaimController>();
            if (claimController != null)
            {
                claimController.Initialize(_gameSession, this);
            }
            else
            {
                Debug.LogError("[GameSessionView] 场景里没有 RouteClaimController，认领功能不可用。");
            }

            BuildTurnHud(GetComponentInParent<Canvas>());
            RefreshViews();

            BeginPlayerColorSelection(0, new HashSet<PlayerTrainColor>());
        }

        /// <summary>
        /// 右下角的回合面板：只显示当前玩家和本回合动作状态。
        /// 一回合只做一个动作，动作做完回合即结束，因此不设「结束回合」按钮，由本组件自动换人。
        /// </summary>
        private void BuildTurnHud(Canvas canvas)
        {
            if (canvas == null || _turnHudRoot != null)
            {
                return;
            }

            var root = new GameObject("TurnHud", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);

            _turnHudRoot = (RectTransform)root.transform;
            _turnHudRoot.anchorMin = new Vector2(1f, 0f);
            _turnHudRoot.anchorMax = new Vector2(1f, 0f);
            _turnHudRoot.pivot = new Vector2(1f, 0f);
            _turnHudRoot.anchoredPosition = new Vector2(-24f, 24f);
            _turnHudRoot.sizeDelta = new Vector2(300f, 76f);
            root.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.92f);

            _turnLabel = ClaimPanel.CreateLabelPublic(_turnHudRoot, "TurnLabel", 18, TextAnchor.MiddleCenter);
            _turnLabel.color = Color.white;
            _turnLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _turnLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _turnLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            _turnLabel.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            _turnLabel.rectTransform.sizeDelta = new Vector2(276f, 28f);

            _turnPhaseLabel = ClaimPanel.CreateLabelPublic(_turnHudRoot, "TurnPhase", 14, TextAnchor.MiddleCenter);
            _turnPhaseLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _turnPhaseLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _turnPhaseLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            _turnPhaseLabel.rectTransform.anchoredPosition = new Vector2(0f, -44f);
            _turnPhaseLabel.rectTransform.sizeDelta = new Vector2(276f, 22f);
        }

        private void RefreshTurnHud()
        {
            if (_gameSession == null || _turnLabel == null || _turnPhaseLabel == null)
            {
                return;
            }

            var state = _gameSession.GameState;
            _turnPhaseLabel.color = new Color(1f, 1f, 1f, 0.65f);

            if (state.IsGameOver)
            {
                _turnLabel.text = "游戏结束";
                _turnPhaseLabel.text = "等待结算";
                return;
            }

            var player = state.CurrentPlayer;
            _turnLabel.text = $"当前回合：{player.Name}（{PlayerColorSelectPanel.GetColorName(player.Color)}）";

            switch (state.TurnPhase)
            {
                case TurnActionPhase.AwaitingAction:
                    _turnPhaseLabel.text = "可抽牌 / 认领路线 / 抽任务卡";
                    break;
                case TurnActionPhase.DrawingSecondCard:
                    _turnPhaseLabel.text = "还需再抽 1 张车厢卡";
                    break;
                default:
                    var next = state.NextPlayer;
                    _turnPhaseLabel.text =
                        $"动作完成，轮到 {next.Name}（{PlayerColorSelectPanel.GetColorName(next.Color)}）";
                    break;
            }
        }

        private void OnControllerChanged()
        {
            RefreshViews();

            if (_gameSession == null)
            {
                return;
            }

            var state = _gameSession.GameState;
            if (state.IsGameOver)
            {
                if (_finalScoreShown)
                {
                    return;
                }

                _finalScoreShown = true;
                var result = FinalScoreCalculator.Calculate(state);
                Debug.Log($"[GameSessionView] 游戏结束。{string.Join("；", result.Rankings.Select(entry => $"{entry.Player.Name} {entry.Total} 分"))}");
                FinalScorePanel.Open(GetComponentInParent<Canvas>(), result);
                return;
            }

            if (state.TurnPhase == TurnActionPhase.AwaitingAction)
            {
                var current = state.CurrentPlayer;
                Debug.Log($"[GameSessionView] 轮到 {current.Name}（{PlayerColorSelectPanel.GetColorName(current.Color)}）。" +
                          (state.IsFinalRound ? $"最后一轮，还剩 {state.FinalRoundTurnsRemaining} 回合。" : string.Empty));
            }
        }

        /// <summary>递归式地为每位玩家依次弹出选色面板；takenColors 记录已被选走的颜色。</summary>
        private void BeginPlayerColorSelection(int playerIndex, HashSet<PlayerTrainColor> takenColors)
        {
            var players = _gameSession.GameState.Players;
            if (playerIndex >= players.Count)
            {
                FinishPreGameSetup();
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            var panel = PlayerColorSelectPanel.Open(canvas, players[playerIndex], takenColors, color =>
            {
                players[playerIndex].SetTrainColor(color);
                takenColors.Add(color);
                Debug.Log($"[GameSessionView] {players[playerIndex].Name} 选择了 {color} 色列车。");

                // 每选完一人立刻刷新状态栏，边框/图标/ID 颜色即时更新。
                RefreshPlayerInfo();
                BeginPlayerColorSelection(playerIndex + 1, takenColors);
            });

            if (panel == null)
            {
                Debug.LogError("[GameSessionView] 列车颜色选择面板创建失败（canvas 为空？），跳过选色直接发牌。");
                FinishPreGameSetup();
            }
        }

        /// <summary>所有玩家颜色选完：重排行动顺序 → 发初始车票 → 刷新视图 → 依次挑选任务卡。</summary>
        private void FinishPreGameSetup()
        {
            if (_preGameSetupDone)
            {
                return;
            }

            _preGameSetupDone = true;

            // 按 红→黄→蓝→绿→紫 重排行动顺序，缺失的颜色直接跳过（如 4 人无蓝即 红→黄→绿→紫）。
            _gameSession.GameState.RebuildTurnOrderByColor();
            Debug.Log("[GameSessionView] 行动顺序：" + string.Join(" → ",
                _gameSession.GameState.Players.ConvertAll(p => $"{p.Name}（{PlayerColorSelectPanel.GetColorName(p.Color)}）")));

            _gameSession.DealInitialTrainCards();
            RefreshViews();
            OpenInitialDestinationDraft(0);
        }

        /// <summary>开局发 3 张任务卡，每位玩家依次至少留 2 张，弃的回牌堆底。</summary>
        private void OpenInitialDestinationDraft(int playerIndex)
        {
            if (_gameSession == null || _gameSession.GameState == null)
            {
                return;
            }

            var players = _gameSession.GameState.Players;
            if (playerIndex >= players.Count)
            {
                Debug.Log("[GameSessionView] 开局流程结束，进入第一回合。");
                RefreshPlayerInfo();
                return;
            }

            var player = players[playerIndex];
            var drawn = _gameSession.GameState.DestinationTicketDeck.DrawMany(3);
            if (drawn.Count == 0)
            {
                Debug.LogWarning("[GameSessionView] 任务牌堆是空的，跳过开局挑选。");
                OpenInitialDestinationDraft(playerIndex + 1);
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            _destinationPanelOpen = true;
            var panel = DestinationDraftPanel.Open(canvas, drawn, 2,
                $"{player.Name} 的开局任务卡：点选要保留的，至少保留 2 张", kept =>
            {
                _destinationPanelOpen = false;
                var discarded = drawn.Where(t => !kept.Contains(t)).ToList();
                // 开局挑选不属于回合动作，不消耗回合状态。
                _gameSession.TurnManager.ChooseDestinationTickets(player, kept, discarded, false);

                Debug.Log($"[GameSessionView] {player.Name} 任务卡挑选完成：保留 {kept.Count} 张" +
                          $"（{string.Join("、", kept.ConvertAll(t => $"{t.StartCity}-{t.EndCity}"))}），" +
                          $"弃 {discarded.Count} 张回牌堆" +
                          (discarded.Count > 0 ? "并洗牌。" : "。"));
                RefreshPlayerInfo();
                OpenInitialDestinationDraft(playerIndex + 1);
            });

            if (panel == null)
            {
                _destinationPanelOpen = false;
                Debug.LogError("[GameSessionView] 开局任务卡面板创建失败（canvas 为空？）。");
                OpenInitialDestinationDraft(playerIndex + 1);
            }
        }

        /// <summary>游戏中点击任务卡牌堆：抽 3 张，至少留 1 张，弃的回牌堆并洗牌。</summary>
        private void OnDestinationDeckClicked()
        {
            if (_gameSession == null || _destinationPanelOpen)
            {
                return;
            }

            if (_gameSession.GameState.IsGameOver)
            {
                Debug.LogWarning("[GameSessionView] 游戏已经结束，不能再抽任务卡。");
                return;
            }

            // 一回合只能做一个动作：抽选任务卡也算一个动作。
            if (_gameSession.GameState.TurnPhase != TurnActionPhase.AwaitingAction)
            {
                Debug.LogWarning("[GameSessionView] 本回合已经执行过动作，不能再抽任务卡。");
                return;
            }

            var deck = _gameSession.GameState.DestinationTicketDeck;
            if (deck.IsEmpty)
            {
                Debug.Log("[GameSessionView] 任务牌堆已空，无法抽选。");
                return;
            }

            var drawn = deck.DrawMany(3);
            if (drawn.Count == 0)
            {
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            _destinationPanelOpen = true;
            var panel = DestinationDraftPanel.Open(canvas, drawn, 1, "抽选任务卡：点选要保留的，至少保留 1 张", kept =>
            {
                _destinationPanelOpen = false;
                var discarded = drawn.Where(t => !kept.Contains(t)).ToList();
                _gameSession.TurnManager.ChooseDestinationTickets(_gameSession.GameState.CurrentPlayer, kept, discarded);

                Debug.Log($"[GameSessionView] 抽选任务卡：保留 {kept.Count} 张" +
                          $"（{string.Join("、", kept.ConvertAll(t => $"{t.StartCity}-{t.EndCity}"))}），" +
                          $"弃 {discarded.Count} 张回牌堆" +
                          (discarded.Count > 0 ? "并洗牌。" : "。"));
                RefreshPlayerInfo();
            });

            if (panel == null)
            {
                _destinationPanelOpen = false;
                Debug.LogError("[GameSessionView] 抽选任务卡面板创建失败（canvas 为空？）。");
            }
        }

        private void OnDestroy()
        {
            if (drawTrainCardButton != null)
            {
                drawTrainCardButton.onClick.RemoveListener(OnDrawTrainCardClicked);
            }

            if (destinationDeckView != null)
            {
                destinationDeckView.OnDeckClicked -= OnDestinationDeckClicked;
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

            if (_controller != null)
            {
                _controller.Changed -= OnControllerChanged;
            }
        }

        public void OnDrawTrainCardClicked()
        {
            SubmitDrawTrainCard();
        }

        private void OnDeckClicked()
        {
            SubmitDrawTrainCard();
        }

        /// <summary>抽车厢卡改走控制器：单机立即执行，联机时同一入口会变成发给主机。</summary>
        private void SubmitDrawTrainCard()
        {
            if (_controller == null || _gameSession == null)
            {
                Debug.LogError("[GameSessionView] 游戏尚未初始化。");
                return;
            }

            _controller.Submit(new GameCommand { Kind = CommandKind.DrawTrainCard });
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

            if (destinationHandView != null)
            {
                destinationHandView.Refresh(_gameSession.GameState.CurrentPlayer);
            }

            if (destinationDeckView != null)
            {
                destinationDeckView.Refresh(_gameSession.GameState.DestinationTicketDeck.Count);
            }

            if (playerStatusBar != null)
            {
                playerStatusBar.Refresh(_gameSession.GameState.Players);
            }

            RefreshTurnHud();
        }

        /// <summary>公开刷新接口：认领路线后手牌变化时由外部调用。</summary>
        public void RefreshHand()
        {
            RefreshPlayerInfo();
        }
    }
}

