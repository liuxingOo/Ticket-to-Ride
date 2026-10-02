using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TicketToRide.Application;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 把场景里的 RouteVisual 和领域层的认领逻辑接起来：
    /// 启动时为每条视觉路线创建对应的领域 Route，订阅点击事件。
    /// 点击路线时弹出 ClaimPanel 让玩家手动选卡，确认后调用 TurnManager.ClaimRoute。
    /// </summary>
    public class RouteClaimController : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;

        private GameSession _session;
        private GameSessionView _sessionView;
        private IGameController _controller;
        private readonly Dictionary<RouteVisual, Route> _routeByVisual = new();
        private readonly Dictionary<string, RouteVisual> _visualByRouteId = new();
        private ClaimPanel _activePanel;
        private Coroutine _tipRoutine;

        /// <summary>
        /// 由 GameSessionView 在会话创建好（选完人数）之后调用。
        /// 开局前还没有会话，所以映射必须等到这一步才建立。
        /// 客户端没有权威 GameState，session 传 null：本地这份 Route 只用于展示。
        /// </summary>
        public void Initialize(GameSession session, GameSessionView sessionView, IGameController controller)
        {
            _session = session;
            _sessionView = sessionView;
            _controller = controller;

            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
            }

            BuildRouteMap();

            // 路线归属改为「快照驱动」：任何来源（本人认领、别人认领）都通过数据面刷新重绘，
            // 客户端无需自己推断谁占了哪条线。
            if (_controller != null)
            {
                _controller.Changed += RefreshRouteClaims;
            }

            RefreshRouteClaims();
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.Changed -= RefreshRouteClaims;
            }

            foreach (var visual in _routeByVisual.Keys)
            {
                if (visual != null)
                {
                    visual.RouteClicked -= OnRouteClicked;
                }
            }

            _routeByVisual.Clear();
        }

        /// <summary>按数据面里的路线归属，把每条已认领路线改成对应玩家颜色。</summary>
        private void RefreshRouteClaims()
        {
            var view = _controller?.View;
            if (view == null)
            {
                return;
            }

            var owners = new Dictionary<string, PlayerTrainColor>();
            foreach (var state in view.Routes)
            {
                if (string.IsNullOrEmpty(state.OwnerPlayerId))
                {
                    continue;
                }

                if (!owners.TryGetValue(state.RouteId, out var color))
                {
                    var owner = view.FindPlayer(state.OwnerPlayerId);
                    if (owner == null)
                    {
                        continue;
                    }

                    color = owner.Color;
                    owners[state.RouteId] = color;
                }

                if (_visualByRouteId.TryGetValue(state.RouteId, out var visual) && visual != null && !visual.IsClaimed)
                {
                    visual.Claim(color);
                }
            }
        }

        private void BuildRouteMap()
        {
            var visuals = FindObjectsByType<RouteVisual>(FindObjectsInactive.Exclude);

            foreach (var visual in visuals)
            {
                if (string.IsNullOrWhiteSpace(visual.StartCityId) || string.IsNullOrWhiteSpace(visual.EndCityId))
                {
                    continue;
                }

                TrainCarColor? color = visual.IsGreyRoute ? (TrainCarColor?)null : visual.RouteColor;
                var route = new Route(visual.RouteId, visual.StartCityId, visual.EndCityId, visual.Length, color);

                _routeByVisual[visual] = route;
                _visualByRouteId[visual.RouteId] = visual;
                visual.RouteClicked += OnRouteClicked;
            }

            if (_session == null)
            {
                // 客户端：城市 / 节数 / 颜色都是静态地图数据，本地这份 Route 只用于弹认领面板的展示，认领由主机裁决。
                Debug.Log($"[RouteClaimController] 客户端已为 {_routeByVisual.Count} 条路线建立展示映射。");
                return;
            }

            // 登记进 GameState：双轨规则需要纵览全盘（同城市对的另一条轨道是谁的）。
            _session.GameState.RegisterRoutes(_routeByVisual.Values);

            // 登记进 RouteRegistry：联机时权威端用 RouteId 还原真实 Route 对象，
            // 同时在这里检测 Id 重复（RouteVisual.RouteId 的 Inspector 默认值是 "route"）。
            var registry = _session.RouteRegistry;
            foreach (var route in _routeByVisual.Values)
            {
                registry.Register(route);
            }

            Debug.Log($"[RouteClaimController] 已为 {_routeByVisual.Count} 条路线建立认领映射，RouteRegistry 收录 {registry.Count} 条。");

            if (registry.DuplicateIds.Count > 0)
            {
                Debug.LogError($"[RouteClaimController] 发现重复的路线 Id：{string.Join("、", registry.DuplicateIds)}。" +
                               "联机按 Id 认领会出错，请在场景里把这些 RouteVisual 改成唯一 Id。");
            }

            Debug.Assert(registry.DuplicateIds.Count == 0, "[RouteClaimController] RouteVisual.RouteId 存在重复，联机认领会失败。");
        }

        private void OnRouteClicked(RouteVisual visual)
        {
            Debug.Log($"[RouteClaimController] 收到路线点击：{visual.StartCityId}–{visual.EndCityId}");

            if (!_routeByVisual.TryGetValue(visual, out var route))
            {
                Debug.LogWarning("[RouteClaimController] 该路线没有对应的领域模型，忽略。");
                return;
            }

            if (_activePanel != null)
            {
                return;
            }

            if (IsOnline)
            {
                OnRouteClickedOnline(visual, route);
                return;
            }

            if (_session == null)
            {
                return;
            }

            if (route.IsClaimed)
            {
                return;
            }

            if (_session.GameState.IsGameOver)
            {
                ShowTip("游戏已经结束");
                return;
            }

            // 一回合只能做一个动作：动作已经开始，本回合不能再认领路线。
            if (_session.GameState.TurnPhase != TurnActionPhase.AwaitingAction)
            {
                ShowTip("本回合已执行过动作");
                return;
            }

            var player = _session.GameState.CurrentPlayer;

            // 双轨规则：被封锁的平行轨道不弹认领面板，只给一句简短提示。
            if (_session.GameState.IsBlockedByParallelRule(route, player))
            {
                Debug.Log($"[RouteClaimController] {route.StartCity}–{route.EndCity} 因双轨规则不可认领（当前 {_session.GameState.Players.Count} 人局）。");
                ShowTip("此双轨已封锁，只能使用一条");
                return;
            }

            Debug.Log($"[RouteClaimController] 打开认领面板，canvas={(canvas != null ? "OK" : "NULL")}");

            _activePanel = ClaimPanel.Open(
                canvas,
                route,
                player,
                cards => OnClaimConfirmed(visual, route, player, cards),
                () => _activePanel = null);

            if (_activePanel == null)
            {
                Debug.LogError("[RouteClaimController] 认领面板创建失败！");
            }
        }

        private static bool IsOnline =>
            NetworkBootstrap.Role == NetworkRole.Host || NetworkBootstrap.Role == NetworkRole.Client;

        /// <summary>
        /// 联机（主机本机 / 客户端）的路线点击：只做「是不是我、现在能不能动」的粗筛，
        /// 真正的规则裁决（双轨封锁、手牌、路线归属）全部在主机。被主机拒绝时本机不会有提示。
        /// </summary>
        private void OnRouteClickedOnline(RouteVisual visual, Route route)
        {
            var view = _controller?.View;
            if (view == null)
            {
                return;
            }

            // 归属看快照驱动的视觉状态：本地那份展示 Route 永远不知道谁占了它。
            if (visual.IsClaimed)
            {
                return;
            }

            if (view.IsGameOver)
            {
                ShowTip("游戏已经结束");
                return;
            }

            if (view.TurnPhase != TurnActionPhase.AwaitingAction)
            {
                ShowTip("本回合已执行过动作");
                return;
            }

            if (view.CurrentPlayerId != view.LocalPlayerId)
            {
                ShowTip("还没轮到你");
                return;
            }

            _activePanel = ClaimPanel.Open(
                canvas,
                route,
                view.MyHand,
                cards => OnClaimSubmitted(route, cards),
                () => _activePanel = null);

            if (_activePanel == null)
            {
                Debug.LogError("[RouteClaimController] 认领面板创建失败！");
            }
        }

        /// <summary>联机确认认领：只提交「路线 Id + 用掉的颜色」，真实卡引用由主机从手牌取回。</summary>
        private void OnClaimSubmitted(Route route, List<TrainCarCard> cards)
        {
            _activePanel = null;

            _controller.Submit(new GameCommand
            {
                Kind = CommandKind.ClaimRoute,
                RouteId = route.Id,
                CardColors = cards.Select(card => card.Color).ToArray(),
            });
        }

        private void OnClaimConfirmed(RouteVisual visual, Route route, Player player, List<TrainCarCard> cards)
        {
            _activePanel = null;

            if (!player.CanClaimRoute(route, cards))
            {
                Debug.LogWarning($"[RouteClaim] 规则校验未通过：{route.StartCity}–{route.EndCity}。");
                return;
            }

            if (_session.TurnManager.ClaimRoute(route, cards))
            {
                // 认领结果统一由数据面驱动重绘（本机立即生效；联机由主机快照回流）。
                RefreshRouteClaims();
                _sessionView?.RefreshHand();

                Debug.Log(
                    $"[RouteClaim] {player.Name} 认领了 {route.StartCity}–{route.EndCity}" +
                    $"（{route.Length} 节，{player.Color}），剩余车厢 {player.TrainCarsRemaining}，得分 {player.Score}。");
            }
            else if (_session.GameState.IsBlockedByParallelRule(route, player))
            {
                ShowTip("此双轨已封锁，只能使用一条");
            }
            else
            {
                Debug.LogWarning($"[RouteClaim] 认领失败：{route.StartCity}–{route.EndCity}。");
            }
        }

        /// <summary>
        /// 简短提示：复用城市悬停提示的样式（无底板、跟随鼠标），1.8 秒后自动消失。
        /// </summary>
        private void ShowTip(string text)
        {
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                return;
            }

            var tooltip = CityTooltip.Get(canvas);
            if (tooltip == null)
            {
                return;
            }

            tooltip.Show(text);

            if (_tipRoutine != null)
            {
                StopCoroutine(_tipRoutine);
            }

            _tipRoutine = StartCoroutine(HideTipAfter(1.8f));
        }

        private IEnumerator HideTipAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);

            var tooltip = CityTooltip.Instance;
            if (tooltip != null)
            {
                tooltip.Hide();
            }

            _tipRoutine = null;
        }
    }
}

