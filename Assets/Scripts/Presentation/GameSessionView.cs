using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TicketToRide.Application;
using TicketToRide.Application.Net;
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

        /// <summary>联机时的真实网络传输（单机为 null）。</summary>
        private TcpTransport _tcpTransport;

        /// <summary>主机端会话；单机/客户端为 null。</summary>
        private HostSession _hostSession;

        /// <summary>联机时由快照驱动的面板（开局选色 / 挑任务卡 / 对局中抽任务卡）；单机或没到时为 null。</summary>
        private MonoBehaviour _draftPanel;

        private RectTransform _turnHudRoot;
        private Text _turnLabel;
        private Text _turnPhaseLabel;

        /// <summary>左上角「谁在做什么」小字（仅联机，且只在别人操作时显示）。</summary>
        private RectTransform _activityRoot;
        private Text _activityLabel;

        /// <summary>屏幕上方一闪而过的提示（主机拒绝命令等）。</summary>
        private RectTransform _toastRoot;

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
            if (NetworkBootstrap.Role == NetworkRole.Client)
            {
                StartClient();
                return;
            }

            // 单机与主机都先选人数：主机会在选完之后才开始监听端口。
            // 开局流程：选人数 → 逐个选列车颜色 → 发初始车票 → 每位玩家依次选任务卡 → 进入第一回合。
            // 放在 Start：此时场景里手动创建的 SpriteLibrary / DestinationHandView 等都已 Awake。
            OpenPlayerCountSelect();
        }

        /// <summary>
        /// 客户端：直接连主机，没有本地领域状态。
        /// 开局（选人数 / 选色 / 选任务卡）由主机阶段机驱动，面板由快照回流后弹出，
        /// 本端只提交命令、不本地推进。
        /// </summary>
        private void StartClient()
        {
            TcpTransport transport;
            try
            {
                transport = new TcpTransport(NetworkBootstrap.Ip, NetworkBootstrap.Port);
            }
            catch (System.InvalidOperationException exception)
            {
                Debug.LogError($"[GameSessionView] 加入主机失败：{exception.Message}");
                ShowNotice("加入主机失败", $"无法连接 {NetworkBootstrap.Ip}:{NetworkBootstrap.Port}\n{exception.Message}");
                return;
            }

            _tcpTransport = transport;
            var clientSession = new ClientSession(transport);
            clientSession.NoticeReceived += ShowToast;
            _controller = clientSession;
            _controller.Changed += OnControllerChanged;
            gameObject.AddComponent<GameLoopDriver>().Initialize(_controller);

            // 报到：把菜单里填的昵称告诉主机，主机按连接绑定后再覆盖公屏显示名。
            clientSession.SendHello(NetworkBootstrap.Nickname);

            if (trainCardMarketView != null)
            {
                trainCardMarketView.Bind(_controller);
            }

            // 客户端没有权威 GameState：session 传 null，本地只建路线的展示映射，
            // 点击认领时走命令通道，真正的规则裁决在主机。
            var claimController = FindAnyObjectByType<RouteClaimController>();
            if (claimController != null)
            {
                claimController.Initialize(null, this, _controller);
            }

            BuildTurnHud(GetComponentInParent<Canvas>());
            BuildActivityTicker(GetComponentInParent<Canvas>());
            RefreshViews();

            Debug.Log($"[GameSessionView] 已作为客户端连接 {NetworkBootstrap.Ip}:{NetworkBootstrap.Port}，等待主机快照。");
        }

        /// <summary>主机：建 TCP 监听，把陆续连进来的客户端按顺序分配到 player_2、player_3…</summary>
        private IGameController CreateHostController()
        {
            var transport = new TcpTransport(NetworkBootstrap.Port);
            var host = new HostSession(transport, _gameSession);
            host.AssignClient(transport.LocalClientId, _gameSession.GameState.Players[0].Id);

            int nextSlot = 1;
            transport.ClientConnected += clientId =>
            {
                var players = _gameSession.GameState.Players;
                if (nextSlot >= players.Count)
                {
                    Debug.LogWarning($"[GameSessionView] 客户端 {clientId} 接入，但 {players.Count} 人局已满，暂不分配玩家。");
                    return;
                }

                var playerId = players[nextSlot].Id;
                host.AssignClient(clientId, playerId);
                Debug.Log($"[GameSessionView] 客户端 {clientId} 加入，分配为 {playerId}。");
                nextSlot++;

                // 刚接入的客户端先拿一份当前快照，否则要等下一个动作才有画面。
                host.PublishSnapshot();
            };

            _tcpTransport = transport;
            _hostSession = host;
            return host;
        }

        /// <summary>联机时把开局的本地变更推给客户端（开局流程不走命令通道，需手动广播）。</summary>
        private void PublishHostSnapshot()
        {
            _hostSession?.PublishSnapshot();
        }

        /// <summary>
        /// 屏幕上方一闪而过的提示：主机拒绝命令时回的原因。
        /// 与 ShowNotice 的区别是它不挡操作、不用点，1.6 秒后淡出。
        /// </summary>
        private void ShowToast(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            // 同一时刻只留一条：新提示直接顶掉旧的。
            if (_toastRoot != null)
            {
                Destroy(_toastRoot.gameObject);
            }

            var root = new GameObject("CommandToast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsLastSibling();

            _toastRoot = (RectTransform)root.transform;
            _toastRoot.anchorMin = new Vector2(0.5f, 1f);
            _toastRoot.anchorMax = new Vector2(0.5f, 1f);
            _toastRoot.pivot = new Vector2(0.5f, 1f);
            _toastRoot.anchoredPosition = new Vector2(0f, -70f);
            _toastRoot.sizeDelta = new Vector2(440f, 44f);

            var background = root.GetComponent<Image>();
            background.color = new Color(0.62f, 0.18f, 0.18f, 0.92f);
            background.raycastTarget = false; // 不挡地图点击。

            var label = ClaimPanel.CreateLabelPublic(_toastRoot, "ToastLabel", 18, TextAnchor.MiddleCenter);
            label.text = text;
            label.color = Color.white;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);

            StartCoroutine(FadeToast(root.GetComponent<CanvasGroup>(), _toastRoot));
        }

        private System.Collections.IEnumerator FadeToast(CanvasGroup group, RectTransform root)
        {
            yield return new WaitForSeconds(1.6f);

            const float fadeSeconds = 0.6f;
            for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.deltaTime)
            {
                if (group == null)
                {
                    yield break; // 已经被下一条提示顶掉了。
                }

                group.alpha = 1f - elapsed / fadeSeconds;
                yield return null;
            }

            if (root != null)
            {
                Destroy(root.gameObject);
            }
        }

        /// <summary>屏幕中央弹一块提示（连接失败等）。</summary>
        private void ShowNotice(string title, string body)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var root = new GameObject("NetworkNotice", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsLastSibling();

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(820f, 220f);
            root.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.98f);

            var label = ClaimPanel.CreateLabelPublic(rootRect, "Label", 24, TextAnchor.MiddleCenter);
            label.color = Color.white;
            label.text = $"{title}\n{body}";
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(20f, 20f);
            label.rectTransform.offsetMax = new Vector2(-20f, -20f);
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
            _gameSession = GameSession.CreateSession(playerCount, NetworkBootstrap.Nickname);
            Debug.Log($"[GameSessionView] 已创建 {playerCount} 人局：{string.Join("、", _gameSession.GameState.Players.ConvertAll(p => p.Name))}");

            // 动作与回合推进都交给控制器：表现层不再自己跑协程换人。
            // 单机走本地直执行；主机走 HostSession + TCP 监听。
            if (NetworkBootstrap.Role == NetworkRole.Host)
            {
                try
                {
                    _controller = CreateHostController();
                }
                catch (System.InvalidOperationException exception)
                {
                    Debug.LogError($"[GameSessionView] 主机启动失败：{exception.Message}");
                    ShowNotice("主机启动失败", exception.Message);
                    return;
                }
            }
            else
            {
                _controller = new LocalHotseatController(_gameSession);
            }

            _controller.Changed += OnControllerChanged;
            gameObject.AddComponent<GameLoopDriver>().Initialize(_controller);

            if (trainCardMarketView != null)
            {
                trainCardMarketView.Bind(_controller);
            }

            var claimController = FindAnyObjectByType<RouteClaimController>();
            if (claimController != null)
            {
                claimController.Initialize(_gameSession, this, _controller);
            }
            else
            {
                Debug.LogError("[GameSessionView] 场景里没有 RouteClaimController，认领功能不可用。");
            }

            BuildTurnHud(GetComponentInParent<Canvas>());
            BuildActivityTicker(GetComponentInParent<Canvas>());

            if (NetworkBootstrap.Role == NetworkRole.Host)
            {
                // 联机：开局由主机阶段机驱动，选色 / 选任务卡都走命令通道，
                // 表现层只读快照。BeginPreGame 广播首份快照后，
                // Changed → UpdateDraftPanels 会按 Phase + ActingPlayerId 弹出本人该操作的面板。
                _hostSession.BeginPreGame();
                RefreshViews();
            }
            else
            {
                // 单机热座：沿用表现层依次驱动的开局流程。
                PublishHostSnapshot();
                RefreshViews();
                BeginPlayerColorSelection(0, new HashSet<PlayerTrainColor>());
            }
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
            var state = _controller?.View;
            if (state == null || _turnLabel == null || _turnPhaseLabel == null)
            {
                return;
            }

            _turnPhaseLabel.color = new Color(1f, 1f, 1f, 0.65f);

            if (state.IsGameOver)
            {
                _turnLabel.text = "游戏结束";
                _turnPhaseLabel.text = "等待结算";
                return;
            }

            // 联机开局阶段机：显示「现在该谁操作」，而不是对局的当前回合。
            if (state.Phase == MatchPhase.ColorSelect || state.Phase == MatchPhase.TicketDraft)
            {
                var actor = state.FindPlayer(state.ActingPlayerId);
                // 选色阶段颜色还没定，只报名字；选任务卡时颜色已定，带上颜色更好认。
                string actorName = actor == null
                    ? "等待玩家接入"
                    : state.Phase == MatchPhase.TicketDraft
                        ? $"{actor.Name}（{PlayerColorSelectPanel.GetColorName(actor.Color)}）"
                        : actor.Name;

                _turnLabel.text = state.Phase == MatchPhase.ColorSelect
                    ? "开局 · 选择列车颜色"
                    : "开局 · 挑选任务卡";
                _turnPhaseLabel.text = state.ActingPlayerId == state.LocalPlayerId ? "轮到你操作" : $"等待 {actorName}";
                return;
            }

            var player = state.CurrentPlayer;
            if (player == null)
            {
                // 客户端刚连上、还没收到第一份快照时，公开段是空的。
                _turnLabel.text = "等待主机同步…";
                _turnPhaseLabel.text = string.Empty;
                return;
            }

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
                    _turnPhaseLabel.text = next == null
                        ? "动作完成"
                        : $"动作完成，轮到 {next.Name}（{PlayerColorSelectPanel.GetColorName(next.Color)}）";
                    break;
            }
        }

        /// <summary>左上角「谁在做什么」小字：常驻组件，联机且别人在操作时才显示。</summary>
        private void BuildActivityTicker(Canvas canvas)
        {
            if (canvas == null || _activityRoot != null)
            {
                return;
            }

            var root = new GameObject("ActivityTicker", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);

            _activityRoot = (RectTransform)root.transform;
            _activityRoot.anchorMin = new Vector2(0f, 1f);
            _activityRoot.anchorMax = new Vector2(0f, 1f);
            _activityRoot.pivot = new Vector2(0f, 1f);
            _activityRoot.anchoredPosition = new Vector2(24f, -24f);
            _activityRoot.sizeDelta = new Vector2(300f, 34f);

            var background = root.GetComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.92f); // 浅色底：配黑字才看得清。
            background.raycastTarget = false; // 别挡住地图点击。

            _activityLabel = ClaimPanel.CreateLabelPublic(_activityRoot, "ActivityLabel", 16, TextAnchor.MiddleLeft);
            _activityLabel.color = new Color(0f, 0f, 0f, 1f);
            _activityLabel.rectTransform.anchorMin = Vector2.zero;
            _activityLabel.rectTransform.anchorMax = Vector2.one;
            _activityLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
            _activityLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);

            _activityRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// 刷新「谁在做什么」：数据全部来自快照公开段，不需要额外的网络消息。
        /// 只显示别人；轮到自己时收起（自己的状态看右下角回合面板）。
        /// </summary>
        private void RefreshActivityTicker()
        {
            if (_activityRoot == null || _activityLabel == null)
            {
                return;
            }

            var view = _controller?.View;
            if (!IsOnline || view == null)
            {
                _activityRoot.gameObject.SetActive(false);
                return;
            }

            string actorId;
            string action;

            switch (view.Phase)
            {
                case MatchPhase.ColorSelect:
                    actorId = view.ActingPlayerId;
                    action = "正在选择列车颜色";
                    break;

                case MatchPhase.TicketDraft:
                    actorId = view.ActingPlayerId;
                    action = "正在挑选任务卡";
                    break;

                case MatchPhase.GameOver:
                    _activityRoot.gameObject.SetActive(false);
                    return;

                default:
                    if (!string.IsNullOrEmpty(view.DraftingPlayerId))
                    {
                        // 对局中抽了任务卡、还没选完的人。
                        actorId = view.DraftingPlayerId;
                        action = "正在挑选任务卡";
                    }
                    else
                    {
                        actorId = view.CurrentPlayerId;
                        action = DescribeTurnPhase(view.TurnPhase);
                    }

                    break;
            }

            // 先看有没有「该操作但还没客户端接入」的座位：这比「正在行动」更该优先说明，
            // 否则主机和已连客户端会误以为卡死。WaitingPlayerId 永远不是自己，无需再判本端。
            if (!string.IsNullOrEmpty(view.WaitingPlayerId))
            {
                var waiting = view.FindPlayer(view.WaitingPlayerId);
                _activityLabel.text = $"正在等待 {waiting?.Name ?? view.WaitingPlayerId} 接入…";
                _activityLabel.color = new Color(1f, 0.55f, 0.35f, 1f);
                _activityRoot.gameObject.SetActive(true);
                return;
            }

            _activityLabel.color = new Color(0f, 0f, 0f, 1f);

            if (string.IsNullOrEmpty(actorId) || actorId == view.LocalPlayerId)
            {
                _activityRoot.gameObject.SetActive(false);
                return;
            }

            var actor = view.FindPlayer(actorId);
            _activityLabel.text = $"{actor?.Name ?? actorId} {action}…";
            _activityRoot.gameObject.SetActive(true);
        }

        /// <summary>当前回合玩家在做什么：一回合只做一个动作，所以只有抽到一半和准备换人两种中间态。</summary>
        private static string DescribeTurnPhase(TurnActionPhase phase)
        {
            switch (phase)
            {
                case TurnActionPhase.DrawingSecondCard:
                    return "正在抽第二张车厢卡";
                case TurnActionPhase.ActionCompleted:
                    return "动作完成，即将换人";
                default:
                    return "正在行动";
            }
        }

        private void OnControllerChanged()
        {
            RefreshViews();
            UpdateDraftPanels();

            var view = _controller?.View;
            if (view == null)
            {
                return;
            }

            // 终局结算：得分明细由权威端算好（单机本端算、联机随快照下发），表现层只画表。
            if (view.IsGameOver)
            {
                if (_finalScoreShown)
                {
                    return;
                }

                _finalScoreShown = true;
                var entries = view.FinalScores;
                Debug.Log($"[GameSessionView] 游戏结束。{string.Join("；", entries.Select(entry => $"{entry.PlayerName} {entry.Total} 分"))}");
                FinalScorePanel.Open(GetComponentInParent<Canvas>(), entries, ReturnToMenu);
                return;
            }

            var current = view.CurrentPlayer;
            if (current != null && view.TurnPhase == TurnActionPhase.AwaitingAction)
            {
                Debug.Log($"[GameSessionView] 轮到 {current.Name}（{PlayerColorSelectPanel.GetColorName(current.Color)}）。" +
                          (view.IsFinalRound ? $"最后一轮，还剩 {view.FinalRoundTurnsRemaining} 回合。" : string.Empty));
            }
        }

        /// <summary>
        /// 「再来一局」：回菜单重选模式/人数再开一局。
        /// 不直接在 GameScene 里重载，是因为主机换局要先释放监听端口，
        /// 走一趟菜单能保证旧连接（_tcpTransport.Shutdown）先关干净，避免端口占用。
        /// </summary>
        private static void ReturnToMenu()
        {
            SceneManager.LoadScene("MenuScene");
        }

        /// <summary>是否联机（主机或客户端）。联机时开局由主机阶段机驱动，表现层只读快照。</summary>
        private static bool IsOnline =>
            NetworkBootstrap.Role == NetworkRole.Host || NetworkBootstrap.Role == NetworkRole.Client;

        /// <summary>
        /// 联机时的面板驱动：只根据快照判断「现在该不该我操作」。
        /// 开局按 Phase + ActingPlayerId；对局中抽任务卡则看私有段里有没有我捏着的票。
        /// 面板只提交命令，提交后不本地推进，等主机快照回流后再刷新。
        /// </summary>
        private void UpdateDraftPanels()
        {
            if (!IsOnline || _controller == null)
            {
                return;
            }

            var view = _controller.View;
            if (view == null)
            {
                return;
            }

            bool isMyTurn = !string.IsNullOrEmpty(view.LocalPlayerId)
                            && view.ActingPlayerId == view.LocalPlayerId;

            switch (view.Phase)
            {
                case MatchPhase.ColorSelect:
                    if (isMyTurn) OpenOnlineColorSelect(view);
                    else ClearDraftPanel();
                    break;

                case MatchPhase.TicketDraft:
                    if (isMyTurn) OpenOnlineTicketDraft(view);
                    else ClearDraftPanel();
                    break;

                case MatchPhase.InGame:
                    // 对局中抽任务卡：主机把我抽到的 3 张随快照私有段发回来，据此弹挑选面板。
                    if (view.MyDrawnTickets.Count > 0) OpenOnlineInGameDraft(view);
                    else ClearDraftPanel();
                    break;

                default:
                    ClearDraftPanel();
                    break;
            }
        }

        /// <summary>轮到我选色：按快照里「排在我前面、已操作过的人」的颜色置灰。</summary>
        private void OpenOnlineColorSelect(IMatchView view)
        {
            if (_draftPanel != null)
            {
                return; // 面板已开着，等玩家点（避免每次快照刷新都重开）。
            }

            var me = view.FindPlayer(view.LocalPlayerId);
            if (me == null)
            {
                return;
            }

            // 选色阶段按 Players 顺序推进，排在我前面的就是已选过的人（颜色已写回其 Player）。
            var taken = new HashSet<PlayerTrainColor>();
            foreach (var player in view.Players)
            {
                if (player.Id == me.Id)
                {
                    break;
                }

                taken.Add(player.Color);
            }

            _draftPanel = PlayerColorSelectPanel.Open(
                GetComponentInParent<Canvas>(), me.Name, me.Id, taken,
                color => _controller.Submit(new GameCommand { Kind = CommandKind.ChooseColor, Color = color }));
        }

        /// <summary>轮到我挑开局任务卡：用快照私有段里的那 3 张，选完发 PickInitialTickets。</summary>
        private void OpenOnlineTicketDraft(IMatchView view)
        {
            if (_draftPanel != null)
            {
                return;
            }

            var drawn = view.MyDrawnTickets;
            if (drawn == null || drawn.Count == 0)
            {
                return; // 主机那 3 张还没到，等下一份快照。
            }

            var me = view.FindPlayer(view.LocalPlayerId);
            string title = $"{(me != null ? me.Name : "你")} 的开局任务卡：点选要保留的，至少保留 2 张";

            _draftPanel = DestinationDraftPanel.Open(
                GetComponentInParent<Canvas>(), new List<DestinationTicket>(drawn), 2, title,
                kept => _controller.Submit(new GameCommand
                {
                    Kind = CommandKind.PickInitialTickets,
                    TicketIds = kept.Select(ticket => ticket.Id).ToArray(),
                }));
        }

        /// <summary>
        /// 对局中抽任务卡：主机已经把那 3 张随快照私有段发回来了，选完发 ChooseDestinationTickets。
        /// 与开局的区别只有「至少留 1 张」和命令类型。
        /// </summary>
        private void OpenOnlineInGameDraft(IMatchView view)
        {
            if (_draftPanel != null)
            {
                return;
            }

            var drawn = view.MyDrawnTickets;
            var me = view.FindPlayer(view.LocalPlayerId);
            string title = $"{(me != null ? me.Name : "你")} 的任务卡：点选要保留的，至少保留 1 张";

            _draftPanel = DestinationDraftPanel.Open(
                GetComponentInParent<Canvas>(), new List<DestinationTicket>(drawn), 1, title,
                kept => _controller.Submit(new GameCommand
                {
                    Kind = CommandKind.ChooseDestinationTickets,
                    TicketIds = kept.Select(ticket => ticket.Id).ToArray(),
                }));
        }

        /// <summary>清理快照驱动的面板（轮到别人 / 阶段切换 / 我已选完时）。</summary>
        private void ClearDraftPanel()
        {
            if (_draftPanel == null)
            {
                return;
            }

            Destroy(_draftPanel.gameObject);
            _draftPanel = null;
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
                PublishHostSnapshot();
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
            PublishHostSnapshot();
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
                PublishHostSnapshot();
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
            // 联机（主机本机 / 客户端）走命令通道：主机抽完会把那 3 张随快照私有段发回来，
            // 面板由 UpdateDraftPanels 弹出。客户端没有本地 GameSession，必须先分流。
            if (IsOnline)
            {
                SubmitDrawDestinationTickets();
                return;
            }

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

        /// <summary>
        /// 联机抽任务卡：先按快照粗筛「是不是我、现在能不能动」，通过则提交命令。
        /// 被主机拒绝时本机不会有提示（第一版接受静默失败）。
        /// </summary>
        private void SubmitDrawDestinationTickets()
        {
            var view = _controller?.View;
            if (view == null)
            {
                return;
            }

            if (_draftPanel != null)
            {
                return; // 已经在挑选了，别重复抽。
            }

            if (view.IsGameOver)
            {
                Debug.LogWarning("[GameSessionView] 游戏已经结束，不能再抽任务卡。");
                return;
            }

            // 一回合只能做一个动作：抽选任务卡也算一个动作。
            if (view.TurnPhase != TurnActionPhase.AwaitingAction)
            {
                Debug.LogWarning("[GameSessionView] 本回合已经执行过动作，不能再抽任务卡。");
                return;
            }

            if (view.CurrentPlayerId != view.LocalPlayerId)
            {
                Debug.LogWarning("[GameSessionView] 还没轮到你，不能抽任务卡。");
                return;
            }

            if (view.TicketDeckCount <= 0)
            {
                Debug.Log("[GameSessionView] 任务牌堆已空，无法抽选。");
                return;
            }

            _controller.Submit(new GameCommand { Kind = CommandKind.DrawDestinationTickets });
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

            if (_controller != null)
            {
                _controller.Changed -= OnControllerChanged;
            }

            _tcpTransport?.Shutdown();
            _tcpTransport = null;
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
            // 客户端没有本地 GameSession，这里只要求控制器就绪。
            if (_controller == null)
            {
                Debug.LogError("[GameSessionView] 游戏尚未初始化。");
                return;
            }

            _controller.Submit(new GameCommand { Kind = CommandKind.DrawTrainCard });
        }

        private void RefreshViews()
        {
            RefreshPlayerInfo();

            if (trainCardMarketView != null)
            {
                trainCardMarketView.Refresh(_controller?.View?.MarketCards);
            }
        }

        private void RefreshPlayerInfo()
        {
            var view = _controller?.View;
            if (view == null)
            {
                return;
            }

            if (deckCountView != null)
            {
                deckCountView.Refresh(view.DeckCount);
            }
            else
            {
                Debug.LogWarning("[GameSessionView] 没有绑定 DeckCountView。");
            }

            if (playerHandView != null)
            {
                playerHandView.Refresh(view.MyHand);
            }
            else
            {
                Debug.LogWarning("[GameSessionView] 没有绑定 PlayerHandView。");
            }

            if (destinationHandView != null)
            {
                destinationHandView.Refresh(view.MyTickets);
            }

            if (destinationDeckView != null)
            {
                destinationDeckView.Refresh(view.TicketDeckCount);
            }

            if (playerStatusBar != null)
            {
                playerStatusBar.Refresh(view.Players);
            }

            RefreshTurnHud();
            RefreshActivityTicker();
        }

        /// <summary>公开刷新接口：认领路线后手牌变化时由外部调用。</summary>
        public void RefreshHand()
        {
            RefreshPlayerInfo();
        }
    }
}

