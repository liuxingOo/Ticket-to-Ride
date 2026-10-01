namespace TicketToRide.Domain
{
    /// <summary>
    /// 单个回合内的动作状态机。
    /// 官方规则：一回合只能做「抽 2 张车厢卡 / 认领 1 条路线 / 抽选任务卡」其中之一，
    /// 而抽车厢卡又必须抽满 2 张（明牌车头除外，它一次吃掉两次机会）。
    /// </summary>
    public enum TurnActionPhase
    {
        /// <summary>尚未行动：可以抽牌、认领路线或抽任务卡。</summary>
        AwaitingAction,

        /// <summary>已抽 1 张车厢卡，本回合必须再抽 1 张才能结束回合。</summary>
        DrawingSecondCard,

        /// <summary>本回合的动作已完成，只能结束回合。</summary>
        ActionCompleted,
    }
}