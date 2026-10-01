using System;
using System.Collections.Generic;
using System.Linq;

namespace TicketToRide.Domain
{
    public class TurnManager
    {
        private readonly GameState _gameState;

        public TurnManager(GameState gameState)
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        }

        public TrainCarCard DrawTrainCard()
        {
            if (_gameState.IsGameOver)
            {
                throw new InvalidOperationException("游戏已经结束，不能再抽牌。");
            }

            if (_gameState.TurnPhase == TurnActionPhase.ActionCompleted)
            {
                throw new InvalidOperationException("本回合已经执行过动作。");
            }

            var card = _gameState.TrainCarDeck.DrawOne();
            _gameState.CurrentPlayer.Hand.AddCard(card);

            // 盲抽（即使盲抽到车头）都可以再抽第二张；抽满两张本回合动作结束。
            _gameState.SetTurnPhase(_gameState.TurnPhase == TurnActionPhase.AwaitingAction
                ? TurnActionPhase.DrawingSecondCard
                : TurnActionPhase.ActionCompleted);

            return card;
        }

        public TrainCarCard TakeOneMarketCard(TrainCarCard card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card), "Cannot take a null market card.");
            }

            if (_gameState.IsGameOver)
            {
                throw new InvalidOperationException("游戏已经结束，不能再抽牌。");
            }

            var phase = _gameState.TurnPhase;
            if (phase == TurnActionPhase.ActionCompleted)
            {
                throw new InvalidOperationException("本回合已经执行过动作。");
            }

            // 官方规则：明牌车头不能作为本回合的第二张车票拿取。
            if (card.IsLocomotive && phase == TurnActionPhase.DrawingSecondCard)
            {
                throw new InvalidOperationException("明牌车头不能作为本回合的第二张车票拿取。");
            }

            var taken = _gameState.TrainCarMarket.TakeVisibleCards(new List<TrainCarCard> { card });
            if (taken.Count == 0)
            {
                throw new InvalidOperationException("The selected market card could not be taken.");
            }

            _gameState.CurrentPlayer.Hand.AddCards(taken);

            // 明牌车头一次就吃掉两次抽牌机会，本回合动作直接结束。
            _gameState.SetTurnPhase(card.IsLocomotive || phase == TurnActionPhase.DrawingSecondCard
                ? TurnActionPhase.ActionCompleted
                : TurnActionPhase.DrawingSecondCard);

            return taken[0];
        }

        public void DiscardTrainCard(TrainCarCard card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card), "Card cannot be null.");
            }

            if (!_gameState.CurrentPlayer.Hand.RemoveCard(card))
            {
                throw new InvalidOperationException("The current player does not have this card.");
            }

            _gameState.TrainCarDeck.DiscardCard(card);
        }

        public void DrawTrainCards(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Draw count must be greater than zero.");
            }

            for (int i = 0; i < count; i++)
            {
                DrawTrainCard();
            }
        }

        /// <summary>
        /// 任务卡挑选结算：保留的加入指定玩家；只要有弃卡，就放回牌堆底并洗一次牌
        /// （开局挑选完、游戏中抽选走同一个方法）。
        /// consumesTurnAction=false 用于开局前的手牌挑选，它不属于回合动作、不改变回合状态。
        /// </summary>
        public void ChooseDestinationTickets(
            Player player,
            IReadOnlyCollection<DestinationTicket> kept,
            IReadOnlyCollection<DestinationTicket> discarded,
            bool consumesTurnAction = true)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (kept == null)
            {
                throw new ArgumentNullException(nameof(kept));
            }

            if (discarded == null)
            {
                throw new ArgumentNullException(nameof(discarded));
            }

            if (_gameState.IsGameOver)
            {
                throw new InvalidOperationException("游戏已经结束，不能再抽任务卡。");
            }

            if (consumesTurnAction && _gameState.TurnPhase != TurnActionPhase.AwaitingAction)
            {
                throw new InvalidOperationException("本回合已经执行过动作。");
            }

            player.AddDestinationTickets(kept);

            if (discarded.Count > 0)
            {
                _gameState.DestinationTicketDeck.ReturnToBottom(discarded);
                _gameState.DestinationTicketDeck.Shuffle();
            }

            if (consumesTurnAction)
            {
                _gameState.SetTurnPhase(TurnActionPhase.ActionCompleted);
            }
        }

        public bool ClaimRoute(Route route, IEnumerable<TrainCarCard> playedCards)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route), "Route cannot be null.");
            }

            if (playedCards == null)
            {
                throw new ArgumentNullException(nameof(playedCards), "Played cards cannot be null.");
            }

            if (_gameState.IsGameOver)
            {
                return false;
            }

            if (_gameState.TurnPhase != TurnActionPhase.AwaitingAction)
            {
                return false;
            }

            var currentPlayer = _gameState.CurrentPlayer;
            var list = playedCards.ToList();

            if (!currentPlayer.CanClaimRoute(route, list))
            {
                return false;
            }

            // 双轨规则：2–3 人局同城市对只能用一条；4–5 人局同一玩家不能占两条。
            if (_gameState.IsBlockedByParallelRule(route, currentPlayer))
            {
                return false;
            }

            if (!currentPlayer.TryClaimRoute(route, list))
            {
                return false;
            }

            // 打出的车厢卡进弃牌堆：牌堆抽空后这些牌会被重洗回流（官方规则要求）。
            _gameState.TrainCarDeck.DiscardCards(list);

            _gameState.SetTurnPhase(TurnActionPhase.ActionCompleted);
            return true;
        }

        public void EndTurn()
        {
            if (_gameState.TurnPhase != TurnActionPhase.ActionCompleted)
            {
                throw new InvalidOperationException("本回合还没有执行动作，不能切换到下一位玩家。");
            }

            _gameState.NextTurn();
        }
    }
}
