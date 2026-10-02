using System;
using System.IO;
using TicketToRide.Domain;

namespace TicketToRide.Application.Net.Protocol
{
    /// <summary>消息类型（消息头的第 2 个字节）。</summary>
    public enum MessageType : byte
    {
        Command = 1,
        Snapshot = 2,

        /// <summary>客户端 → 主机：报到，携带自己的昵称。</summary>
        Hello = 3,

        /// <summary>主机 → 客户端：一次性提示（命令被拒绝等）。</summary>
        Notice = 4,
    }

    /// <summary>
    /// 手写小端二进制编解码：不用反射，IL2CPP 安全，比 JSON 省一半带宽。
    /// 消息格式统一为 [1B 协议版本][1B 消息类型][payload]。
    /// 空字符串用于表示「无」（例如未认领路线的 OwnerPlayerId），不写 null。
    /// </summary>
    public static class MessageCodec
    {
        // 2：快照新增 ViewerPlayerId（开局阶段机需要客户端知道「我是谁」），与 1 不兼容。
        // 3：快照新增 DraftingPlayerId（客户端显示「谁在做什么」），与 2 不兼容。
        // 4：快照新增终局结算表 FinalScores（客户端也要能看结算面板），与 3 不兼容。
        // 5：新增 Hello / Notice 两种消息（客户端上报昵称、主机回提示），与 4 不兼容。
        // 6：快照新增 WaitingPlayerId（界面提示在等谁接入），与 5 不兼容。
        public const byte ProtocolVersion = 6;

        /// <summary>昵称长度上限：协议层直接截断，防止畸形包撑爆内存或刷屏。</summary>
        public const int MaxNicknameLength = 16;

        // ============================ 命令 ============================

        public static byte[] EncodeCommand(GameCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(ProtocolVersion);
            writer.Write((byte)MessageType.Command);
            writer.Write((byte)command.Kind);
            writer.Write((byte)command.Color);
            writer.Write(command.MarketSlot);
            WriteString(writer, command.RouteId);
            WriteColors(writer, command.CardColors);
            WriteStrings(writer, command.TicketIds);

            writer.Flush();
            return stream.ToArray();
        }

        public static GameCommand DecodeCommand(byte[] data)
        {
            using var reader = BeginRead(data, MessageType.Command);

            var command = new GameCommand
            {
                Kind = (CommandKind)ReadEnumByte(reader, MaxCommandKind, nameof(GameCommand.Kind)),
                Color = (PlayerTrainColor)ReadEnumByte(reader, MaxPlayerTrainColor, nameof(GameCommand.Color)),
                MarketSlot = reader.ReadInt32(),
            };

            command.RouteId = reader.ReadString();
            command.CardColors = ReadColors(reader);
            command.TicketIds = ReadStrings(reader);

            return command;
        }

        // ============================ 报到 / 提示 ============================

        public static byte[] EncodeHello(string nickname)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(ProtocolVersion);
            writer.Write((byte)MessageType.Hello);
            WriteString(writer, nickname);

            writer.Flush();
            return stream.ToArray();
        }

        public static string DecodeHello(byte[] data)
        {
            using var reader = BeginRead(data, MessageType.Hello);

            var nickname = reader.ReadString();
            if (nickname.Length > MaxNicknameLength)
            {
                nickname = nickname.Substring(0, MaxNicknameLength);
            }

            return nickname.Trim();
        }

        public static byte[] EncodeNotice(string text)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(ProtocolVersion);
            writer.Write((byte)MessageType.Notice);
            WriteString(writer, text);

