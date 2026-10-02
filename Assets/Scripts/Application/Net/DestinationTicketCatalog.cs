using System.Collections.Generic;
using TicketToRide.Domain;

namespace TicketToRide.Application.Net
{
    /// <summary>
    /// 任务卡的静态图鉴：Id → DestinationTicket。
    /// 任务卡的城市 / 分数 / 卡面图都是固定不变的数据，Id（destination_1..30）在
    /// DestinationTicketDeck.CreateStandardDeck 里也是确定分配的（只有发牌顺序被洗）。
    /// 客户端只拿到 Id，靠这张图鉴还原卡面所需的全部信息——它镜像的是静态牌面，不是权威可变状态。
    /// </summary>
    public static class DestinationTicketCatalog
    {
        private static readonly Dictionary<string, DestinationTicket> ById = Build();

        /// <summary>按 Id 取任务卡；找不到返回 null。</summary>
        public static DestinationTicket Get(string ticketId)
        {
            if (string.IsNullOrEmpty(ticketId))
            {
                return null;
            }

            return ById.TryGetValue(ticketId, out var ticket) ? ticket : null;
        }

        private static Dictionary<string, DestinationTicket> Build()
        {
            var map = new Dictionary<string, DestinationTicket>();
            var deck = DestinationTicketDeck.CreateStandardDeck();
            foreach (var ticket in deck.DrawMany(deck.Count))
            {
                map[ticket.Id] = ticket;
            }

            return map;
        }
    }
}