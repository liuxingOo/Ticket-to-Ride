using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    /// <summary>单个玩家的终局得分明细。</summary>
    public class PlayerFinalScore
    {
        public Player Player { get; }

        /// <summary>路线分：游戏中认领路线时已累计，这里只是取出来展示。</summary>
        public int RouteScore { get; }

        /// <summary>任务卡得分：已完成的任务卡 +分，未完成的 −分。</summary>
        public int TicketScore { get; }

        /// <summary>已完成的任务卡数量（也是官方平局规则的第二判据）。</summary>
        public int CompletedTickets { get; }

        public int TotalTickets { get; }

        /// <summary>最长连续路径长度，按车厢数计。</summary>
        public int LongestRouteLength { get; }

        /// <summary>最长连续路径加分：全场最长 +10，其余 0；并列最长时并列者都 +10。</summary>
        public int LongestRouteBonus { get; }

        public int Total => RouteScore + TicketScore + LongestRouteBonus;

        public bool HasLongestRoute => LongestRouteBonus > 0;

        public int Rank { get; internal set; }

        internal PlayerFinalScore(
            Player player,
            int ticketScore,
            int completedTickets,
            int totalTickets,
            int longestRouteLength,
            int longestRouteBonus)
        {
            Player = player;
            RouteScore = player.Score;
            TicketScore = ticketScore;
            CompletedTickets = completedTickets;
            TotalTickets = totalTickets;
            LongestRouteLength = longestRouteLength;
            LongestRouteBonus = longestRouteBonus;
        }
    }

    /// <summary>一局游戏的终局结算结果。</summary>
    public class FinalScoreResult
    {
        /// <summary>按名次排好序的得分明细（总分降序，同分则完成任务卡多者在先）。</summary>
        public IReadOnlyList<PlayerFinalScore> Rankings { get; }

        /// <summary>获胜者。总分与完成任务卡数都相同时并列获胜，所以可能有多个。</summary>
        public IReadOnlyList<PlayerFinalScore> Winners { get; }

        public bool IsSharedVictory => Winners.Count > 1;

        internal FinalScoreResult(IReadOnlyList<PlayerFinalScore> rankings, IReadOnlyList<PlayerFinalScore> winners)
        {
            Rankings = rankings;
            Winners = winners;
        }
    }

    /// <summary>
    /// 终局结算（官方美版规则）：
    /// 总分 = 路线分 + 任务卡分（完成 +、未完成 −）+ 最长连续路径加分（+10）。
    /// 平局时完成任务卡多者获胜，仍然相同则并列获胜。
    /// </summary>
    public static class FinalScoreCalculator
    {
        /// <summary>最长连续路径加分。</summary>
        public const int LongestRouteBonusPoints = 10;

        public static FinalScoreResult Calculate(GameState gameState)
        {
            if (gameState == null)
            {
                throw new ArgumentNullException(nameof(gameState));
            }

            var players = gameState.Players;

            var longestLengths = new Dictionary<Player, int>();
            foreach (var player in players)
            {
                longestLengths[player] = GetLongestRouteLength(player);
            }

            int longestOverall = longestLengths.Count > 0 ? longestLengths.Values.Max() : 0;

            var entries = new List<PlayerFinalScore>(players.Count);
            foreach (var player in players)
            {
                int ticketScore = player.GetDestinationTicketScore();
                int completed = player.DestinationTickets.Count(ticket => ticket.IsCompleted(player.ClaimedRoutes));
                int longest = longestLengths[player];

                // 无人认领任何路线时 longestOverall 为 0，此时不发加分。
                int bonus = longestOverall > 0 && longest == longestOverall ? LongestRouteBonusPoints : 0;

                entries.Add(new PlayerFinalScore(
                    player,
                    ticketScore,
                    completed,
                    player.DestinationTickets.Count,
                    longest,
                    bonus));
            }

            // 名次：总分降序 → 完成任务卡多者优先（官方平局规则）→ 原行动顺序兜底。
            var rankings = entries
                .Select((entry, index) => new { Entry = entry, Index = index })
                .OrderByDescending(item => item.Entry.Total)
                .ThenByDescending(item => item.Entry.CompletedTickets)
                .ThenBy(item => item.Index)
                .Select(item => item.Entry)
                .ToList();

            // 真正并列（总分与完成任务卡数都相同）的两个玩家名次数字相同。
            int rank = 0;
            int previousTotal = int.MinValue;
            int previousCompleted = int.MinValue;

            for (int i = 0; i < rankings.Count; i++)
            {
                var entry = rankings[i];
                if (i == 0 || entry.Total != previousTotal || entry.CompletedTickets != previousCompleted)
                {
                    rank = i + 1;
                }

                entry.Rank = rank;
                previousTotal = entry.Total;
                previousCompleted = entry.CompletedTickets;
            }

            var top = rankings[0];
            var winners = rankings
                .Where(entry => entry.Total == top.Total && entry.CompletedTickets == top.CompletedTickets)
                .ToList();

            return new FinalScoreResult(rankings, winners);
        }

        /// <summary>
        /// 最长连续路径（按车厢数计）：在玩家已认领路线构成的图上找最长简单路径。
        /// 规则要点：同一条路线不能重复使用，同一条路径也不能两次经过同一个城市。
        /// 规模很小（玩家最多 45 节车厢），用带剪枝的 DFS 足够。
        /// </summary>
        public static int GetLongestRouteLength(Player player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            var adjacency = new Dictionary<string, List<(string City, int Length)>>();
            int totalCars = 0;

            foreach (var route in player.ClaimedRoutes)
            {
                if (route == null)
                {
                    continue;
                }

                totalCars += route.Length;
                AddEdge(adjacency, route.StartCity, route.EndCity, route.Length);
                AddEdge(adjacency, route.EndCity, route.StartCity, route.Length);
            }

            if (adjacency.Count == 0)
            {
                return 0;
            }

            int best = 0;
            var visited = new HashSet<string>();

            // 从每个城市都起一次：最长路径不一定从端点城市开始计数更优，全试一遍最稳妥。
            foreach (var city in adjacency.Keys)
            {
                visited.Add(city);
                Search(city, 0, totalCars, adjacency, visited, ref best);
                visited.Remove(city);
            }

            return best;
        }

        private static void AddEdge(
            Dictionary<string, List<(string City, int Length)>> adjacency,
            string from,
            string to,
            int length)
        {
            if (!adjacency.TryGetValue(from, out var neighbors))
            {
                neighbors = new List<(string City, int Length)>();
                adjacency[from] = neighbors;
            }

            neighbors.Add((to, length));
        }

        private static void Search(
            string city,
            int currentLength,
            int remainingCars,
            Dictionary<string, List<(string City, int Length)>> adjacency,
            HashSet<string> visited,
            ref int best)
        {
            if (currentLength > best)
            {
                best = currentLength;
            }

            // 剪枝：把还没用过的车厢全部接上也不可能超过当前最优。
            if (currentLength + remainingCars <= best)
            {
                return;
            }

            foreach (var edge in adjacency[city])
            {
                if (!visited.Add(edge.City))
                {
                    continue;
                }

                Search(edge.City, currentLength + edge.Length, remainingCars - edge.Length, adjacency, visited, ref best);
                visited.Remove(edge.City);
            }
        }
    }
}