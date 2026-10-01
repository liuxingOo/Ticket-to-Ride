using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TicketToRide.Application;
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
        [SerializeField] private GameSessionView sessionView;
        [SerializeField] private Canvas canvas;

        private GameSession _session;
        private readonly Dictionary<RouteVisual, Route> _routeByVisual = new();
        private ClaimPanel _activePanel;
        private Coroutine _tipRoutine;

        private void Start()
        {
            Debug.Log("[RouteClaimController] Start 执行，开始初始化认领控制器。");

            if (sessionView == null)
            {
                sessionView = FindAnyObjectByType<GameSessionView>();
            }

            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
            }

            Debug.Log($"[RouteClaimController] sessionView={(sessionView != null ? "已找到" : "NULL")}, canvas={(canvas != null ? "已找到" : "NULL")}");

            if (sessionView == null || sessionView.Session == null)
            {
                Debug.LogError("[RouteClaimController] 找不到 GameSessionView，认领功能不可用。");
                return;
            }

            _session = sessionView.Session;
            BuildRouteMap();
        }

        private void OnDestroy()
        {
            foreach (var visual in _routeByVisual.Keys)
            {
                if (visual != null)
                {
                    visual.RouteClicked -= OnRouteClicked;
                }
            }

            _routeByVisual.Clear();
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
                visual.RouteClicked += OnRouteClicked;
            }

            // 登记进 GameState：双轨规则需要纵览全盘（同城市对的另一条轨道是谁的）。
            _session.GameState.RegisterRoutes(_routeByVisual.Values);

            Debug.Log($"[RouteClaimController] 已为 {_routeByVisual.Count} 条路线建立认领映射。");
        }

        private void OnRouteClicked(RouteVisual visual)
        {
            Debug.Log($"[RouteClaimController] 收到路线点击：{visual.StartCityId}–{visual.EndCityId}");

            if (_session == null || !_routeByVisual.TryGetValue(visual, out var route))
            {
                Debug.LogWarning("[RouteClaimController] 该路线没有对应的领域模型，忽略。");
                return;
            }

            if (route.IsClaimed || _activePanel != null)
            {
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
                visual.Claim(player.Color);
                sessionView.RefreshHand();

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

