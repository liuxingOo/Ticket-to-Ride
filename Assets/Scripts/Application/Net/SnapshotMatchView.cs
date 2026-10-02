using System;
using System.Collections.Generic;
using System.Linq;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

namespace TicketToRide.Application.Net
{
    /// <summary>
    /// 客户端（以及主机自身的表现层）读取面：只读主机下发的快照。
    /// 快照里只有公开段 + 本人的私有段，所以这里天然拿不到别人的手牌 / 任务卡。
    /// 通过 Func 延迟取「最新快照」，这样快照更新后无需重建本对象。
    /// </summary>
    public class SnapshotMatchView : IMatchView
    {
        private static readonly TrainCarCard[] NoCards = new TrainCarCard[0];
        private static readonly DestinationTicket[] NoTickets = new DestinationTicket[0];
        private static readonly PublicPlayer[] NoPlayers = new PublicPlayer[0];
        private static readonly RouteState[] NoRoutes = new RouteState[0];
        private static readonly FinalScoreEntry[] NoFinalScores = new FinalScoreEntry[0];

        private readonly Func<MatchSnapshot> _provider;

        public SnapshotMatchView(Func<MatchSnapshot> snapshotProvider)
        {
            _provider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        }

        private MatchSnapshot Snap => _provider();

        public MatchPhase Phase => Snap?.Phase ?? MatchPhase.InGame;
        public TurnActionPhase TurnPhase => Snap?.TurnPhase ?? TurnActionPhase.AwaitingAction;
        public bool IsGameOver => Snap?.Phase == MatchPhase.GameOver;
        public bool IsFinalRound => Snap?.IsFinalRound ?? false;
        public int FinalRoundTurnsRemaining => Snap?.FinalRoundTurnsRemaining ?? 0;
        public string CurrentPlayerId => Snap?.CurrentPlayerId ?? string.Empty;
        public string ActingPlayerId => Snap?.ActingPlayerId ?? string.Empty;
        public string DraftingPlayerId => Snap?.DraftingPlayerId ?? string.Empty;
        public string WaitingPlayerId => Snap?.WaitingPlayerId ?? string.Empty;
        public string LocalPlayerId => Snap?.ViewerPlayerId ?? string.Empty;

        public IReadOnlyList<PublicPlayer> Players => (IReadOnlyList<PublicPlayer>)Snap?.Players ?? NoPlayers;

        public IReadOnlyList<TrainCarCard> MarketCards
        {
            get
            {
                var colors = Snap?.MarketCardColors;
                if (colors == null || colors.Length == 0)
                {
                    return NoCards;
                }

                // TrainCarCard 只有颜色一个属性、无 Id、无隐藏状态，同色牌完全等价，
                // 因此这里按颜色还原只用于显示的卡对象是安全的（不是镜像权威可变状态）。
                var cards = new TrainCarCard[colors.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    cards[i] = new TrainCarCard(colors[i]);
                }

                return cards;
            }
        }

        public int DeckCount => Snap?.DeckCount ?? 0;
        public int DiscardCount => Snap?.DiscardCount ?? 0;
        public int TicketDeckCount => Snap?.TicketDeckCount ?? 0;

        public IReadOnlyList<RouteState> Routes => (IReadOnlyList<RouteState>)Snap?.Routes ?? NoRoutes;

        public IReadOnlyList<FinalScoreEntry> FinalScores =>
            (IReadOnlyList<FinalScoreEntry>)Snap?.FinalScores ?? NoFinalScores;

        public IReadOnlyList<TrainCarCard> MyHand
        {
            get
            {
                var colors = Snap?.MyHandCardColors;
                if (colors == null || colors.Length == 0)
                {
                    return NoCards;
                }

                var cards = new TrainCarCard[colors.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    cards[i] = new TrainCarCard(colors[i]);
                }

                return cards;
            }
        }

        public IReadOnlyList<DestinationTicket> MyTickets => ResolveTickets(Snap?.MyTicketIds);

        public IReadOnlyList<DestinationTicket> MyDrawnTickets => ResolveTickets(Snap?.MyDrawnTicketIds);

        /// <summary>按 Id 列表从静态图鉴还原任务卡（只镜像静态牌面，不是权威状态）。</summary>
        private static IReadOnlyList<DestinationTicket> ResolveTickets(string[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return NoTickets;
            }

            var tickets = new List<DestinationTicket>(ids.Length);
            foreach (var id in ids)
            {
                var ticket = DestinationTicketCatalog.Get(id);
                if (ticket != null)
                {
                    tickets.Add(ticket);
                }
            }

            return tickets;
        }

        public PublicPlayer FindPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return null;
            }

            return Players.FirstOrDefault(player => player.Id == playerId);
        }

        public PublicPlayer CurrentPlayer => FindPlayer(CurrentPlayerId);

        public PublicPlayer NextPlayer
        {
            get
            {
                var snapshot = Snap;
                var order = snapshot?.TurnOrderIds;
                if (order == null || order.Length == 0)
                {
                    return CurrentPlayer;
                }

                int index = Array.IndexOf(order, snapshot.CurrentPlayerId);
                if (index < 0)
                {
                    return CurrentPlayer;
                }

                return FindPlayer(order[(index + 1) % order.Length]);
            }
        }
    }
}