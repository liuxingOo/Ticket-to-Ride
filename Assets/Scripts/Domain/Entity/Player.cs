using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    public class Player
    {
        public string Id { get; }
        public string Name { get; }
        public TrainCarColor Color { get; }
        public PlayerHand Hand { get; }
        public int TrainCarsRemaining { get; private set; }
        public int Score { get; private set; }
        public List<Route> ClaimedRoutes { get; }
        public List<DestinationTicket> DestinationTickets { get; }

        public Player(string id, string name, TrainCarColor color, int startingTrainCars = 45)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Player id cannot be empty.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Player name cannot be empty.", nameof(name));
            }

            if (startingTrainCars <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingTrainCars), "Starting train count must be greater than zero.");
            }

            Id = id;
            Name = name;
            Color = color;
            Hand = new PlayerHand();
            ClaimedRoutes = new List<Route>();
            DestinationTickets = new List<DestinationTicket>();
            TrainCarsRemaining = startingTrainCars;
        }

        public void AddDestinationTickets(IEnumerable<DestinationTicket> tickets)
        {
            if (tickets == null)
            {
                throw new ArgumentNullException(nameof(tickets));
            }

            foreach (var ticket in tickets)
            {
                if (ticket == null)
                {
                    throw new ArgumentException("Destination tickets cannot contain null.", nameof(tickets));
                }

                DestinationTickets.Add(ticket);
            }
        }

        public int GetDestinationTicketScore()
        {
            return DestinationTickets.Sum(ticket => ticket.IsCompleted(ClaimedRoutes) ? ticket.Points : -ticket.Points);
        }

        public bool CanClaimRoute(Route route, IEnumerable<TrainCarCard> playedCards)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route), "Route cannot be null.");
            }

            if (playedCards == null)
            {
                throw new ArgumentNullException(nameof(playedCards), "Played cards collection cannot be null.");
            }

            if (route.IsClaimed)
            {
                return false;
            }

            if (route.Length > TrainCarsRemaining)
            {
                return false;
            }

            if (!route.CanBeClaimedWith(playedCards))
            {
                return false;
            }

            var requiredCards = playedCards.ToList();
            var remainingHand = Hand.GetCards().ToList();

            foreach (var card in requiredCards)
            {
                int index = remainingHand.FindIndex(handCard => handCard.Equals(card));
                if (index < 0)
                {
                    return false;
                }

                remainingHand.RemoveAt(index);
            }

            return true;
        }

        public bool TryClaimRoute(Route route, IEnumerable<TrainCarCard> playedCards)
        {
            if (!CanClaimRoute(route, playedCards))
            {
                return false;
            }

            var cardList = playedCards.ToList();

            foreach (var card in cardList)
            {
                Hand.RemoveCard(card);
            }

            route.ClaimBy(Id);
            ClaimedRoutes.Add(route);
            TrainCarsRemaining -= route.Length;
            Score += GetRouteScore(route.Length);

            return true;
        }

        private static int GetRouteScore(int routeLength)
        {
            switch (routeLength)
            {
                case 1:
                    return 1;
                case 2:
                    return 2;
                case 3:
                    return 4;
                case 4:
                    return 7;
                case 5:
                    return 10;
                case 6:
                    return 15;
                default:
                    throw new ArgumentOutOfRangeException(nameof(routeLength));
            }
        }
    }
}
