using System;
using System.Collections.Generic;

namespace TicketToRide.Application.Net
{
    /// <summary>
    /// 同进程回环用的中转站：所有 LocalTransport 注册进来，靠队列转发消息。
    /// 发送只入队，真正投递发生在 Dispatch（由某端的 Tick 触发），
    /// 这样「同一帧内立刻收到」这种假象不会掩盖真实网络下的时序问题。
    /// </summary>
    public class LocalTransportHub
    {
        private readonly Dictionary<int, LocalTransport> _endpoints = new Dictionary<int, LocalTransport>();
        private readonly Queue<PendingMessage> _queue = new Queue<PendingMessage>();
        private int _nextId;

        public int Register(LocalTransport transport)
        {
            int id = _nextId++;
            _endpoints[id] = transport;
            return id;
        }

        public void Unregister(int clientId)
        {
            _endpoints.Remove(clientId);
        }

        public void Enqueue(int toClientId, int fromClientId, byte[] payload)
        {
            _queue.Enqueue(new PendingMessage(toClientId, fromClientId, payload));
        }

        /// <summary>把队列里的消息投递给各自的目标端。投递过程中新入队的消息会在本轮内一并处理。</summary>
        public void Dispatch()
        {
            while (_queue.Count > 0)
            {
                var message = _queue.Dequeue();
                if (_endpoints.TryGetValue(message.ToClientId, out var target))
                {
                    target.Deliver(message.FromClientId, message.Payload);
                }
            }
        }

        private readonly struct PendingMessage
        {
            public readonly int ToClientId;
            public readonly int FromClientId;
            public readonly byte[] Payload;

            public PendingMessage(int toClientId, int fromClientId, byte[] payload)
            {
                ToClientId = toClientId;
                FromClientId = fromClientId;
                Payload = payload;
            }
        }
    }

    /// <summary>本地回环传输：主机是 0 号端，客户端依次 1、2、…。</summary>
    public class LocalTransport : ITransport
    {
        private readonly LocalTransportHub _hub;

        public LocalTransport(LocalTransportHub hub)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            LocalClientId = hub.Register(this);
        }

        public int LocalClientId { get; }

        public bool IsHost => LocalClientId == 0;

        public event Action<int, byte[]> MessageReceived;

        public void SendToHost(byte[] payload)
        {
            if (IsHost)
            {
                throw new InvalidOperationException("主机本身就是权威端，不应该再往主机发消息。");
            }

            _hub.Enqueue(0, LocalClientId, payload);
        }

        public void SendToPeer(int clientId, byte[] payload)
        {
            if (!IsHost)
            {
                throw new InvalidOperationException("只有主机可以点对点发送。");
            }

            _hub.Enqueue(clientId, LocalClientId, payload);
        }

        public void Tick()
        {
            _hub.Dispatch();
        }

        public void Shutdown()
        {
            _hub.Unregister(LocalClientId);
            MessageReceived = null;
        }

        /// <summary>由 Hub 调用，把消息交给订阅者。fromClientId = -1 表示来自主机。</summary>
        internal void Deliver(int fromClientId, byte[] payload)
        {
            MessageReceived?.Invoke(fromClientId, payload);
        }
    }
}