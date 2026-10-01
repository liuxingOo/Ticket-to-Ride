using System;
using System.Linq;
using UnityEditor;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Domain;

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

            // ---------- 快照往返（含私有段） ----------
            var snapshot = new MatchSnapshot
            {
                Version = 42,
                Phase = MatchPhase.InGame,
                ActingPlayerId = "player_2",
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
                   && a.Phase == b.Phase
                   && a.ActingPlayerId == b.ActingPlayerId
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
                   && SameRoutes(a.Routes, b.Routes);
        }

        private static bool SameStrings(string[] a, string[] b) => a != null && b != null && a.SequenceEqual(b);
        private static bool SameColors(TrainCarColor[] a, TrainCarColor[] b) => a != null && b != null && a.SequenceEqual(b);

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