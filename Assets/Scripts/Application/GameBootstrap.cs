using System.Collections.Generic;
using TicketToRide.Domain;

namespace TicketToRide.Application
{
    public static class GameBootstrap
    {
        public static (GameState GameState, TurnManager TurnManager, List<Route> Routes) CreateDemoGame()
        {
            var deck = TrainCarDeck.CreateStandardDeck();

            var playerOne = new Player("player_1", "Alice", PlayerTrainColor.Red);
            var playerTwo = new Player("player_2", "Bob", PlayerTrainColor.Blue);

            playerOne.Hand.AddCards(deck.DrawMany(4));
            playerTwo.Hand.AddCards(deck.DrawMany(4));

            var players = new List<Player> { playerOne, playerTwo };
            var gameState = new GameState(players, deck);
            var turnManager = new TurnManager(gameState);

            var routes = new List<Route>
            {
                new Route("route_1", "Seattle", "Portland", 2, TrainCarColor.Green),
                new Route("route_2", "Portland", "San Francisco", 3, TrainCarColor.Red),
                new Route("route_3", "San Francisco", "Los Angeles", 2, TrainCarColor.Black)
            };

            var demoTicket = new DestinationTicket("ticket_1", "Seattle", "Los Angeles", 10);

            return (gameState, turnManager, routes);
        }
    }
}
