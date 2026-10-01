using TicketToRide.Domain;

namespace TicketToRide.Application.Net.Protocol
{
    /// <summary>
    /// 玩家动作类型。联机时客户端只发命令，由权威端（主机）执行。
    /// </summary>
    public enum CommandKind
    {
        SetPlayerCount,
        ChooseColor,
        DrawTrainCard,
        TakeMarketCard,
        ClaimRoute,
        DrawDestinationTickets,
        ChooseDestinationTickets,
        PickInitialTickets,
    }

    /// <summary>
    /// 扁平结构的动作命令（刻意不用多态，方便后续用 BinaryWriter 手写序列化）。
    /// 关键约束：不携带任何领域对象实例，只带颜色 / 槽位下标 / Id，
    /// 由权威端从该玩家的手牌里取回真实对象引用（TrainCarCard 靠引用相等）。
    /// </summary>
    public class GameCommand
    {
        public CommandKind Kind;

        /// <summary>选色等需要颜色参数的命令使用。</summary>
        public PlayerTrainColor Color;

        /// <summary>市场取牌用槽位下标消歧，不传卡牌实例。</summary>
        public int MarketSlot = -1;

        /// <summary>认领路线用 Route.Id（即 RouteVisual.RouteId）。</summary>
        public string RouteId;

        /// <summary>认领路线要打出的车厢卡颜色（主机据此从手牌取回真实引用）。</summary>
        public TrainCarColor[] CardColors;

        /// <summary>任务卡只带保留的 DestinationTicket.Id。</summary>
        public string[] TicketIds;
    }
}