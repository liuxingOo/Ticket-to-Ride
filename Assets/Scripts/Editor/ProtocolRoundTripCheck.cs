using System;
using System.Linq;
using UnityEditor;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

// UnityEditor 里也有一个 MessageType，这里显式指定用协议层的那个，避免歧义。
using MessageType = TicketToRide.Application.Net.Protocol.MessageType;

namespace TicketToRide.Application.EditorTools
{
    /// <summary>
    /// 协议编解码往返自检。
    /// 项目当前没有 asmdef，测试代码无法引用 Assembly-CSharp，所以用 Editor 菜单代替 EditMode 测试：
    /// 菜单 Tools/联机/协议往返自检 → 看 Console 输出。
    /// </summary>
    public static class ProtocolRoundTripCheck
    {
        [MenuItem("Tools/联机/协议往返自检")]
        public static void Run()
        {
            int passed = 0;
            int failed = 0;

            void Check(bool condition, string name)
            {
                if (condition)
                {
                    passed++;
                }
                else
                {
                    failed++;
                    UnityEngine.Debug.LogError($"[协议自检] 失败：{name}");
                }
            }

            // ---------- 命令往返 ----------
            var command = new GameCommand
            {
                Kind = CommandKind.ClaimRoute,
                Color = PlayerTrainColor.Blue,
                MarketSlot = 3,
                RouteId = "route_Seattle_Portland",
                CardColors = new[] { TrainCarColor.Red, TrainCarColor.Red, TrainCarColor.Locomotive },
                TicketIds = new[] { "destination_7", "destination_19" },
            };

            var commandBytes = MessageCodec.EncodeCommand(command);
            var commandBack = MessageCodec.DecodeCommand(commandBytes);

            Check(MessageCodec.ReadMessageType(commandBytes) == MessageType.Command, "命令消息头类型");
            Check(commandBack.Kind == command.Kind, "命令 Kind");
            Check(commandBack.Color == command.Color, "命令 Color");
            Check(commandBack.MarketSlot == command.MarketSlot, "命令 MarketSlot");
            Check(commandBack.RouteId == command.RouteId, "命令 RouteId");
            Check(commandBack.CardColors.SequenceEqual(command.CardColors), "命令 CardColors");
            Check(commandBack.TicketIds.SequenceEqual(command.TicketIds), "命令 TicketIds");
            Check(CommandValidator.IsWellFormed(commandBack), "合法命令应通过结构校验");
            Check(
                !CommandValidator.IsWellFormed(new GameCommand { Kind = CommandKind.TakeMarketCard, MarketSlot = -1 }),
                "缺槽位的市场取牌应被拒绝");
            Check(
                !CommandValidator.IsWellFormed(new GameCommand { Kind = CommandKind.ClaimRoute, RouteId = "" }),
                "缺路线 Id 的认领应被拒绝");

            // ---------- 报到 / 提示往返 ----------
            var helloBytes = MessageCodec.EncodeHello("  小明 ");
            Check(MessageCodec.ReadMessageType(helloBytes) == MessageType.Hello, "报到消息头类型");
            Check(MessageCodec.DecodeHello(helloBytes) == "小明", "昵称往返并去掉首尾空白");

            var longName = new string('昵', MessageCodec.MaxNicknameLength + 6);
            Check(
                MessageCodec.DecodeHello(MessageCodec.EncodeHello(longName)).Length == MessageCodec.MaxNicknameLength,
                "超长昵称应被截断");

            var noticeBytes = MessageCodec.EncodeNotice("现在不能执行该动作");
            Check(MessageCodec.ReadMessageType(noticeBytes) == MessageType.Notice, "提示消息头类型");
            Check(MessageCodec.DecodeNotice(noticeBytes) == "现在不能执行该动作", "提示往返");

            // ---------- 快照往返（含私有段） ----------
            var snapshot = new MatchSnapshot
            {
                Version = 42,
                ViewerPlayerId = "player_2",
                Phase = MatchPhase.InGame,
                ActingPlayerId = "player_2",
                DraftingPlayerId = "player_1",
                WaitingPlayerId = "player_3",
                TurnPhase = TurnActionPhase.DrawingSecondCard,
                CurrentPlayerId = "player_1",
                TurnOrderIds = new[] { "player_1", "player_2", "player_3" },
                IsFinalRound = true,
                FinalRoundTurnsRemaining = 2,
                Players = new[]
                {
                    new PublicPlayer { Id = "player_1", Name = "Alice", Color = PlayerTrainColor.Red, Score = 17, TrainCarsRemaining = 33, HandCount = 6, TicketCount = 2 },
                    new PublicPlayer { Id = "player_2", Name = "Bob", Color = PlayerTrainColor.Yellow, Score = 12, TrainCarsRemaining = 41, HandCount = 4, TicketCount = 3 },
                },
                MarketCardColors = new[] { TrainCarColor.Red, TrainCarColor.Locomotive, TrainCarColor.Black, TrainCarColor.White, TrainCarColor.Orange },
                DeckCount = 88,
                DiscardCount = 5,
                TicketDeckCount = 21,
                Routes = new[]
                {
                    new RouteState { RouteId = "route_Seattle_Portland", OwnerPlayerId = "player_1" },
                    new RouteState { RouteId = "route_Portland_San Francisco", OwnerPlayerId = "" },
                },
                FinalScores = new[]
                {
                    new FinalScoreEntry { PlayerId = "player_1", PlayerName = "Alice", Color = PlayerTrainColor.Red, Rank = 1, RouteScore = 62, TicketScore = 18, CompletedTickets = 2, TotalTickets = 3, LongestRouteLength = 27, LongestRouteBonus = 10 },
                    new FinalScoreEntry { PlayerId = "player_2", PlayerName = "Bob", Color = PlayerTrainColor.Yellow, Rank = 2, RouteScore = 48, TicketScore = -7, CompletedTickets = 1, TotalTickets = 3, LongestRouteLength = 19, LongestRouteBonus = 0 },
                },
                MyHandCardColors = new[] { TrainCarColor.Blue, TrainCarColor.Green },
                MyTicketIds = new[] { "destination_3", "destination_11" },
                MyDrawnTicketIds = new[] { "destination_3", "destination_11", "destination_24" },
            };

            var snapshotBytes = MessageCodec.EncodeSnapshot(snapshot);
            var snapshotBack = MessageCodec.DecodeSnapshot(snapshotBytes);

            Check(MessageCodec.ReadMessageType(snapshotBytes) == MessageType.Snapshot, "快照消息头类型");
            Check(SnapshotEquals(snapshot, snapshotBack), "快照往返内容一致");
            Check(
                snapshotBack.MyHandCardColors.SequenceEqual(snapshot.MyHandCardColors)
                && snapshotBack.MyTicketIds.SequenceEqual(snapshot.MyTicketIds)
                && snapshotBack.MyDrawnTicketIds.SequenceEqual(snapshot.MyDrawnTicketIds),
                "快照私有段往返一致");

            // ---------- 版本不匹配应被拒绝 ----------
            var badVersion = (byte[])commandBytes.Clone();
            badVersion[0] = 99;
            Check(Throws<System.IO.InvalidDataException>(() => MessageCodec.DecodeCommand(badVersion)), "错误协议版本应被拒绝");

            UnityEngine.Debug.Log($"[协议自检] 完成：通过 {passed} 项，失败 {failed} 项。命令 {commandBytes.Length} 字节，快照 {snapshotBytes.Length} 字节。");
        }

