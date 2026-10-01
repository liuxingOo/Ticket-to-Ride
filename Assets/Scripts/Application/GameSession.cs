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

        /// <summary>开局可选的默认玩家名，下标对应人数-1（最多 5 人）。</summary>
        private static readonly string[] DefaultPlayerNames = { "Alice", "Bob", "Carol", "Dave", "Erin" };

        /// <summary>
        /// 按人数创建一局游戏。默认列车颜色按官方行动顺序发放（红→黄→蓝→绿→紫），
        /// 随后由开局选色面板逐个覆盖；所有玩家选完后 GameState.RebuildTurnOrderByColor 会重排行动顺序。
        /// </summary>
        public static GameSession CreateSession(int playerCount)
        {
            if (playerCount < 1 || playerCount > GameState.TurnOrderColors.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerCount),
                    $"玩家人数必须在 1 到 {GameState.TurnOrderColors.Length} 之间。");
            }

            var deck = TrainCarDeck.CreateStandardDeck();
            var market = new TrainCarMarket(deck);
            var destinationTicketDeck = DestinationTicketDeck.CreateStandardDeck();

            // 初始车票不在创建时发：开局先走列车颜色选择，选完后由 DealInitialTrainCards 统一发，
            // 随后才弹 DestinationDraftPanel 做任务卡挑选。
            var players = new List<Player>();
            for (int i = 0; i < playerCount; i++)
            {
                players.Add(new Player($"player_{i + 1}", DefaultPlayerNames[i], GameState.TurnOrderColors[i]));
            }

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
