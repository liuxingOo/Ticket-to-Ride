using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

namespace TicketToRide.Application.Net
{
    /// <summary>
    /// 主机即服务器。它是唯一持有权威 GameState 的一端：
    /// 客户端发来的命令在这里做身份校验 + 规则裁决，通过才落到领域层，
    /// 之后按「每人一份」打快照广播出去（私有段只发本人）。
    /// 同时接管「动作完成 → 停顿 1 秒 → 自动换人」的回合推进。
    /// </summary>
    public class HostSession : IGameController
    {
        /// <summary>动作完成到自动换人之间的停顿，留给玩家看清本回合结果。</summary>
        public const float TurnAdvanceDelay = 1f;

        private readonly ITransport _transport;
        private readonly GameSession _session;

        /// <summary>主机自己的表现层读取面：读主机自己那份快照（含本人私有段）。</summary>
        private readonly SnapshotMatchView _view;

        /// <summary>连接 Id → 玩家 Id。身份只认这张表，客户端自称什么都不算。</summary>
        private readonly Dictionary<int, string> _senderToPlayer = new Dictionary<int, string>();

        /// <summary>「抽 3 选 1」的中间态：只留在主机，绝不下发他人。</summary>
        private readonly Dictionary<string, List<DestinationTicket>> _pendingTickets
            = new Dictionary<string, List<DestinationTicket>>();

        /// <summary>
        /// 连接 Id → 客户端上报的昵称。按连接 Id 存而不是按玩家 Id：
        /// 这样「报到」比「分座位」早到也不会丢，广播时再映射成玩家 Id。
        /// </summary>
        private readonly Dictionary<int, string> _nicknames = new Dictionary<int, string>();

        /// <summary>开局阶段机：ColorSelect / TicketDraft，进入对局后为 InGame（终局由领域状态覆盖为 GameOver）。</summary>
        private MatchPhase _phase = MatchPhase.InGame;

        /// <summary>开局流程中正在操作的人（选色 / 挑任务卡），对局中为空。</summary>
        private string _actingPlayerId = string.Empty;

        /// <summary>已被选走的颜色，主机据此拒绝重复选色。</summary>
        private readonly HashSet<PlayerTrainColor> _takenColors = new HashSet<PlayerTrainColor>();

        /// <summary>开局流程推进到 GameState.Players 的第几位（选色与选任务卡共用）。</summary>
        private int _preGameIndex;

        /// <summary>终局结算结果，只在游戏结束时算一次（最长连续路径是 DFS，别每份快照重算）。</summary>
        private FinalScoreResult _finalScore;

        private int _version;
        private float _advanceTimer = -1f;

        public HostSession(ITransport transport, GameSession session)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _transport.MessageReceived += OnMessageReceived;
            _view = new SnapshotMatchView(() => LocalSnapshot);
        }

        public GameSession Session => _session;

        public IMatchView View => _view;

        /// <summary>主机自己这位玩家最近一次收到的快照。</summary>
        public MatchSnapshot LocalSnapshot { get; private set; }

        public event Action Changed;

        /// <summary>对外可见的对局阶段：领域终局优先，其余交给开局阶段机。</summary>
        public MatchPhase Phase => _session.GameState.IsGameOver ? MatchPhase.GameOver : _phase;

        /// <summary>开局流程中正在操作的人（选色 / 挑任务卡）；对局中为空。</summary>
        public string ActingPlayerId => _actingPlayerId;

        /// <summary>
        /// 启动开局阶段机：第一位玩家开始选色。此后选色 / 选任务卡都走命令通道，
        /// 由主机按 Phase + ActingPlayerId 判定「现在轮到谁、能不能动手」，并广播结果。
        /// </summary>
        public void BeginPreGame()
        {
            var players = _session.GameState.Players;

            _phase = MatchPhase.ColorSelect;
            _preGameIndex = 0;
            _takenColors.Clear();
            _actingPlayerId = players.Count > 0 ? players[0].Id : string.Empty;

            Broadcast();
        }

        /// <summary>把一条连接绑定到某个玩家 Id。开局时由主机按选座结果分配。</summary>
        public void AssignClient(int clientId, string playerId)
        {
            _senderToPlayer[clientId] = playerId;
        }

        public void Submit(GameCommand command)
        {
            var playerId = _senderToPlayer.TryGetValue(_transport.LocalClientId, out var id) ? id : null;
            Apply(_transport.LocalClientId, playerId, command);
        }

