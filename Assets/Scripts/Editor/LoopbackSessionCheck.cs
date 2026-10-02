using System.Linq;
using UnityEditor;
using UnityEngine;
using TicketToRide.Application.Net;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

namespace TicketToRide.Application.EditorTools
{
    /// <summary>
    /// Step 4 的门禁：同进程起 1 主机 + 2 客户端跑一串真实命令，
    /// 断言「权威状态正确 / 三方公开段一致 / 私有段互不可见 / 越权与作弊被拒」。
    /// 菜单 Tools/联机/回环联机自检 → 看 Console 输出。
    /// </summary>
    public static class LoopbackSessionCheck
    {
        [MenuItem("Tools/联机/回环联机自检")]
        public static void Run()
        {
            int passed = 0;
            int failed = 0;

            void Check(bool condition, string name)
            {
                if (condition)
                {
                    passed++;
                }
                else
                {
                    failed++;
                    Debug.LogError($"[回环自检] 失败：{name}");
                }
            }

            var loopback = SessionFactory.CreateLoopback(3);
            var host = loopback.Host;
            var session = host.Session;
            var players = session.GameState.Players;
            var player1 = players[0];
            var player2 = players[1];
            var player3 = players[2];

            Check(loopback.Clients.Count == 2, "1 主机 + 2 客户端已建立");

            session.DealInitialTrainCards(4);

            // 各补两张同色牌，让认领结果完全确定（不依赖随机手牌）。
            player1.Hand.AddCard(new TrainCarCard(TrainCarColor.Blue));
            player1.Hand.AddCard(new TrainCarCard(TrainCarColor.Blue));
            player2.Hand.AddCard(new TrainCarCard(TrainCarColor.Green));
            player2.Hand.AddCard(new TrainCarCard(TrainCarColor.Green));
            player3.Hand.AddCard(new TrainCarCard(TrainCarColor.Red));
            player3.Hand.AddCard(new TrainCarCard(TrainCarColor.Red));

            var routeA = new Route("route_Test_A", "Seattle", "Portland", 2, TrainCarColor.Blue);
            var routeB = new Route("route_Test_B", "Portland", "San Francisco", 2, TrainCarColor.Green);
            var routeC = new Route("route_Test_C", "San Francisco", "Los Angeles", 2, TrainCarColor.Red);
            session.RouteRegistry.Register(routeA);
            session.RouteRegistry.Register(routeB);
            session.RouteRegistry.Register(routeC);

            host.PublishSnapshot();
            loopback.TickAll(0f);
            Check(loopback.Clients.All(client => client.Latest != null), "两个客户端都收到了开局快照");
            Check(loopback.Clients.All(client => client.Latest.Version == host.LocalSnapshot.Version), "两端快照版本号一致");

            // ---------- 回合 1：主机玩家认领 routeA ----------
            host.Submit(new GameCommand
            {
                Kind = CommandKind.ClaimRoute,
                RouteId = routeA.Id,
                CardColors = new[] { TrainCarColor.Blue, TrainCarColor.Blue },
            });
            loopback.TickAll(0f);

            Check(routeA.OwnerPlayerId == player1.Id, "主机玩家的认领落到权威状态");
            Check(PublicSegmentsMatch(loopback), "回合1后三方公开段一致");
            Check(loopback.Clients.All(client => client.Latest.Routes.Any(r => r.RouteId == routeA.Id && r.OwnerPlayerId == player1.Id)),
                "客户端看得到 routeA 的归属");
            Check(player1.Hand.GetCards().Count(card => card.Color == TrainCarColor.Blue) == 0, "认领打出的牌已从手牌扣除");

            loopback.TickAll(1.1f);
            Check(session.GameState.CurrentPlayer.Id == player2.Id, "停顿 1 秒后自动轮到 player_2");

            // ---------- 回合 2：客户端(player_2) 认领 routeB ----------
            loopback.Clients[0].Submit(new GameCommand
            {
                Kind = CommandKind.ClaimRoute,
                RouteId = routeB.Id,
                CardColors = new[] { TrainCarColor.Green, TrainCarColor.Green },
            });
            loopback.TickAll(0f);

            Check(routeB.OwnerPlayerId == player2.Id, "客户端的认领落到权威状态");
            Check(PublicSegmentsMatch(loopback), "回合2后三方公开段一致");

            loopback.TickAll(1.1f);
            Check(session.GameState.CurrentPlayer.Id == player3.Id, "自动轮到 player_3");

            // ---------- 回合 3：客户端(player_3) 抽任务卡（两阶段） ----------
            var client1 = loopback.Clients[0];
            var client2 = loopback.Clients[1];

            client2.Submit(new GameCommand { Kind = CommandKind.DrawDestinationTickets });
            loopback.TickAll(0f);

            Check(client2.Latest.MyDrawnTicketIds.Length == 3, "player_3 拿到抽 3 选 1 的中间态");
            Check(client1.Latest.MyDrawnTicketIds.Length == 0, "player_2 看不到 player_3 的中间态");
            Check(client1.Latest.MyTicketIds.Length == 0, "player_2 手上没有任务卡（私有段为空）");

            var keepId = client2.Latest.MyDrawnTicketIds[0];
            client2.Submit(new GameCommand
            {
                Kind = CommandKind.ChooseDestinationTickets,
                TicketIds = new[] { keepId },
            });
            loopback.TickAll(0f);

            Check(player3.DestinationTickets.Count == 1, "player_3 保留了 1 张任务卡");
            Check(client2.Latest.MyTicketIds.Length == 1 && client2.Latest.MyTicketIds[0] == keepId, "player_3 自己的私有段正确");
            Check(client1.Latest.MyTicketIds.Length == 0, "player_2 的私有段仍为空");
            Check(PublicSegmentsMatch(loopback), "回合3后三方公开段一致");

            // ---------- 越权：不是当前回合的人发命令 ----------
            client1.Submit(new GameCommand
            {
                Kind = CommandKind.ClaimRoute,
                RouteId = routeC.Id,
                CardColors = new[] { TrainCarColor.Green, TrainCarColor.Green },
            });
            loopback.TickAll(0f);
            Check(routeC.OwnerPlayerId == null, "非当前回合玩家的命令被拒绝");

            loopback.TickAll(1.1f);
            Check(session.GameState.CurrentPlayer.Id == player1.Id, "自动轮回到 player_1");

            // ---------- 作弊：认领时谎报远超手牌数量的颜色 ----------
            var blueBefore = player1.Hand.GetCards().Count(card => card.Color == TrainCarColor.Blue);
            host.Submit(new GameCommand
            {
                Kind = CommandKind.ClaimRoute,
                RouteId = routeC.Id,
                CardColors = Enumerable.Repeat(TrainCarColor.Blue, 20).ToArray(),
            });
            loopback.TickAll(0f);

            Check(routeC.OwnerPlayerId == null, "手牌不足的认领被拒绝");
            Check(player1.Hand.GetCards().Count(card => card.Color == TrainCarColor.Blue) == blueBefore, "被拒的命令没有扣掉任何手牌");
            Check(session.GameState.CurrentPlayer.Id == player1.Id, "被拒的命令没有推进回合");

            Debug.Log($"[回环自检] 完成：通过 {passed} 项，失败 {failed} 项。" +
                      $"权威状态：routeA={routeA.OwnerPlayerId}、routeB={routeB.OwnerPlayerId}、routeC={routeC.OwnerPlayerId ?? "未认领"}；" +
                      $"当前玩家={session.GameState.CurrentPlayer.Id}；快照版本={host.LocalSnapshot?.Version}。");
        }

