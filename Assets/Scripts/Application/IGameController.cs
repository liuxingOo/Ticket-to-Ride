using System;
using TicketToRide.Application.Net.Protocol;

namespace TicketToRide.Application
{
    /// <summary>
    /// 游戏动作入口与回合推进的抽象。
    /// 单机由 <see cref="LocalHotseatController"/> 直接执行；联机时由客户端把命令发给主机。
    /// 表现层只通过本接口提交动作、订阅 <see cref="Changed"/> 刷新，不再自己驱动回合。
    /// </summary>
    public interface IGameController
    {
        /// <summary>状态发生变化（动作完成、换人、结束）时触发，供表现层刷新。</summary>
        event Action Changed;

        /// <summary>提交一个动作命令。单机=立即执行；联机=发给主机。</summary>
        void Submit(GameCommand command);

        /// <summary>每帧驱动（推进 1 秒延迟、拉取网络消息）。由 GameLoopDriver 调用。</summary>
        void Tick(float deltaTime);
    }
}