using TicketToRide.Domain;

namespace TicketToRide.Application.Net.Protocol
{
    /// <summary>
    /// 终局结算表里的一行（公开段）。
    /// 客户端没有权威 GameState，算不出最长连续路径和任务卡完成情况，
    /// 所以这些分数一律由权威端算好下发，客户端只负责展示。
    /// 名次为 1 即获胜者（并列获胜时会有多行名次为 1）。
    /// </summary>
    public class FinalScoreEntry
    {
        public string PlayerId;
        public string PlayerName;
        public PlayerTrainColor Color;

        public int Rank;

        /// <summary>路线分：游戏中认领路线时已累计。</summary>
        public int RouteScore;

        /// <summary>任务卡得分：已完成 +分，未完成 −分。</summary>
        public int TicketScore;

        public int CompletedTickets;
        public int TotalTickets;

        public int LongestRouteLength;

        /// <summary>最长连续路径加分（全场最长 +10，否则 0）。</summary>
        public int LongestRouteBonus;

        public int Total => RouteScore + TicketScore + LongestRouteBonus;

        public bool HasLongestRoute => LongestRouteBonus > 0;
    }
}