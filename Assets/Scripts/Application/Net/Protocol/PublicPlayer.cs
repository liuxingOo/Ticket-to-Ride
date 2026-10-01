using TicketToRide.Domain;

namespace TicketToRide.Application.Net.Protocol
{
    /// <summary>
    /// 快照里的玩家公开信息：只含所有人都能看到的数据，
    /// 手牌 / 任务卡内容用「数量」代替（内容只发给本人，见 MatchSnapshot 的 My* 段）。
    /// </summary>
    public class PublicPlayer
    {
        public string Id;
        public string Name;
        public PlayerTrainColor Color;
        public int Score;
        public int TrainCarsRemaining;
        public int HandCount;
        public int TicketCount;
    }
}