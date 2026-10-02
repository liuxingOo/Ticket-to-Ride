namespace TicketToRide.Presentation
{
    /// <summary>一局游戏的运行方式。</summary>
    public enum NetworkRole
    {
        /// <summary>同一台机器上轮流操作。</summary>
        Singleplayer,

        /// <summary>主机即服务器：本端持有权威 GameState。</summary>
        Host,

        /// <summary>客户端：命令发给主机，界面只读主机发来的快照。</summary>
        Client,
    }

    /// <summary>
    /// 菜单选好的运行方式，跨场景传给 GameScene（静态持有，切场景不丢）。
    /// 由 MenuManager 写入，GameSessionView 读取后决定用哪种 IGameController。
    /// </summary>
    public static class NetworkBootstrap
    {
        public const string DefaultIp = "127.0.0.1";
        public const int DefaultPort = 7777;

        public static NetworkRole Role { get; private set; } = NetworkRole.Singleplayer;

        /// <summary>客户端要连接的主机地址（主机模式下无意义）。</summary>
        public static string Ip { get; private set; } = DefaultIp;

        /// <summary>主机监听端口 / 客户端连接端口。</summary>
        public static int Port { get; private set; } = DefaultPort;

        /// <summary>玩家自定义昵称；空表示沿用默认名字（Alice 等）。</summary>
        public static string Nickname { get; private set; } = string.Empty;

        /// <summary>记下菜单里填的昵称，空串会被规整掉以便各方统一按「空 = 用默认名」处理。</summary>
        public static void SetNickname(string nickname)
        {
            Nickname = string.IsNullOrWhiteSpace(nickname) ? string.Empty : nickname.Trim();
        }

        public static void ConfigureSingleplayer()
        {
            Role = NetworkRole.Singleplayer;
        }

        public static void ConfigureHost(int port)
        {
            Role = NetworkRole.Host;
            Port = port;
        }

        public static void ConfigureClient(string ip, int port)
        {
            Role = NetworkRole.Client;
            Ip = string.IsNullOrWhiteSpace(ip) ? DefaultIp : ip.Trim();
            Port = port;
        }
    }
}