        public void Tick(float deltaTime)
        {
            _transport.Tick();

            var state = _session.GameState;

            if (!state.IsGameOver &&
                state.TurnPhase == TurnActionPhase.ActionCompleted &&
                _advanceTimer < 0f)
            {
                _advanceTimer = TurnAdvanceDelay;
            }

            if (_advanceTimer < 0f)
            {
                return;
            }

            _advanceTimer -= deltaTime;
            if (_advanceTimer > 0f)
            {
                return;
            }

            _advanceTimer = -1f;

            try
            {
                _session.TurnManager.EndTurn();
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning($"[HostSession] 无法推进回合：{exception.Message}");
                return;
            }

            Broadcast();
        }

        /// <summary>主动广播一份当前快照（开局完成后、或需要主动同步时调用）。</summary>
        public void PublishSnapshot()
        {
            Broadcast();
        }

        private void OnMessageReceived(int fromClientId, byte[] payload)
        {
            MessageType type;
            try
            {
                type = MessageCodec.ReadMessageType(payload);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[HostSession] 忽略非法消息：{exception.Message}");
                return;
            }

            if (type == MessageType.Hello)
            {
                ApplyHello(fromClientId, payload);
                return;
            }

            if (type != MessageType.Command)
            {
                return;
            }

            GameCommand command;
            try
            {
                command = MessageCodec.DecodeCommand(payload);
            }
            catch (Exception exception)
            {
                Reject(fromClientId, $"命令解析失败：{exception.Message}");
                return;
            }

            var playerId = _senderToPlayer.TryGetValue(fromClientId, out var id) ? id : null;
            Apply(fromClientId, playerId, command);
        }

        /// <summary>
        /// 客户端上报昵称。只记在连接上，等到打快照时再映射成玩家 Id：
        /// 这样报到比分座位早到，也会在分座位后的那次广播里自动生效。
        /// </summary>
        private void ApplyHello(int clientId, byte[] payload)
        {
            string nickname;
            try
            {
                nickname = MessageCodec.DecodeHello(payload);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[HostSession] 昵称解析失败：{exception.Message}");
                return;
            }

            _nicknames[clientId] = nickname;
            Debug.Log($"[HostSession] 连接 {clientId} 上报昵称：{(string.IsNullOrEmpty(nickname) ? "(空，用默认名)" : nickname)}");

            // 已经分好座位的话，立刻广播让大家的界面都改名；还没分座位就等分座位后那次广播。
            if (_senderToPlayer.ContainsKey(clientId))
            {
                Broadcast();
            }
        }

        /// <summary>把「连接上的昵称」折算成「玩家 Id → 昵称」，供打快照时覆盖显示名。</summary>
        private Dictionary<string, string> BuildNameOverrides()
        {
            Dictionary<string, string> overrides = null;

            foreach (var pair in _nicknames)
            {
                if (string.IsNullOrEmpty(pair.Value)
                    || !_senderToPlayer.TryGetValue(pair.Key, out var playerId))
                {
                    continue;
                }

                overrides ??= new Dictionary<string, string>();
                overrides[playerId] = pair.Value;
            }

            return overrides;
        }

        private void Apply(int senderId, string playerId, GameCommand command)
        {
            if (playerId == null)
            {
                Reject(senderId, "该连接未绑定玩家");
                return;
            }

            if (!CommandValidator.IsWellFormed(command))
            {
                Reject(senderId, "命令字段不合法");
                return;
            }

            if (!IsAllowedNow(playerId, command.Kind))
            {
                Reject(senderId, "现在不能执行该动作");
                return;
            }

            try
            {
                if (!ApplyCommand(playerId, command))
                {
                    Reject(senderId, "动作被规则拒绝");
                    return;
                }
            }
            catch (InvalidOperationException exception)
            {
                Reject(senderId, exception.Message);
                return;
            }

            var state = _session.GameState;
            if (!state.IsGameOver &&
                state.TurnPhase == TurnActionPhase.ActionCompleted &&
                _advanceTimer < 0f)
            {
                _advanceTimer = TurnAdvanceDelay;
            }

            Broadcast();
        }

