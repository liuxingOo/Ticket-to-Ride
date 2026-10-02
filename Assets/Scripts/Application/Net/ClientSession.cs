using System;
using UnityEngine;
using TicketToRide.Application.Net.Protocol;

namespace TicketToRide.Application.Net
{
    /// <summary>
    /// 客户端会话：只做两件事——把命令发给主机、接收并按版本号接受快照。
    /// 它不碰领域层，本地没有任何权威状态；界面只能读 Latest 里的数据。
    /// </summary>
    public class ClientSession : IGameController
    {
        private readonly ITransport _transport;
        private readonly SnapshotMatchView _view;

        public ClientSession(ITransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.MessageReceived += OnMessageReceived;
            _view = new SnapshotMatchView(() => Latest);
        }

        /// <summary>表现层读取面：只暴露主机快照里的公开段 + 本人私有段。</summary>
        public IMatchView View => _view;

        /// <summary>主机下发的最近一份快照（已按版本号去旧）。</summary>
        public MatchSnapshot Latest { get; private set; }

        public event Action Changed;

        /// <summary>主机发来的一次性提示（命令被拒绝等），只用于界面弹一句提示。</summary>
        public event Action<string> NoticeReceived;

        /// <summary>向主机报到，带上本机昵称（空表示沿用默认名）。</summary>
        public void SendHello(string nickname)
        {
            _transport.SendToHost(MessageCodec.EncodeHello(nickname));
        }

        public void Submit(GameCommand command)
        {
            if (command == null)
            {
                return;
            }

            _transport.SendToHost(MessageCodec.EncodeCommand(command));
        }

        public void Tick(float deltaTime)
        {
            _transport.Tick();
        }

        private void OnMessageReceived(int fromClientId, byte[] payload)
        {
            MessageType type;
            try
            {
                type = MessageCodec.ReadMessageType(payload);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ClientSession] 忽略非法消息：{exception.Message}");
                return;
            }

            if (type == MessageType.Notice)
            {
                try
                {
                    NoticeReceived?.Invoke(MessageCodec.DecodeNotice(payload));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[ClientSession] 提示解析失败：{exception.Message}");
                }

                return;
            }

            if (type != MessageType.Snapshot)
            {
                return;
            }

            MatchSnapshot snapshot;
            try
            {
                snapshot = MessageCodec.DecodeSnapshot(payload);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ClientSession] 快照解析失败：{exception.Message}");
                return;
            }

            // 迟到 / 乱序的旧包直接丢掉。
            if (Latest != null && snapshot.Version < Latest.Version)
            {
                return;
            }

            Latest = snapshot;
            Changed?.Invoke();
        }
    }
}