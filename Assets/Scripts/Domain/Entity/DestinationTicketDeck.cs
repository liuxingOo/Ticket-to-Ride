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
            var tickets = new[]
            {
                ("Los Angeles", "New York", 21), ("Duluth", "Houston", 8),
                ("Sault Ste. Marie", "Nashville", 8), ("New York", "Atlanta", 6),
                ("Portland", "Nashville", 17), ("Vancouver", "Montreal", 20),
                ("Duluth", "El Paso", 10), ("Toronto", "Miami", 10),
                ("Portland", "Phoenix", 11), ("Dallas", "New York", 11),
                ("Calgary", "Salt Lake City", 7), ("Calgary", "Phoenix", 13),
                ("Los Angeles", "Miami", 20), ("Winnipeg", "Little Rock", 11),
                ("San Francisco", "Atlanta", 17), ("Kansas City", "Houston", 5),
                ("Los Angeles", "Chicago", 16), ("Denver", "Pittsburgh", 11),
                ("Chicago", "Santa Fe", 9), ("Vancouver", "Santa Fe", 13),
                ("Boston", "Miami", 12), ("Chicago", "New Orleans", 7),
                ("Montreal", "Atlanta", 9), ("Seattle", "Los Angeles", 9),
                ("Sault Ste. Marie", "Oklahoma City", 9), ("Helena", "Los Angeles", 8),
                ("Winnipeg", "Houston", 12), ("Denver", "El Paso", 4),
                ("Seattle", "New York", 22), ("Montreal", "New Orleans", 13)
            };

            var deck = new DestinationTicketDeck(tickets.Select((ticket, index) =>
                new DestinationTicket($"destination_{index + 1}", ticket.Item1, ticket.Item2, ticket.Item3)));
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

        private void Shuffle()
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
