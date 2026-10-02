using TicketToRide.Domain;

namespace TicketToRide.Application.Net.Protocol
{
    /// <summary>对局阶段。联机时开局流程也由权威端按阶段 + ActingPlayerId 驱动。</summary>
    public enum MatchPhase : byte
    {
        /// <summary>开局选列车颜色。</summary>
        ColorSelect = 0,

        /// <summary>开局逐个挑任务卡。</summary>
        TicketDraft = 1,

        /// <summary>正式对局中。</summary>
        InGame = 2,

        /// <summary>已结束（含终局结算）。</summary>
        GameOver = 3,
    }

    /// <summary>
    /// 一次全量状态快照（不做增量）。
    /// 公开段所有人一致；My* 段只发给对应玩家，绝不广播他人私有信息。
    /// 客户端用 Version 单调递增来丢弃过期的旧包。
    /// </summary>
    public class MatchSnapshot
    {
        /// <summary>单调递增的版本号，客户端丢弃比已应用版本更小的包。</summary>
        public int Version;

        /// <summary>
        /// 这份快照是发给谁的（本人玩家 Id）。快照本就按人分包，所以不算泄露——
        /// 客户端靠它知道「我是谁」，从而判断开局阶段机现在是不是轮到自己操作。
        /// </summary>
        public string ViewerPlayerId;

        public MatchPhase Phase;

        /// <summary>开局流程中正在操作的人（选色 / 挑任务卡）。</summary>
        public string ActingPlayerId;

        /// <summary>
        /// 正在挑选任务卡的人（对局中抽 3 选 1 的中间态持有者）。
        /// 开局阶段的挑任务卡由 Phase + ActingPlayerId 表达，这里通常为空。
        /// 客户端靠它显示「谁在做什么」，不涉及牌面内容。
        /// </summary>
        public string DraftingPlayerId;

        /// <summary>
        /// 「该他操作、但还没有客户端接入」的玩家 Id，没有则为空。
        /// 只用于界面提示：让主机和已连上的客户端都知道到底卡在等谁，而不是看起来像死机。
        /// </summary>
        public string WaitingPlayerId;

        public TurnActionPhase TurnPhase;

        public string CurrentPlayerId;

        public string[] TurnOrderIds;

        public bool IsFinalRound;
        public int FinalRoundTurnsRemaining;

        public PublicPlayer[] Players;

        /// <summary>市场明牌颜色，按槽位顺序排列（市场内部无空洞）。</summary>
        public TrainCarColor[] MarketCardColors;

        public int DeckCount;
        public int DiscardCount;
        public int TicketDeckCount;

        public RouteState[] Routes;

        /// <summary>终局结算表；未结束时为空数组。</summary>
        public FinalScoreEntry[] FinalScores;

        // —— 仅发给本人的段 ——

        /// <summary>自己的手牌颜色（他人不可见）。</summary>
        public TrainCarColor[] MyHandCardColors;

        /// <summary>自己已持有的任务卡 Id（他人不可见）。</summary>
        public string[] MyTicketIds;

        /// <summary>自己正在挑选的 3 张任务卡 Id（中间态，他人不可见）。</summary>
        public string[] MyDrawnTicketIds;
    }
}