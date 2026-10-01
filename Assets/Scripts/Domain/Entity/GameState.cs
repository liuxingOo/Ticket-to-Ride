using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    public class GameState
    {
        public List<Player> Players { get; }
        public TrainCarDeck TrainCarDeck { get; }
        public TrainCarMarket TrainCarMarket { get; }
        public DestinationTicketDeck DestinationTicketDeck { get; }
        public Queue<Player> TurnOrder { get; }
        public Player CurrentPlayer => TurnOrder.Peek();
        public bool IsGameOver { get; private set; }

        /// <summary>终局阈值：任一玩家剩余车厢 ≤ 该值时进入「最后一轮」。</summary>
        public const int EndGameTrainThreshold = 2;

        /// <summary>是否已进入最后一轮（触发后所有玩家各再行动一回合）。</summary>
        public bool IsFinalRound { get; private set; }

        /// <summary>最后一轮还剩几个回合（减到 0 即游戏结束）。</summary>
        public int FinalRoundTurnsRemaining { get; private set; }

        private readonly List<Route> _routes = new List<Route>();

        /// <summary>棋盘上所有可认领路线（由表现层构建后注册，供双轨等全盘规则判断）。</summary>
        public IReadOnlyList<Route> Routes => _routes;

        public GameState(IEnumerable<Player> players, TrainCarDeck trainCarDeck, TrainCarMarket trainCarMarket = null, DestinationTicketDeck destinationTicketDeck = null)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players), "Players collection cannot be null.");
            }

            var playerList = players.ToList();
            if (playerList.Count == 0)
            {
                throw new ArgumentException("At least one player is required.", nameof(players));
            }

            if (trainCarDeck == null)
            {
                throw new ArgumentNullException(nameof(trainCarDeck), "Train car deck cannot be null.");
            }

            Players = playerList;
            TrainCarDeck = trainCarDeck;
            TrainCarMarket = trainCarMarket ?? new TrainCarMarket(trainCarDeck);
            DestinationTicketDeck = destinationTicketDeck ?? global::TicketToRide.Domain.DestinationTicketDeck.CreateStandardDeck();
            TurnOrder = new Queue<Player>(playerList);
        }

        public void NextTurn()
        {
            // 终局判定（在当前玩家回合结束时进行）：
            // 任一玩家剩余车厢 ≤ 2 → 进入最后一轮，所有玩家（含触发者）各再行动一回合。
            // 触发者刚结束的这个回合不计入最后一轮，所以从下一回合开始倒数 Players.Count 次。
            if (!IsGameOver && !IsFinalRound &&
                Players.Any(player => player.TrainCarsRemaining <= EndGameTrainThreshold))
            {
                IsFinalRound = true;
                FinalRoundTurnsRemaining = Players.Count;
            }
            else if (!IsGameOver && IsFinalRound)
            {
                FinalRoundTurnsRemaining--;
                if (FinalRoundTurnsRemaining <= 0)
                {
                    EndGame();
                }
            }

            var currentPlayer = TurnOrder.Dequeue();
            TurnOrder.Enqueue(currentPlayer);
        }

        public void EndGame()
        {
            IsGameOver = true;
        }

        /// <summary>
        /// 登记棋盘上的全部路线，供双轨规则等需要纵览全盘的判断使用。
        /// 重复登记同一条目会被忽略。
        /// </summary>
        public void RegisterRoutes(IEnumerable<Route> routes)
        {
            if (routes == null)
            {
                throw new ArgumentNullException(nameof(routes));
            }

            foreach (var route in routes)
            {
                if (route != null && !_routes.Contains(route))
                {
                    _routes.Add(route);
                }
            }
        }

        /// <summary>
        /// 双轨规则（美版官方）：
        /// 2–3 人局：同一城市对的两条平行轨道只能使用一条，任一条被认领后另一条对所有人封锁；
        /// 4–5 人局：两条都可认领，但同一位玩家不能同时占据两条。
        /// 返回 true 表示该路线因双轨规则不可被 player 认领（与车厢卡、剩余车厢等无关）。
        /// </summary>
        public bool IsBlockedByParallelRule(Route route, Player player)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            // 尚未登记路线时不做拦截（容错，避免影响旧调用方）。
            if (_routes.Count == 0)
            {
                return false;
            }

            var parallelTracks = new List<Route>();
            foreach (var candidate in _routes)
            {
                if (!ReferenceEquals(candidate, route) && SameCityPair(candidate, route))
                {
                    parallelTracks.Add(candidate);
                }
            }

            if (parallelTracks.Count == 0)
            {
                return false;
            }

            if (Players.Count <= 3)
            {
                // 2–3 人局：只要同城市对的另一条已被任何人占据，本城市对即封锁。
                return parallelTracks.Any(track => track.IsClaimed);
            }

            // 4–5 人局：只有该玩家自己已占据同城市对的另一条时才禁止。
            return parallelTracks.Any(track => track.IsClaimed && track.OwnerPlayerId == player.Id);
        }

        /// <summary>两条路线的城市对是否相同（城市顺序无关）。</summary>
        private static bool SameCityPair(Route a, Route b)
        {
            return (a.StartCity == b.StartCity && a.EndCity == b.EndCity)
                   || (a.StartCity == b.EndCity && a.EndCity == b.StartCity);
        }
    }
}