        private static bool Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (TException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool SnapshotEquals(MatchSnapshot a, MatchSnapshot b)
        {
            return a.Version == b.Version
                   && a.ViewerPlayerId == b.ViewerPlayerId
                   && a.Phase == b.Phase
                   && a.ActingPlayerId == b.ActingPlayerId
                   && a.DraftingPlayerId == b.DraftingPlayerId
                   && a.WaitingPlayerId == b.WaitingPlayerId
                   && a.TurnPhase == b.TurnPhase
                   && a.CurrentPlayerId == b.CurrentPlayerId
                   && SameStrings(a.TurnOrderIds, b.TurnOrderIds)
                   && a.IsFinalRound == b.IsFinalRound
                   && a.FinalRoundTurnsRemaining == b.FinalRoundTurnsRemaining
                   && SamePlayers(a.Players, b.Players)
                   && SameColors(a.MarketCardColors, b.MarketCardColors)
                   && a.DeckCount == b.DeckCount
                   && a.DiscardCount == b.DiscardCount
                   && a.TicketDeckCount == b.TicketDeckCount
                   && SameRoutes(a.Routes, b.Routes)
                   && SameFinalScores(a.FinalScores, b.FinalScores);
        }

        private static bool SameStrings(string[] a, string[] b) => a != null && b != null && a.SequenceEqual(b);
        private static bool SameColors(TrainCarColor[] a, TrainCarColor[] b) => a != null && b != null && a.SequenceEqual(b);

        private static bool SameFinalScores(FinalScoreEntry[] a, FinalScoreEntry[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].PlayerId != b[i].PlayerId || a[i].PlayerName != b[i].PlayerName
                    || a[i].Color != b[i].Color || a[i].Rank != b[i].Rank
                    || a[i].RouteScore != b[i].RouteScore || a[i].TicketScore != b[i].TicketScore
                    || a[i].CompletedTickets != b[i].CompletedTickets || a[i].TotalTickets != b[i].TotalTickets
                    || a[i].LongestRouteLength != b[i].LongestRouteLength
                    || a[i].LongestRouteBonus != b[i].LongestRouteBonus)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SamePlayers(PublicPlayer[] a, PublicPlayer[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].Id != b[i].Id || a[i].Name != b[i].Name || a[i].Color != b[i].Color
                    || a[i].Score != b[i].Score || a[i].TrainCarsRemaining != b[i].TrainCarsRemaining
                    || a[i].HandCount != b[i].HandCount || a[i].TicketCount != b[i].TicketCount)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameRoutes(RouteState[] a, RouteState[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].RouteId != b[i].RouteId || a[i].OwnerPlayerId != b[i].OwnerPlayerId)
                {
                    return false;
                }
            }

            return true;
        }
    }
}