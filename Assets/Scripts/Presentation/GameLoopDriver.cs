using UnityEngine;
using TicketToRide.Application;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 每帧把 <see cref="IGameController.Tick"/> 推起来的驱动组件。
    /// 表现层不再自己跑协程推进回合，改由控制器在 Tick 里管理计时与网络消息。
    /// </summary>
    public class GameLoopDriver : MonoBehaviour
    {
        private IGameController _controller;

        public void Initialize(IGameController controller)
        {
            _controller = controller;
        }

        private void Update()
        {
            _controller?.Tick(Time.deltaTime);
        }
    }
}