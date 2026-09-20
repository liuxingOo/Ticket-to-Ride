using System;
using System.Collections.Generic;
using System.Linq;
using TicketToRide.Domain;

namespace TicketToRide.Domain.Services
{
    public class RouteRuleService
    {
        public bool CanPlayerClaimRoute(Player player, Route route, IEnumerable<TrainCarCard> playedCards)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player), "Player cannot be null.");
            }

            if (route == null)
            {
                throw new ArgumentNullException(nameof(route), "Route cannot be null.");
            }

            if (playedCards == null)
            {
                throw new ArgumentNullException(nameof(playedCards), "Played cards cannot be null.");
            }

            var cardList = playedCards.ToList();

            if (route.IsClaimed)
            {
                return false;
            }

            if (route.Length > player.TrainCarsRemaining)
            {
                return false;
            }

            if (cardList.Count != route.Length)
            {
                return false;
            }

            var remainingCards = player.Hand.GetCards().ToList();

            foreach (var card in cardList)
            {
                if (card == null)
                {
                    return false;
                }

                var matchIndex = remainingCards.FindIndex(handCard => handCard.Equals(card));
                if (matchIndex < 0)
                {
                    return false;
                }

                remainingCards.RemoveAt(matchIndex);
            }

            if (route.Color == null)
            {
                return true;
            }

            var validColorMatches = cardList.Count(card => card.IsLocomotive || card.Color == route.Color.Value);
            return validColorMatches == cardList.Count;
        }
    }
}
