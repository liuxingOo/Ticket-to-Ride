using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    public class Route
    {
        public string Id { get; }
        public string StartCity { get; }
        public string EndCity { get; }
        public int Length { get; }
        public TrainCarColor? Color { get; }
        public string OwnerPlayerId { get; private set; }
        public bool IsClaimed => !string.IsNullOrWhiteSpace(OwnerPlayerId);

        public Route(string id, string startCity, string endCity, int length, TrainCarColor? color = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Route id cannot be empty.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(startCity))
            {
                throw new ArgumentException("Start city cannot be empty.", nameof(startCity));
            }

            if (string.IsNullOrWhiteSpace(endCity))
            {
                throw new ArgumentException("End city cannot be empty.", nameof(endCity));
            }

            if (length < 1 || length > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Route length must be between 1 and 6.");
            }

            Id = id;
            StartCity = startCity;
            EndCity = endCity;
            Length = length;
            Color = color;
        }

        public bool CanBeClaimedWith(IEnumerable<TrainCarCard> cards)
        {
            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards), "Cards collection cannot be null.");
            }

            if (IsClaimed)
            {
                return false;
            }

            var cardList = cards.ToList();
            if (cardList.Count != Length)
            {
                return false;
            }

            if (Color == null)
            {
                return true;
            }

            return cardList.All(card => card.IsLocomotive || card.Color == Color.Value);
        }

        public void ClaimBy(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id cannot be empty.", nameof(playerId));
            }

            if (IsClaimed)
            {
                throw new InvalidOperationException("This route has already been claimed.");
            }

            OwnerPlayerId = playerId;
        }
    }
}
