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
            var playerOne = new Player("player_1", "Alice", PlayerTrainColor.Red);

            // 初始车票不在创建时发：开局先走列车颜色选择，选完后由 DealInitialTrainCards 统一发，
            // 随后才弹 DestinationDraftPanel 做任务卡 3 选 2。
            var players = new List<Player> { playerOne };
            return new GameSession(players, deck, market, destinationTicketDeck);
        }

        /// <summary>所有玩家选完列车颜色后调用：给每位玩家发初始车票（标准版每人 4 张）。</summary>
        public void DealInitialTrainCards(int perPlayer = 4)
        {
            foreach (var player in GameState.Players)
            {
                player.Hand.AddCards(GameState.TrainCarDeck.DrawMany(perPlayer));
            }
        }
    }
}
