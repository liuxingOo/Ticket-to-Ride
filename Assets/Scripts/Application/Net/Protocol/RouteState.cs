namespace TicketToRide.Application.Net.Protocol
{
    /// <summary>
    /// 快照里一条路线的归属状态。OwnerPlayerId 为空表示尚未被认领。
    /// </summary>
    public class RouteState
    {
        public string RouteId;
        public string OwnerPlayerId;
    }
}