using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    public class DestinationTicket
    {
        public string Id { get; }
        public string StartCity { get; }
        public string EndCity { get; }
        public int Points { get; }

        public DestinationTicket(string id, string startCity, string endCity, int points)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Ticket id cannot be empty.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(startCity))
            {
                throw new ArgumentException("Start city cannot be empty.", nameof(startCity));
            }

            if (string.IsNullOrWhiteSpace(endCity))
            {
                throw new ArgumentException("End city cannot be empty.", nameof(endCity));
            }

            if (points <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(points), "Ticket points must be greater than zero.");
            }

            Id = id;
            StartCity = startCity;
            EndCity = endCity;
            Points = points;
        }

        public bool IsCompleted(IEnumerable<Route> claimedRoutes)
        {
            if (claimedRoutes == null)
            {
                throw new ArgumentNullException(nameof(claimedRoutes), "Claimed routes collection cannot be null.");
            }

            var routes = claimedRoutes.ToList();
            var connectedCities = new HashSet<string> { StartCity };
            var frontier = new Queue<string>();
            frontier.Enqueue(StartCity);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                foreach (var route in routes)
                {
                    if (route == null)
                    {
                        continue;
                    }

                    if (route.StartCity == current && connectedCities.Add(route.EndCity))
                    {
                        frontier.Enqueue(route.EndCity);
                    }
                    else if (route.EndCity == current && connectedCities.Add(route.StartCity))
                    {
                        frontier.Enqueue(route.StartCity);
                    }
                }
            }

            return connectedCities.Contains(EndCity);
        }
    }
}