        private bool ApplyCommand(string playerId, GameCommand command)
        {
            // —— 开局阶段机：选色 / 选任务卡也走命令通道，由主机裁决 ——
            switch (_phase)
            {
                case MatchPhase.ColorSelect:
                    return ApplyChooseColor(playerId, command);

                case MatchPhase.TicketDraft:
                    if (!ApplyChooseTickets(playerId, command))
                    {
                        return false;
                    }

                    AdvanceTicketDraft();
                    return true;
            }

            // —— 正式对局 ——
            switch (command.Kind)
            {
                case CommandKind.DrawTrainCard:
                    _session.TurnManager.DrawTrainCard();
                    return true;

                case CommandKind.TakeMarketCard:
                    var visible = _session.GameState.TrainCarMarket.VisibleCards;
                    if (command.MarketSlot < 0 || command.MarketSlot >= visible.Count)
                    {
                        return false;
                    }

                    // 只信槽位、不信颜色：这个位置上摆的是哪张牌，只有主机说了算。
                    _session.TurnManager.TakeOneMarketCard(visible[command.MarketSlot]);
                    return true;

                case CommandKind.ClaimRoute:
                    if (!_session.RouteRegistry.TryGet(command.RouteId, out var route))
                    {
                        return false;
                    }

                    var cards = ResolveCardsFromHand(playerId, command.CardColors);
                    return cards != null && _session.TurnManager.ClaimRoute(route, cards);

                case CommandKind.DrawDestinationTickets:
                    var drawn = _session.GameState.DestinationTicketDeck.DrawMany(3);
                    if (drawn.Count == 0)
                    {
                        return false;
                    }

                    _pendingTickets[playerId] = drawn;
                    return true;

                case CommandKind.ChooseDestinationTickets:
                case CommandKind.PickInitialTickets:
                    return ApplyChooseTickets(playerId, command);

                default:
                    // 对局中不接受开局命令。
                    return false;
            }
        }

        /// <summary>开局选色：只认「轮到自己 + 颜色未被占用」，通过后立刻推进到下一位。</summary>
        private bool ApplyChooseColor(string playerId, GameCommand command)
        {
            if (command.Kind != CommandKind.ChooseColor)
            {
                return false;
            }

            var player = FindPlayer(playerId);
            if (player == null || _takenColors.Contains(command.Color))
            {
                return false;
            }

            player.SetTrainColor(command.Color);
            _takenColors.Add(command.Color);
            AdvanceColorSelect();
            return true;
        }

        /// <summary>选色推进：下一位，或全部选完后进入任务卡阶段。</summary>
        private void AdvanceColorSelect()
        {
            _preGameIndex++;
            var players = _session.GameState.Players;

            if (_preGameIndex >= players.Count)
            {
                StartTicketDraft();
                return;
            }

            _actingPlayerId = players[_preGameIndex].Id;
        }

        /// <summary>所有人选完色：按颜色重排行动顺序、发初始车票，进入任务卡阶段。</summary>
        private void StartTicketDraft()
        {
            var state = _session.GameState;

            state.RebuildTurnOrderByColor();
            _session.DealInitialTrainCards();

            _phase = MatchPhase.TicketDraft;
            _preGameIndex = 0;
            _actingPlayerId = state.Players[0].Id;
            DrawPendingTickets(_actingPlayerId);
        }

        /// <summary>任务卡推进：下一位，或全部选完后正式进入对局。</summary>
        private void AdvanceTicketDraft()
        {
            _preGameIndex++;
            var players = _session.GameState.Players;

            if (_preGameIndex >= players.Count)
            {
                _phase = MatchPhase.InGame;
                _actingPlayerId = string.Empty;
                return;
            }

            _actingPlayerId = players[_preGameIndex].Id;
            DrawPendingTickets(_actingPlayerId);
        }

        /// <summary>替当前该挑任务卡的玩家抽 3 张，中间态只留在主机（随快照私有段发本人）。</summary>
        private void DrawPendingTickets(string playerId)
        {
            var drawn = _session.GameState.DestinationTicketDeck.DrawMany(3);
            if (drawn.Count > 0)
            {
                _pendingTickets[playerId] = drawn;
            }
        }

        private bool ApplyChooseTickets(string playerId, GameCommand command)
        {
            if (!_pendingTickets.TryGetValue(playerId, out var drawn))
            {
                return false;
            }

            var player = FindPlayer(playerId);
            if (player == null)
            {
                return false;
            }

            // 只认「实际发出去的那 3 张」：客户端既留不了没抽到的票，也多留不了。
            var kept = drawn.Where(ticket => command.TicketIds.Contains(ticket.Id)).ToList();

            bool isInitial = command.Kind == CommandKind.PickInitialTickets;
            int minKeep = isInitial ? 2 : 1;
            if (kept.Count < minKeep)
            {
                return false;
            }

            _session.TurnManager.ChooseDestinationTickets(
                player,
                kept,
                drawn.Except(kept).ToList(),
                consumesTurnAction: !isInitial);

            _pendingTickets.Remove(playerId);
            return true;
        }

