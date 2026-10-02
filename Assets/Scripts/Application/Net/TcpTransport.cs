using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace TicketToRide.Application.Net
{
    /// <summary>
    /// 真实网络传输（局域网直连，TCP）。
    /// 主机监听端口，给每条进来的连接分配一个 clientId（从 1 开始，主机自己固定 0）；客户端只连主机。
    /// 收包在后台线程做，但事件一律在 Tick()（主线程）里抛，避免在非主线程碰 Unity API。
    /// 帧格式：[4 字节小端长度][MessageCodec 编出来的整包]。
    /// </summary>
    public class TcpTransport : ITransport
    {
        private const int HeaderSize = 4;
        private const int MaxFrameSize = 1024 * 1024;
        private const int ConnectTimeoutMs = 3000;

        private readonly bool _isHost;
        private readonly ConcurrentQueue<IncomingMessage> _messages = new ConcurrentQueue<IncomingMessage>();
        private readonly ConcurrentQueue<int> _connected = new ConcurrentQueue<int>();
        private readonly Dictionary<int, Connection> _connections = new Dictionary<int, Connection>();
        private readonly object _connectionsLock = new object();

        private TcpListener _listener;
        private Connection _hostConnection;
        private volatile bool _running;
        private int _nextClientId = 1;

        /// <summary>收到一条消息。fromClientId = -1 表示来自主机。</summary>
        public event Action<int, byte[]> MessageReceived;

        /// <summary>主机专用：有新客户端接入（在 Tick 里抛出，主线程安全）。</summary>
        public event Action<int> ClientConnected;

        /// <summary>主机：监听指定端口，LocalClientId 固定为 0。</summary>
        public TcpTransport(int listenPort)
        {
            _isHost = true;
            LocalClientId = 0;

            try
            {
                _listener = new TcpListener(IPAddress.Any, listenPort);
                _listener.Start();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"无法监听端口 {listenPort}（{exception.Message}）。", exception);
            }

            _running = true;
            new Thread(AcceptLoop) { IsBackground = true, Name = "TcpAccept" }.Start();

            Debug.Log($"[TcpTransport] 主机已开始监听端口 {listenPort}。");
        }

        /// <summary>客户端：连接主机。连不上会抛 InvalidOperationException，由调用方提示玩家。</summary>
        public TcpTransport(string hostIp, int port)
        {
            _isHost = false;

            // 主机侧的连接 Id 由主机分配，本端会话用不到，给个占位值。
            LocalClientId = -1;
            _running = true;

            var client = new TcpClient();
            try
            {
                var connectResult = client.BeginConnect(hostIp, port, null, null);
                if (!connectResult.AsyncWaitHandle.WaitOne(ConnectTimeoutMs))
                {
                    throw new TimeoutException($"连接超时（{ConnectTimeoutMs} 毫秒）。");
                }

                client.EndConnect(connectResult);
            }
            catch (Exception exception)
            {
                client.Close();
                throw new InvalidOperationException($"无法连接主机 {hostIp}:{port}（{exception.Message}）。", exception);
            }

            client.NoDelay = true;
            _hostConnection = new Connection(-1, client, EnqueueMessage);
            _hostConnection.StartReceive();

            Debug.Log($"[TcpTransport] 已连接到主机 {hostIp}:{port}。");
        }

        public int LocalClientId { get; }

        public bool IsHost => _isHost;

        public void SendToHost(byte[] payload)
        {
            if (_isHost)
            {
                throw new InvalidOperationException("主机本身就是权威端，不应该再往主机发消息。");
            }

            if (_hostConnection == null || !_hostConnection.Send(payload))
            {
                Debug.LogWarning("[TcpTransport] 发送给主机失败，连接可能已断开。");
            }
        }

        public void SendToPeer(int clientId, byte[] payload)
        {
            if (!_isHost)
            {
                throw new InvalidOperationException("只有主机可以点对点发送。");
            }

            Connection connection;
            lock (_connectionsLock)
            {
                _connections.TryGetValue(clientId, out connection);
            }

            if (connection == null)
            {
                Debug.LogWarning($"[TcpTransport] 客户端 {clientId} 不在线，消息已丢弃。");
                return;
            }

            if (!connection.Send(payload))
            {
                Debug.LogWarning($"[TcpTransport] 发送给客户端 {clientId} 失败，连接可能已断开。");
            }
        }

        public void Tick()
        {
            while (_connected.TryDequeue(out var clientId))
            {
                ClientConnected?.Invoke(clientId);
            }

            while (_messages.TryDequeue(out var message))
            {
                MessageReceived?.Invoke(message.FromClientId, message.Payload);
            }
        }

        public void Shutdown()
        {
            _running = false;

            try
            {
                _listener?.Stop();
            }
            catch (Exception)
            {
                // 关闭期的异常无需处理。
            }

            _listener = null;

            List<Connection> remaining;
            lock (_connectionsLock)
            {
                remaining = new List<Connection>(_connections.Values);
                _connections.Clear();
            }

            foreach (var connection in remaining)
            {
                connection.Close();
            }

            _hostConnection?.Close();
            _hostConnection = null;

            MessageReceived = null;
            ClientConnected = null;
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    return; // 监听已停止。
                }

                client.NoDelay = true;

                int clientId = Interlocked.Increment(ref _nextClientId) - 1;
                var connection = new Connection(clientId, client, EnqueueMessage);

                lock (_connectionsLock)
                {
                    _connections[clientId] = connection;
                }

                connection.StartReceive();
                _connected.Enqueue(clientId);
            }
        }

        private void EnqueueMessage(int clientId, byte[] payload)
        {
            _messages.Enqueue(new IncomingMessage(clientId, payload));
        }

        private readonly struct IncomingMessage
        {
            public readonly int FromClientId;
            public readonly byte[] Payload;

            public IncomingMessage(int fromClientId, byte[] payload)
            {
                FromClientId = fromClientId;
                Payload = payload;
            }
        }

        /// <summary>一条 TCP 连接：独立线程收包，发送加锁保证一帧只写一个完整包。</summary>
        private sealed class Connection
        {
            private readonly TcpClient _client;
            private readonly NetworkStream _stream;
            private readonly Action<int, byte[]> _onMessage;
            private readonly object _sendLock = new object();

            public Connection(int clientId, TcpClient client, Action<int, byte[]> onMessage)
            {
                ClientId = clientId;
                _client = client;
                _stream = client.GetStream();
                _onMessage = onMessage;
            }

            public int ClientId { get; }

            public void StartReceive()
            {
                new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = $"TcpRecv{ClientId}",
                }.Start();
            }

            public bool Send(byte[] payload)
            {
                try
                {
                    lock (_sendLock)
                    {
                        var header = BitConverter.GetBytes(payload.Length);
                        _stream.Write(header, 0, HeaderSize);
                        _stream.Write(payload, 0, payload.Length);
                        _stream.Flush();
                    }

                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public void Close()
            {
                try
                {
                    _stream?.Close();
                }
                catch (Exception)
                {
                    // 忽略关闭期异常。
                }

                try
                {
                    _client?.Close();
                }
                catch (Exception)
                {
                    // 忽略关闭期异常。
                }
            }

            private void ReceiveLoop()
            {
                var header = new byte[HeaderSize];

                try
                {
                    while (true)
                    {
                        if (!TryReadFully(header, HeaderSize))
                        {
                            return;
                        }

                        int length = BitConverter.ToInt32(header, 0);
                        if (length <= 0 || length > MaxFrameSize)
                        {
                            return;
                        }

                        var payload = new byte[length];
                        if (!TryReadFully(payload, length))
                        {
                            return;
                        }

                        _onMessage(ClientId, payload);
                    }
                }
                catch (Exception)
                {
                    // 对端关闭或网络异常：结束该连接。
                }
            }

            private bool TryReadFully(byte[] buffer, int count)
            {
                int offset = 0;
                while (offset < count)
                {
                    int read = _stream.Read(buffer, offset, count - offset);
                    if (read <= 0)
                    {
                        return false;
                    }

                    offset += read;
                }

                return true;
            }
        }
    }
}