        /// <summary>比较主机的本地快照与各客户端快照的公开段是否完全一致。</summary>
        private static bool PublicSegmentsMatch(LoopbackSession loopback)
        {
            var reference = loopback.Host.LocalSnapshot;
            if (reference == null)
            {
                return false;
            }

            foreach (var client in loopback.Clients)
            {
                var other = client.Latest;
                if (other == null
                    || other.CurrentPlayerId != reference.CurrentPlayerId
                    || other.TurnPhase != reference.TurnPhase
                    || other.IsFinalRound != reference.IsFinalRound
                    || other.DeckCount != reference.DeckCount
                    || other.DiscardCount != reference.DiscardCount
                    || other.TicketDeckCount != reference.TicketDeckCount
                    || !SameStrings(other.TurnOrderIds, reference.TurnOrderIds)
                    || !SamePlayers(other.Players, reference.Players)
                    || !SameRoutes(other.Routes, reference.Routes)
                    || !SameColors(other.MarketCardColors, reference.MarketCardColors))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameStrings(string[] a, string[] b) => a != null && b != null && a.SequenceEqual(b);

        private static bool SameColors(TrainCarColor[] a, TrainCarColor[] b) => a != null && b != null && a.SequenceEqual(b);

        private static bool SamePlayers(PublicPlayer[] a, PublicPlayer[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].Id != b[i].Id || a[i].Name != b[i].Name || a[i].Color != b[i].Color
                    || a[i].Score != b[i].Score || a[i].TrainCarsRemaining != b[i].TrainCarsRemaining
                    || a[i].HandCount != b[i].HandCount || a[i].TicketCount != b[i].TicketCount)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameRoutes(RouteState[] a, RouteState[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].RouteId != b[i].RouteId || a[i].OwnerPlayerId != b[i].OwnerPlayerId)
                {
                    return false;
                }
            }

            return true;
        }
    }
}