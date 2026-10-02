using System.Collections.Generic;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

namespace TicketToRide.Application
{
    /// <summary>
    /// 表现层唯一的数据读取面。
    /// 单机 / 主机由 SessionMatchView 直接读权威 GameState 实现；
    /// 客户端由 SnapshotMatchView 读主机下发的快照实现。
    /// 表现层只通过这里取数据，绝不直接碰 GameState——否则客户端会看到别人的手牌。
    /// </summary>
    public interface IMatchView
    {
        // —— 阶段与回合（公开） ——

        MatchPhase Phase { get; }
        TurnActionPhase TurnPhase { get; }
        bool IsGameOver { get; }
        bool IsFinalRound { get; }
        int FinalRoundTurnsRemaining { get; }

        /// <summary>当前回合行动的玩家 Id。</summary>
        string CurrentPlayerId { get; }

        /// <summary>开局流程中正在操作的玩家 Id（选色 / 挑任务卡），无则为空。</summary>
        string ActingPlayerId { get; }

        /// <summary>
        /// 对局中正在挑任务卡（抽 3 选 1）的玩家 Id，无则为空。
        /// 只用于界面上显示「谁在做什么」，不含牌面内容。
        /// </summary>
        string DraftingPlayerId { get; }

        /// <summary>
        /// 「该他操作、但还没有客户端接入」的玩家 Id，无则为空。
        /// 只用于界面提示：开局/回合卡住时说明到底在等谁接入。
        /// </summary>
        string WaitingPlayerId { get; }

        /// <summary>
        /// 本端玩家的 Id。联机开局阶段机用它判断「现在是不是轮到我操作」；
        /// 单机热座没有独立的开局阶段机，返回空串。
        /// </summary>
        string LocalPlayerId { get; }

        // —— 公开数据 ——

        IReadOnlyList<PublicPlayer> Players { get; }

        /// <summary>市场明牌，按槽位顺序排列（下标即提交市场取牌时用的 MarketSlot）。</summary>
        IReadOnlyList<TrainCarCard> MarketCards { get; }

        int DeckCount { get; }
        int DiscardCount { get; }
        int TicketDeckCount { get; }

        /// <summary>全部路线归属；OwnerPlayerId 为空表示未认领。</summary>
        IReadOnlyList<RouteState> Routes { get; }

        /// <summary>
        /// 终局结算表（未结束时为空）。客户端算不出最长连续路径 / 任务卡完成情况，
        /// 所以这份结果一律由权威端算好下发，表现层只展示。
        /// </summary>
        IReadOnlyList<FinalScoreEntry> FinalScores { get; }

        // —— 仅本人可见 ——

        IReadOnlyList<TrainCarCard> MyHand { get; }
        IReadOnlyList<DestinationTicket> MyTickets { get; }

        /// <summary>本人正在保留挑选中的那几张任务卡（开局 / 游戏中抽选时的中间态），无则为空。</summary>
        IReadOnlyList<DestinationTicket> MyDrawnTickets { get; }

        // —— 便捷查询 ——

        PublicPlayer FindPlayer(string playerId);
        PublicPlayer CurrentPlayer { get; }
        PublicPlayer NextPlayer { get; }
    }
}