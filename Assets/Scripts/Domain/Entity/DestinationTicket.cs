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

        /// <summary>卡面图序号：destinations.png 10×3 网格中的位置（从左到右、从上到下 0-29）。</summary>
        public int ArtIndex { get; }

        public DestinationTicket(string id, string startCity, string endCity, int points, int artIndex = 0)
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
            ArtIndex = artIndex;
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
