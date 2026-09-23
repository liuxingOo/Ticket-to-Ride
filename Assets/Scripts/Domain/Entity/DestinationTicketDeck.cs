using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    public class DestinationTicketDeck
    {
        private readonly Queue<DestinationTicket> _tickets;

        public int Count => _tickets.Count;
        public bool IsEmpty => _tickets.Count == 0;

        public DestinationTicketDeck(IEnumerable<DestinationTicket> tickets)
        {
            _tickets = new Queue<DestinationTicket>(tickets ?? throw new ArgumentNullException(nameof(tickets)));
        }

        public static DestinationTicketDeck CreateStandardDeck()
        {
            // 第 4 个数字是卡面序号：destinations.png 10×3 网格的位置（从左到右、从上到下 0-29）。
            // 城市名必须与场景 CityMarker.cityId 完全一致（Sault St. Marie 带句点、Saint Louis 全拼）。
            var tickets = new[]
            {
                ("Los Angeles", "New York", 21, 14), ("Duluth", "Houston", 8, 9),
                ("Sault St. Marie", "Nashville", 8, 21), ("New York", "Atlanta", 6, 17),
                ("Portland", "Nashville", 17, 18), ("Vancouver", "Montreal", 20, 26),
                ("Duluth", "El Paso", 10, 8), ("Toronto", "Miami", 10, 25),
                ("Portland", "Phoenix", 11, 19), ("Dallas", "New York", 11, 5),
                ("Calgary", "Salt Lake City", 7, 2), ("Calgary", "Phoenix", 13, 1),
                ("Los Angeles", "Miami", 20, 13), ("Winnipeg", "Little Rock", 11, 29),
                ("San Francisco", "Atlanta", 17, 20), ("Kansas City", "Houston", 5, 11),
                ("Los Angeles", "Chicago", 16, 12), ("Denver", "Pittsburgh", 11, 7),
                ("Chicago", "Santa Fe", 9, 4), ("Vancouver", "Santa Fe", 13, 27),
                ("Boston", "Miami", 12, 0), ("Chicago", "New Orleans", 7, 3),
                ("Montreal", "Atlanta", 9, 15), ("Seattle", "Los Angeles", 9, 23),
                ("Sault St. Marie", "Oklahoma City", 9, 22), ("Helena", "Los Angeles", 8, 10),
                ("Winnipeg", "Houston", 12, 28), ("Denver", "El Paso", 4, 6),
                ("Seattle", "New York", 22, 24), ("Montreal", "New Orleans", 13, 16)
            };

            var deck = new DestinationTicketDeck(tickets.Select((ticket, index) =>
                new DestinationTicket($"destination_{index + 1}", ticket.Item1, ticket.Item2, ticket.Item3, ticket.Item4)));
            deck.Shuffle();
            return deck;
        }

        public List<DestinationTicket> DrawMany(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var drawnTickets = new List<DestinationTicket>();
            for (var index = 0; index < count && !IsEmpty; index++)
            {
                drawnTickets.Add(_tickets.Dequeue());
            }

            return drawnTickets;
        }

        /// <summary>把不要的任务卡放回牌堆底（开局挑选、游戏中抽选都走这里）。</summary>
        public void ReturnToBottom(IEnumerable<DestinationTicket> tickets)
        {
            if (tickets == null)
            {
                throw new ArgumentNullException(nameof(tickets));
            }

            foreach (var ticket in tickets)
            {
                if (ticket != null)
                {
                    _tickets.Enqueue(ticket);
                }
            }
        }

        /// <summary>洗牌（开局挑选完、游戏中抽选有弃卡时调用）。</summary>
        public void Shuffle()
        {
            var tickets = _tickets.OrderBy(_ => Guid.NewGuid()).ToList();
            _tickets.Clear();

            foreach (var ticket in tickets)
            {
                _tickets.Enqueue(ticket);
            }
        }
    }
}
