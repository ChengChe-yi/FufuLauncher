/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.

Yae 命名管道协议命令类型。
协议定义参见 HolographicHat/YaeAchievement (GPL-3.0) 与 Snap Hutao (MIT)。
*/
namespace FufuLauncher.Services.Yae;

/// <summary>
/// YaeAchievementLib 注入游戏后，通过命名管道与宿主通信的命令类型。
/// </summary>
public enum YaeCommandKind : byte
{
    None = 0,

    /// <summary>客户端推送到管道的成就数据（0x01）。</summary>
    ResponseAchievement = 1,

    /// <summary>客户端推送到管道的玩家背包数据（0x02）。</summary>
    ResponsePlayerStore = 2,

    /// <summary>客户端推送到管道的玩家属性数据（0x03，类型 + double 值）。</summary>
    ResponsePlayerProp = 3,

    /// <summary>客户端请求命令 ID（0xFC），服务端写入成就/背包命令 ID。</summary>
    RequestCmdId = 252,

    /// <summary>客户端请求方法 RVA 表（0xFD），服务端写入 10 个 RVA。</summary>
    RequestRva = 253,

    /// <summary>客户端请求恢复游戏主线程（0xFE）。</summary>
    RequestResumeThread = 254,

    /// <summary>会话结束（0xFF），服务端写入 true 后关闭游戏。</summary>
    SessionEnd = 255,
}
