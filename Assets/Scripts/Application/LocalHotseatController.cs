using System;
using UnityEngine;
using TicketToRide.Application.Net;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

namespace TicketToRide.Application
{
    /// <summary>
    /// 单机（同一台机器轮流操作）用的控制器：命令立即在本地领域层执行。
    /// 它同时接管「动作完成 → 停顿 1 秒 → 自动换人」的回合推进，
    /// 让表现层不再需要协程。
    /// </summary>
    public class LocalHotseatController : IGameController
    {
        /// <summary>动作完成到自动换人之间的停顿，留给玩家看清本回合结果。</summary>
        public const float TurnAdvanceDelay = 1f;

        private readonly GameSession _session;
        private readonly SessionMatchView _view;

        /// <summary>换人倒计时；小于 0 表示当前没有待推进的回合。</summary>
        private float _advanceTimer = -1f;

        public LocalHotseatController(GameSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _view = new SessionMatchView(_session);
        }

        public IMatchView View => _view;

        public event Action Changed;

        public void Submit(GameCommand command)
        {
            if (_session == null || command == null)
            {
                return;
            }

            if (_session.GameState.IsGameOver)
            {
                Debug.LogWarning($"[LocalHotseatController] 游戏已经结束，忽略命令 {command.Kind}。");
                return;
            }

            try
            {
                Apply(command);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning($"[LocalHotseatController] 命令 {command.Kind} 执行失败：{exception.Message}");
                return;
            }

            if (_session.GameState.TurnPhase == TurnActionPhase.ActionCompleted)
            {
                ArmAdvanceTimer();
            }

            Changed?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            if (_session == null)
            {
                return;
            }

            var state = _session.GameState;

            // 认领路线 / 市场取牌 / 抽任务卡目前仍由表现层直接调用领域层，
            // 这里统一兜底：只要进入「动作完成」，就排定自动换人，行为与改造前一致。
            if (!state.IsGameOver &&
                state.TurnPhase == TurnActionPhase.ActionCompleted &&
                _advanceTimer < 0f)
            {
                ArmAdvanceTimer();
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
            EndTurn();
        }

        private void Apply(GameCommand command)
        {
            switch (command.Kind)
            {
                case CommandKind.DrawTrainCard:
                    var card = _session.TurnManager.DrawTrainCard();
                    Debug.Log($"[LocalHotseatController] {_session.GameState.CurrentPlayer.Name} 抽到 {card}。");
                    break;

                case CommandKind.TakeMarketCard:
                    var visible = _session.GameState.TrainCarMarket.VisibleCards;
                    if (command.MarketSlot < 0 || command.MarketSlot >= visible.Count)
                    {
                        Debug.LogWarning($"[LocalHotseatController] 市场槽位 {command.MarketSlot} 越界，忽略。");
                        break;
                    }

                    // 只信槽位、不信颜色：这个位置上摆的是哪张牌，只有权威端说了算。
                    _session.TurnManager.TakeOneMarketCard(visible[command.MarketSlot]);
                    break;

                default:
                    // 其余动作（认领路线 / 任务卡）待后续步骤接入命令模型。
                    Debug.LogWarning($"[LocalHotseatController] 暂未接入的命令类型：{command.Kind}。");
                    break;
            }
        }

        private void ArmAdvanceTimer()
        {
            _advanceTimer = TurnAdvanceDelay;
        }

        private void EndTurn()
        {
            try
            {
                _session.TurnManager.EndTurn();
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning($"[LocalHotseatController] 无法推进回合：{exception.Message}");
                return;
            }

            Changed?.Invoke();
        }
    }
}