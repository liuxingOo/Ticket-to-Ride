using System.Collections.Generic;
using System.Linq;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

namespace TicketToRide.Application.Net
{
    /// <summary>
    /// 把一局当前状态打成 MatchSnapshot。
    /// 公开段对所有人一致；My* 私有段只按 viewerPlayerId 填本人数据——绝不含他人手牌/任务卡。
    /// 主机对每个客户端各调一次，只有在状态变化后才广播（不每帧发）。
    /// </summary>
    public static class SnapshotBuilder
    {
        public static MatchSnapshot Build(
            GameSession session,
            string viewerPlayerId,
            int version = 0,
            MatchPhase phase = MatchPhase.InGame,
            string actingPlayerId = null,
            IReadOnlyCollection<DestinationTicket> pendingDrawnTickets = null,
            string draftingPlayerId = null,
            string waitingPlayerId = null,
            FinalScoreResult finalScore = null,
            IReadOnlyDictionary<string, string> nameOverrides = null)
        {
            var state = session.GameState;

            var snapshot = new MatchSnapshot
            {
                Version = version,
                ViewerPlayerId = viewerPlayerId ?? string.Empty,
                Phase = phase,
                ActingPlayerId = actingPlayerId ?? string.Empty,
                DraftingPlayerId = draftingPlayerId ?? string.Empty,
                WaitingPlayerId = waitingPlayerId ?? string.Empty,
                TurnPhase = state.TurnPhase,
                CurrentPlayerId = state.CurrentPlayer.Id,
                // Players 的顺序就是行动顺序（选色后 RebuildTurnOrderByColor 已按红黄蓝绿紫重排）。
                TurnOrderIds = state.Players.Select(player => player.Id).ToArray(),
                IsFinalRound = state.IsFinalRound,
                FinalRoundTurnsRemaining = state.FinalRoundTurnsRemaining,

                Players = state.Players.Select(player => new PublicPlayer
                {
                    Id = player.Id,
                    Name = ResolveName(player.Id, player.Name, nameOverrides),
                    Color = player.Color,
                    Score = player.Score,
                    TrainCarsRemaining = player.TrainCarsRemaining,
                    HandCount = player.Hand.Count,
                    TicketCount = player.DestinationTickets.Count,
                }).ToArray(),

                MarketCardColors = state.TrainCarMarket.VisibleCards.Select(card => card.Color).ToArray(),
                DeckCount = state.TrainCarDeck.Count,
                DiscardCount = state.TrainCarDeck.DiscardCount,
                TicketDeckCount = state.DestinationTicketDeck.Count,

                Routes = session.RouteRegistry.Routes
                    .Select(route => new RouteState
                    {
                        RouteId = route.Id,
                        OwnerPlayerId = route.OwnerPlayerId ?? string.Empty,
                    })
                    .ToArray(),

                // 终局结算表：只有结束时才有内容，客户端据此弹结算面板。
                FinalScores = finalScore == null
                    ? new FinalScoreEntry[0]
                    : ToFinalScoreEntries(finalScore.Rankings, nameOverrides),
            };

            // —— 私有段：只填 viewer 自己的数据 ——
            var viewer = state.Players.FirstOrDefault(player => player.Id == viewerPlayerId);

            snapshot.MyHandCardColors = viewer != null
                ? viewer.Hand.GetCards().Select(card => card.Color).ToArray()
                : new TrainCarColor[0];

            snapshot.MyTicketIds = viewer != null
                ? viewer.DestinationTickets.Select(ticket => ticket.Id).ToArray()
                : new string[0];

            snapshot.MyDrawnTicketIds = pendingDrawnTickets != null
                ? pendingDrawnTickets.Select(ticket => ticket.Id).ToArray()
                : new string[0];

            return snapshot;
        }

        /// <summary>把领域层的结算明细转成协议行：主机下发与单机本地展示共用同一套映射。</summary>
        public static FinalScoreEntry[] ToFinalScoreEntries(
            IReadOnlyList<PlayerFinalScore> rankings,
            IReadOnlyDictionary<string, string> nameOverrides = null)
        {
            if (rankings == null)
            {
                return new FinalScoreEntry[0];
            }

            return rankings.Select(entry => new FinalScoreEntry
            {
                PlayerId = entry.Player.Id,
                PlayerName = ResolveName(entry.Player.Id, entry.Player.Name, nameOverrides),
                Color = entry.Player.Color,
                Rank = entry.Rank,
                RouteScore = entry.RouteScore,
                TicketScore = entry.TicketScore,
                CompletedTickets = entry.CompletedTickets,
                TotalTickets = entry.TotalTickets,
                LongestRouteLength = entry.LongestRouteLength,
                LongestRouteBonus = entry.LongestRouteBonus,
            }).ToArray();
        }

        /// <summary>
        /// 昵称覆盖：领域层的 Player.Name 是只读的（且 Domain 层不改），
        /// 所以联机客户端上报的昵称只在打快照时按玩家 Id 换掉显示名，领域状态保持原样。
        /// </summary>
        private static string ResolveName(
            string playerId,
            string fallback,
            IReadOnlyDictionary<string, string> nameOverrides)
        {
            if (nameOverrides != null
                && !string.IsNullOrEmpty(playerId)
                && nameOverrides.TryGetValue(playerId, out var nickname)
                && !string.IsNullOrWhiteSpace(nickname))
            {
                return nickname;
            }

            return fallback;
        }
    }
}