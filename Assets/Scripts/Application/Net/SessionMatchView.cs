using System.Collections.Generic;
using System.Linq;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

namespace TicketToRide.Application.Net
{
    /// <summary>
    /// 单机 / 主机端的读取面：直接读权威 <see cref="GameSession"/>。
    /// 主机即服务器时，主机自己也是一位玩家，但它的界面同样只应看到自己的手牌——
    /// 所以主机表现层应改用 SnapshotMatchView(LocalSnapshot)，本类主要服务单机热座。
    /// </summary>
    public class SessionMatchView : IMatchView
    {
        private readonly GameSession _session;

        public SessionMatchView(GameSession session)
        {
            _session = session;
        }

        private GameState State => _session.GameState;

        public MatchPhase Phase => State.IsGameOver ? MatchPhase.GameOver : MatchPhase.InGame;
        public TurnActionPhase TurnPhase => State.TurnPhase;
        public bool IsGameOver => State.IsGameOver;
        public bool IsFinalRound => State.IsFinalRound;
        public int FinalRoundTurnsRemaining => State.FinalRoundTurnsRemaining;
        public string CurrentPlayerId => State.CurrentPlayer.Id;

        /// <summary>单机没有独立的开局阶段机，开局流程仍由表现层依次驱动。</summary>
        public string ActingPlayerId => string.Empty;

        /// <summary>单机的抽选中间态由表现层自己持有，这里无数据（「谁在做什么」只用于联机）。</summary>
        public string DraftingPlayerId => string.Empty;

        /// <summary>单机每个座位都是本机的，不存在「等人接入」。</summary>
        public string WaitingPlayerId => string.Empty;

        /// <summary>单机热座由表现层驱动开局，不需要「我是谁」来判轮次。</summary>
        public string LocalPlayerId => string.Empty;

        public IReadOnlyList<PublicPlayer> Players => State.Players.ConvertAll(ToPublic);

        public IReadOnlyList<TrainCarCard> MarketCards => State.TrainCarMarket.VisibleCards;

        public int DeckCount => State.TrainCarDeck.Count;
        public int DiscardCount => State.TrainCarDeck.DiscardCount;
        public int TicketDeckCount => State.DestinationTicketDeck.Count;

        public IReadOnlyList<RouteState> Routes => _session.RouteRegistry.Routes
            .Select(route => new RouteState
            {
                RouteId = route.Id,
                OwnerPlayerId = route.OwnerPlayerId ?? string.Empty,
            })
            .ToArray();

        /// <summary>单机在本端直接算（联机时这份结果由主机算好下发）。</summary>
        public IReadOnlyList<FinalScoreEntry> FinalScores =>
            SnapshotBuilder.ToFinalScoreEntries(FinalScoreCalculator.Calculate(State).Rankings);

        public IReadOnlyList<TrainCarCard> MyHand => State.CurrentPlayer.Hand.GetCards();

        public IReadOnlyList<DestinationTicket> MyTickets => State.CurrentPlayer.DestinationTickets;

        /// <summary>单机开局的抽选中间态由表现层自己持有（DestinationDraftPanel 直接拿引用），这里无数据。</summary>
        public IReadOnlyList<DestinationTicket> MyDrawnTickets => System.Array.Empty<DestinationTicket>();

        public PublicPlayer FindPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return null;
            }

            foreach (var player in State.Players)
            {
                if (player.Id == playerId)
                {
                    return ToPublic(player);
                }
            }

            return null;
        }

        public PublicPlayer CurrentPlayer => ToPublic(State.CurrentPlayer);
        public PublicPlayer NextPlayer => ToPublic(State.NextPlayer);

        private static PublicPlayer ToPublic(Player player)
        {
            return new PublicPlayer
            {
                Id = player.Id,
                Name = player.Name,
                Color = player.Color,
                Score = player.Score,
                TrainCarsRemaining = player.TrainCarsRemaining,
                HandCount = player.Hand.Count,
                TicketCount = player.DestinationTickets.Count,
            };
        }
    }
}