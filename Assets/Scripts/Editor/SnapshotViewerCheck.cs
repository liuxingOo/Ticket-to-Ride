using System.Linq;
using UnityEditor;
using UnityEngine;
using TicketToRide.Application;
using TicketToRide.Application.Net;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

namespace TicketToRide.Application.EditorTools
{
    /// <summary>
    /// 快照视角自检：同一局分别按 player_1 / player_2 的视角打快照，
    /// 断言「公开段一致、私有段各按本人填」。
    /// 菜单 Tools/联机/快照视角自检 → 看 Console 输出。
    /// </summary>
    public static class SnapshotViewerCheck
    {
        [MenuItem("Tools/联机/快照视角自检")]
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
                    Debug.LogError($"[快照自检] 失败：{name}");
                }
            }

            var session = GameSession.CreateSession(2);
            var state = session.GameState;
            session.DealInitialTrainCards(4);

            var player1 = state.Players[0];
            var player2 = state.Players[1];

            // 只给 player_1 任务卡，让两个视角的私有段必定不同。
            var tickets = state.DestinationTicketDeck.DrawMany(3);
            player1.AddDestinationTickets(tickets);

            // 两条路线：一条已认领、一条未认领。
            var claimedRoute = new Route("route_Seattle_Portland", "Seattle", "Portland", 2, TrainCarColor.Green);
            var openRoute = new Route("route_Portland_San Francisco", "Portland", "San Francisco", 3, TrainCarColor.Red);
            claimedRoute.ClaimBy(player2.Id);
            session.RouteRegistry.Register(claimedRoute);
            session.RouteRegistry.Register(openRoute);

            var snapshot1 = SnapshotBuilder.Build(session, player1.Id, version: 1);
            var snapshot2 = SnapshotBuilder.Build(session, player2.Id, version: 1);

            // ---------- 公开段：两视角必须完全一致 ----------
            Check(SamePlayers(snapshot1.Players, snapshot2.Players), "两视角 Players 公开段一致");
            Check(SameColors(snapshot1.MarketCardColors, snapshot2.MarketCardColors), "两视角市场明牌一致");
            Check(SameRoutes(snapshot1.Routes, snapshot2.Routes), "两视角路线归属一致");
            Check(snapshot1.CurrentPlayerId == snapshot2.CurrentPlayerId, "两视角当前玩家一致");
            Check(SameStrings(snapshot1.TurnOrderIds, snapshot2.TurnOrderIds), "两视角行动顺序一致");
            Check(snapshot1.DeckCount == snapshot2.DeckCount
                  && snapshot1.DiscardCount == snapshot2.DiscardCount
                  && snapshot1.TicketDeckCount == snapshot2.TicketDeckCount, "两视角三种牌堆计数一致");

            // ---------- 私有段：各按本人填 ----------
            Check(SameColors(snapshot1.MyHandCardColors, player1.Hand.GetCards().Select(card => card.Color).ToArray()), "player_1 手牌只按本人填");
            Check(SameColors(snapshot2.MyHandCardColors, player2.Hand.GetCards().Select(card => card.Color).ToArray()), "player_2 手牌只按本人填");
            Check(SameStrings(snapshot1.MyTicketIds, player1.DestinationTickets.Select(ticket => ticket.Id).ToArray()), "player_1 任务卡只按本人填");
            Check(snapshot2.MyTicketIds.Length == 0, "player_2 未持有任务卡 → 私有段为空");
            Check(snapshot1.MyTicketIds.Length == 3 && snapshot2.MyTicketIds.Length == 0, "两视角私有段不相同");

            Debug.Log($"[快照自检] 完成：通过 {passed} 项，失败 {failed} 项。" +
                      $"player_1 视角：手牌 {snapshot1.MyHandCardColors.Length} 张 / 任务卡 {snapshot1.MyTicketIds.Length} 张；" +
                      $"player_2 视角：手牌 {snapshot2.MyHandCardColors.Length} 张 / 任务卡 {snapshot2.MyTicketIds.Length} 张。");
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