        private bool IsAllowedNow(string playerId, CommandKind kind)
        {
            // 开局阶段机：只允许「当前该动手的人」执行本阶段的动作。
            switch (Phase)
            {
                case MatchPhase.ColorSelect:
                    return kind == CommandKind.ChooseColor && playerId == _actingPlayerId;

                case MatchPhase.TicketDraft:
                    return kind == CommandKind.PickInitialTickets
                           && playerId == _actingPlayerId
                           && _pendingTickets.ContainsKey(playerId);

                case MatchPhase.GameOver:
                    return false;
            }

            // —— 正式对局 ——
            // 开局专用的 PickInitialTickets 在正式对局一律拒绝（否则可绕开「动作消耗回合」）。
            if (kind == CommandKind.PickInitialTickets)
            {
                return false;
            }

            // 任务卡结算只对「手上还捏着那 3 张票」的人开放，与回合阶段无关。
            if (kind == CommandKind.ChooseDestinationTickets)
            {
                return _pendingTickets.ContainsKey(playerId);
            }

            var state = _session.GameState;
            if (state.IsGameOver || state.TurnPhase == TurnActionPhase.ActionCompleted)
            {
                return false;
            }

            return state.CurrentPlayer.Id == playerId;
        }

        /// <summary>
        /// 把「颜色清单」还原成该玩家手牌里真实的车厢卡对象。
        /// TrainCarCard 没有 Id、也没重写 Equals，领域层全程靠引用相等，
        /// 所以绝不能 new TrainCarCard(...)，必须从 Hand 里取回原对象。
        /// </summary>
        private List<TrainCarCard> ResolveCardsFromHand(string playerId, TrainCarColor[] colors)
        {
            if (colors == null || colors.Length == 0)
            {
                return null;
            }

            var player = FindPlayer(playerId);
            if (player == null)
            {
                return null;
            }

            var hand = player.Hand.GetCards();
            var selected = new List<TrainCarCard>();

            foreach (var color in colors)
            {
                TrainCarCard match = null;
                foreach (var card in hand)
                {
                    if (card.Color == color && !selected.Contains(card))
                    {
                        match = card;
                        break;
                    }
                }

                if (match == null)
                {
                    return null;
                }

                selected.Add(match);
            }

            return selected;
        }

        private Player FindPlayer(string playerId)
        {
            return _session.GameState.Players.FirstOrDefault(player => player.Id == playerId);
        }

        private void Broadcast()
        {
            _version++;

            // 「正在挑任务卡的人」：对局中抽 3 选 1 的中间态持有者。正常情况下最多一个人。
            var draftingPlayerId = _pendingTickets.Count > 0
                ? _pendingTickets.Keys.First()
                : string.Empty;

            if (_session.GameState.IsGameOver && _finalScore == null)
            {
                _finalScore = FinalScoreCalculator.Calculate(_session.GameState);
            }

            // 公屏显示名：客户端上报的昵称在这里覆盖掉领域层的默认名。
            var nameOverrides = BuildNameOverrides();

            // 「该他操作、但还没有客户端接入」的座位：界面据此提示在等谁，避免看起来像死机。
            var waitingPlayerId = ComputeWaitingPlayerId();

            foreach (var pair in _senderToPlayer)
            {
                bool hasPending = _pendingTickets.TryGetValue(pair.Value, out var pending);

                var snapshot = SnapshotBuilder.Build(
                    _session,
                    pair.Value,
                    _version,
                    Phase,
                    _actingPlayerId,
                    hasPending ? pending : null,
                    draftingPlayerId,
                    waitingPlayerId,
                    _finalScore,
                    nameOverrides);

                if (pair.Key == _transport.LocalClientId)
                {
                    LocalSnapshot = snapshot;
                }
                else
                {
                    _transport.SendToPeer(pair.Key, MessageCodec.EncodeSnapshot(snapshot));
                }
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// 「该他操作、但还没有客户端接入」的玩家 Id；没有则空。
        /// 只用于界面提示：主机靠它在开局/回合卡住时说明到底在等谁，而不是看起来像死机。
        /// </summary>
        private string ComputeWaitingPlayerId()
        {
            var state = _session.GameState;

            string target;
            if (Phase == MatchPhase.ColorSelect || Phase == MatchPhase.TicketDraft)
            {
                target = _actingPlayerId;
            }
            else if (state.IsGameOver)
            {
                return string.Empty;
            }
            else
            {
                target = state.CurrentPlayer.Id;
            }

            if (string.IsNullOrEmpty(target))
            {
                return string.Empty;
            }

            foreach (var pair in _senderToPlayer)
            {
                if (pair.Value == target)
                {
                    return string.Empty; // 已经有人接管这个座位，不算在等。
                }
            }

            return target;
        }

        private void Reject(int senderId, string reason)
        {
            Debug.LogWarning($"[HostSession] 拒绝连接 {senderId} 的命令：{reason}");

            // 回一条一次性提示，让客户端知道刚才那下为什么没生效。
            // 主机本机、以及还没绑定玩家的连接都不发（无处可发）。
            if (senderId == _transport.LocalClientId || !_senderToPlayer.ContainsKey(senderId))
            {
                return;
            }

            _transport.SendToPeer(senderId, MessageCodec.EncodeNotice(reason));
        }
    }
}