using System.Collections.Generic;

namespace TicketToRide.Application.Net
{
    /// <summary>
    /// 本地回环的一整套：1 台主机 + N 个客户端，全在同一进程里跑。
    /// 用于在不接真实网络的前提下验证「命令 → 权威端 → 快照」这条链路。
    /// </summary>
    public sealed class LoopbackSession
    {
        public LoopbackSession(HostSession host, IReadOnlyList<ClientSession> clients)
        {
            Host = host;
            Clients = clients;
        }

        public HostSession Host { get; }

        public IReadOnlyList<ClientSession> Clients { get; }

        /// <summary>把一个 Tick 周期推给主机和所有客户端（主机先跑，消息一轮就能跑完全程）。</summary>
        public void TickAll(float deltaTime)
        {
            Host.Tick(deltaTime);

            for (int i = 0; i < Clients.Count; i++)
            {
                Clients[i].Tick(deltaTime);
            }
        }
    }

    public static class SessionFactory
    {
        /// <summary>
        /// 起一局本地回环：主机自己当 player_1，其余玩家各配一个客户端。
        /// 开局（选色、发牌、选任务卡）由调用方决定，这里只搭好连接。
        /// </summary>
        public static LoopbackSession CreateLoopback(int playerCount)
        {
            var hub = new LocalTransportHub();
            var hostTransport = new LocalTransport(hub);
            var session = GameSession.CreateSession(playerCount);
            var host = new HostSession(hostTransport, session);

            var players = session.GameState.Players;
            host.AssignClient(hostTransport.LocalClientId, players[0].Id);

            var clients = new List<ClientSession>();
            for (int i = 1; i < players.Count; i++)
            {
                var transport = new LocalTransport(hub);
                host.AssignClient(transport.LocalClientId, players[i].Id);
                clients.Add(new ClientSession(transport));
            }

            return new LoopbackSession(host, clients);
        }
    }
}