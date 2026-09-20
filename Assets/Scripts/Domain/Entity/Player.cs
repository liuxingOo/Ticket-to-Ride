using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    public class Player
    {
        public string Id { get; }
        public string Name { get; }
        public PlayerHand Hand { get; }
        public int TrainCarsRemaining { get; private set; }
        public int Score { get; private set; }
        public List<Route> ClaimedRoutes { get; }

        public Player(string id, string name, int startingTrainCars = 45)
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
            Hand = new PlayerHand();
            ClaimedRoutes = new List<Route>();
            TrainCarsRemaining = startingTrainCars;
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
            Score += route.Length;

            return true;
        }
    }
}
