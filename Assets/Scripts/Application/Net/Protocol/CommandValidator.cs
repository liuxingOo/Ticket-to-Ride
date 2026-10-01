using System;

namespace TicketToRide.Application.Net.Protocol
{
    /// <summary>
    /// 协议层的结构校验：确认一条命令的字段自洽，把畸形 / 恶意负载挡在领域层之外。
    /// 注意：这里不做规则判断（是否轮到该玩家、手牌够不够、路线是否已占）——
    /// 那些由权威端调用领域层来裁决。
    /// </summary>
    public static class CommandValidator
    {
        public static bool IsWellFormed(GameCommand command)
        {
            if (command == null)
            {
                return false;
            }

            switch (command.Kind)
            {
                case CommandKind.TakeMarketCard:
                    // 市场取牌必须带合法槽位下标，绝不接受卡牌实例。
                    return command.MarketSlot >= 0;

                case CommandKind.ClaimRoute:
                    // 认领必须带路线 Id 与要打出的车厢卡颜色。
                    return !string.IsNullOrWhiteSpace(command.RouteId)
                           && command.CardColors != null
                           && command.CardColors.Length > 0;

                case CommandKind.ChooseDestinationTickets:
                case CommandKind.PickInitialTickets:
                    // 结算任务卡必须带保留的 Id 列表。
                    return command.TicketIds != null && command.TicketIds.Length > 0;

                case CommandKind.SetPlayerCount:
                case CommandKind.ChooseColor:
                case CommandKind.DrawTrainCard:
                case CommandKind.DrawDestinationTickets:
                    return true;

                default:
                    return false;
            }
        }
    }
}