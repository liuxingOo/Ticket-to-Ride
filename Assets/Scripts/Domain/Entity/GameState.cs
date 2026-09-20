using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    public class GameState
    {
        public List<Player> Players { get; }
        public TrainCarDeck TrainCarDeck { get; }
        public TrainCarMarket TrainCarMarket { get; }
        public Queue<Player> TurnOrder { get; }
        public Player CurrentPlayer => TurnOrder.Peek();
        public bool IsGameOver { get; private set; }

        public GameState(IEnumerable<Player> players, TrainCarDeck trainCarDeck, TrainCarMarket trainCarMarket = null)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players), "Players collection cannot be null.");
            }

            var playerList = players.ToList();
            if (playerList.Count == 0)
            {
                throw new ArgumentException("At least one player is required.", nameof(players));
            }

            if (trainCarDeck == null)
            {
                throw new ArgumentNullException(nameof(trainCarDeck), "Train car deck cannot be null.");
            }

            Players = playerList;
            TrainCarDeck = trainCarDeck;
            TrainCarMarket = trainCarMarket ?? new TrainCarMarket(trainCarDeck);
            TurnOrder = new Queue<Player>(playerList);
        }

        public void NextTurn()
        {
            var currentPlayer = TurnOrder.Dequeue();
            TurnOrder.Enqueue(currentPlayer);
        }

        public void EndGame()
        {
            IsGameOver = true;
        }
    }
}
