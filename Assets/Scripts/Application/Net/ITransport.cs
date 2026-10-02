using System;

namespace TicketToRide.Application.Net
{
    /// <summary>
    /// 传输抽象。本地回环（同进程）与真实网络（TCP）各实现一份；
    /// 传输只负责搬字节，内容由 MessageCodec 解释。
    /// </summary>
    public interface ITransport
    {
        /// <summary>本端连接 Id，主机固定为 0。</summary>
        int LocalClientId { get; }

        bool IsHost { get; }

        /// <summary>收到一条消息。fromClientId = -1 表示来自主机。</summary>
        event Action<int, byte[]> MessageReceived;

        void SendToHost(byte[] payload);

        void SendToPeer(int clientId, byte[] payload);

        /// <summary>推进消息分发。本地实现也走队列，避免「即时分发」掩盖时序问题。</summary>
        void Tick();

        void Shutdown();
    }
}