            writer.Flush();
            return stream.ToArray();
        }

        public static string DecodeNotice(byte[] data)
        {
            using var reader = BeginRead(data, MessageType.Notice);
            return reader.ReadString();
        }

        // ============================ 快照 ============================

        public static byte[] EncodeSnapshot(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(ProtocolVersion);
            writer.Write((byte)MessageType.Snapshot);

            writer.Write(snapshot.Version);
            WriteString(writer, snapshot.ViewerPlayerId);
            writer.Write((byte)snapshot.Phase);
            WriteString(writer, snapshot.ActingPlayerId);
            WriteString(writer, snapshot.DraftingPlayerId);
            WriteString(writer, snapshot.WaitingPlayerId);
            writer.Write((byte)snapshot.TurnPhase);
            WriteString(writer, snapshot.CurrentPlayerId);
            WriteStrings(writer, snapshot.TurnOrderIds);
            writer.Write(snapshot.IsFinalRound);
            writer.Write(snapshot.FinalRoundTurnsRemaining);

            writer.Write(snapshot.Players?.Length ?? 0);
            if (snapshot.Players != null)
            {
                foreach (var player in snapshot.Players)
                {
                    WriteString(writer, player.Id);
                    WriteString(writer, player.Name);
                    writer.Write((byte)player.Color);
                    writer.Write(player.Score);
                    writer.Write(player.TrainCarsRemaining);
                    writer.Write(player.HandCount);
                    writer.Write(player.TicketCount);
                }
            }

            WriteColors(writer, snapshot.MarketCardColors);
            writer.Write(snapshot.DeckCount);
            writer.Write(snapshot.DiscardCount);
            writer.Write(snapshot.TicketDeckCount);

            writer.Write(snapshot.Routes?.Length ?? 0);
            if (snapshot.Routes != null)
            {
                foreach (var route in snapshot.Routes)
                {
                    WriteString(writer, route.RouteId);
                    WriteString(writer, route.OwnerPlayerId);
                }
            }

            // 终局结算表：未结束时长度为 0。
            writer.Write(snapshot.FinalScores?.Length ?? 0);
            if (snapshot.FinalScores != null)
            {
                foreach (var entry in snapshot.FinalScores)
                {
                    WriteString(writer, entry.PlayerId);
                    WriteString(writer, entry.PlayerName);
                    writer.Write((byte)entry.Color);
                    writer.Write(entry.Rank);
                    writer.Write(entry.RouteScore);
                    writer.Write(entry.TicketScore);
                    writer.Write(entry.CompletedTickets);
                    writer.Write(entry.TotalTickets);
                    writer.Write(entry.LongestRouteLength);
                    writer.Write(entry.LongestRouteBonus);
                }
            }

            // 私有段
            WriteColors(writer, snapshot.MyHandCardColors);
            WriteStrings(writer, snapshot.MyTicketIds);
            WriteStrings(writer, snapshot.MyDrawnTicketIds);

            writer.Flush();
            return stream.ToArray();
        }

        public static MatchSnapshot DecodeSnapshot(byte[] data)
        {
            using var reader = BeginRead(data, MessageType.Snapshot);

            var snapshot = new MatchSnapshot
            {
                Version = reader.ReadInt32(),
                ViewerPlayerId = reader.ReadString(),
                Phase = (MatchPhase)ReadEnumByte(reader, MaxMatchPhase, nameof(MatchSnapshot.Phase)),
                ActingPlayerId = reader.ReadString(),
                DraftingPlayerId = reader.ReadString(),
                WaitingPlayerId = reader.ReadString(),
                TurnPhase = (TurnActionPhase)ReadEnumByte(reader, MaxTurnActionPhase, nameof(MatchSnapshot.TurnPhase)),
                CurrentPlayerId = reader.ReadString(),
                TurnOrderIds = ReadStrings(reader),
                IsFinalRound = reader.ReadBoolean(),
                FinalRoundTurnsRemaining = reader.ReadInt32(),
            };

            int playerCount = reader.ReadInt32();
            snapshot.Players = new PublicPlayer[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                snapshot.Players[i] = new PublicPlayer
                {
                    Id = reader.ReadString(),
                    Name = reader.ReadString(),
                    Color = (PlayerTrainColor)ReadEnumByte(reader, MaxPlayerTrainColor, nameof(PublicPlayer.Color)),
                    Score = reader.ReadInt32(),
                    TrainCarsRemaining = reader.ReadInt32(),
                    HandCount = reader.ReadInt32(),
                    TicketCount = reader.ReadInt32(),
                };
            }

            snapshot.MarketCardColors = ReadColors(reader);
            snapshot.DeckCount = reader.ReadInt32();
            snapshot.DiscardCount = reader.ReadInt32();
            snapshot.TicketDeckCount = reader.ReadInt32();

            int routeCount = reader.ReadInt32();
            snapshot.Routes = new RouteState[routeCount];
            for (int i = 0; i < routeCount; i++)
            {
                snapshot.Routes[i] = new RouteState
                {
                    RouteId = reader.ReadString(),
                    OwnerPlayerId = reader.ReadString(),
                };
            }

            int finalScoreCount = reader.ReadInt32();
            snapshot.FinalScores = new FinalScoreEntry[finalScoreCount];
            for (int i = 0; i < finalScoreCount; i++)
            {
                snapshot.FinalScores[i] = new FinalScoreEntry
                {
                    PlayerId = reader.ReadString(),
                    PlayerName = reader.ReadString(),
                    Color = (PlayerTrainColor)ReadEnumByte(reader, MaxPlayerTrainColor, nameof(FinalScoreEntry.Color)),
                    Rank = reader.ReadInt32(),
                    RouteScore = reader.ReadInt32(),
                    TicketScore = reader.ReadInt32(),
                    CompletedTickets = reader.ReadInt32(),
                    TotalTickets = reader.ReadInt32(),
                    LongestRouteLength = reader.ReadInt32(),
                    LongestRouteBonus = reader.ReadInt32(),
                };
            }

            // 私有段
            snapshot.MyHandCardColors = ReadColors(reader);
            snapshot.MyTicketIds = ReadStrings(reader);
            snapshot.MyDrawnTicketIds = ReadStrings(reader);

            return snapshot;
        }

        // ============================ 消息头 ============================

        /// <summary>只读消息头，判断类型；版本不匹配或类型非法时抛 InvalidDataException。</summary>
        public static MessageType ReadMessageType(byte[] data)
        {
            if (data == null || data.Length < 2)
            {
                throw new InvalidDataException("消息太短，无法读取消息头。");
            }

            if (data[0] != ProtocolVersion)
            {
                throw new InvalidDataException($"协议版本不匹配：收到 {data[0]}，期望 {ProtocolVersion}。");
            }

            var type = (MessageType)data[1];
            if (type < MessageType.Command || type > MessageType.Notice)
            {
                throw new InvalidDataException($"未知消息类型：{data[1]}。");
            }

            return type;
        }

        // ============================ 内部工具 ============================

        private const byte MaxCommandKind = (byte)CommandKind.PickInitialTickets;
        private const byte MaxPlayerTrainColor = (byte)PlayerTrainColor.Purple;
        private const byte MaxTrainCarColor = (byte)TrainCarColor.Locomotive;
        private const byte MaxTurnActionPhase = (byte)TurnActionPhase.ActionCompleted;
        private const byte MaxMatchPhase = (byte)MatchPhase.GameOver;

        private static BinaryReader BeginRead(byte[] data, MessageType expected)
        {
            var actual = ReadMessageType(data);
            if (actual != expected)
            {
                throw new InvalidDataException($"消息类型不匹配：收到 {actual}，期望 {expected}。");
            }

            var reader = new BinaryReader(new MemoryStream(data, writable: false));
            reader.ReadByte(); // 协议版本
            reader.ReadByte(); // 消息类型
            return reader;
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            writer.Write(value ?? string.Empty);
        }

        private static void WriteStrings(BinaryWriter writer, string[] values)
        {
            writer.Write(values?.Length ?? 0);
            if (values == null)
            {
                return;
            }

            foreach (var value in values)
            {
                WriteString(writer, value);
            }
        }

        private static string[] ReadStrings(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var values = new string[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = reader.ReadString();
            }

            return values;
        }

        private static void WriteColors(BinaryWriter writer, TrainCarColor[] colors)
        {
            writer.Write(colors?.Length ?? 0);
            if (colors == null)
            {
                return;
            }

            foreach (var color in colors)
            {
                writer.Write((byte)color);
            }
        }

        private static TrainCarColor[] ReadColors(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var colors = new TrainCarColor[count];
            for (int i = 0; i < count; i++)
            {
                colors[i] = (TrainCarColor)ReadEnumByte(reader, MaxTrainCarColor, "TrainCarColor");
            }

            return colors;
        }

        private static byte ReadEnumByte(BinaryReader reader, byte maxInclusive, string field)
        {
            byte value = reader.ReadByte();
            if (value > maxInclusive)
            {
                throw new InvalidDataException($"{field} 取值非法：{value}。");
            }

            return value;
        }
    }
}