using System;
using System.Collections.Generic;
using TicketToRide.Domain;
using TicketToRide.Domain.Services;

namespace TicketToRide.Application
{
    public class GameSession
    {
        public GameState GameState { get; }
        public TurnManager TurnManager { get; }
        public RouteRuleService RuleService { get; }

        public GameSession(IEnumerable<Player> players, TrainCarDeck trainCarDeck, TrainCarMarket trainCarMarket = null, DestinationTicketDeck destinationTicketDeck = null)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            GameState = new GameState(players, trainCarDeck ?? throw new ArgumentNullException(nameof(trainCarDeck)), trainCarMarket, destinationTicketDeck);
            TurnManager = new TurnManager(GameState);
            RuleService = new RouteRuleService();
        }

        public static GameSession CreateDemoSession()
        {
            var deck = TrainCarDeck.CreateStandardDeck();
            var market = new TrainCarMarket(deck);
            var destinationTicketDeck = DestinationTicketDeck.CreateStandardDeck();
            var playerOne = new Player("player_1", "Alice", TrainCarColor.Red);

            playerOne.Hand.AddCards(deck.DrawMany(4));

            // 任务卡不再直接发到手里：开局由 DestinationDraftPanel 弹出 3 张，玩家留至少 2 张。

            var players = new List<Player> { playerOne };
            return new GameSession(players, deck, market, destinationTicketDeck);
        }
    }
}
