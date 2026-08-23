/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.

YaeAchievementPipe 命名管道服务端，运行在提权子进程中。
协议与 Snap Hutao 的 YaeNamedPipeServer (MIT) 一致：
- 0xFC：写入成就/背包命令 ID（2 个 uint32）
- 0xFD：写入 10 个方法 RVA
- 0xFE：恢复游戏主线程
- 0x01/0x02：读取 int32 长度 + 数据
- 0x03：读取 12 字节属性对
- 0xFF：写入 true，结束游戏
*/
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace FufuLauncher.Services.Yae;

internal sealed class YaeNamedPipeServer
{
    public const string PipeName = "YaeAchievementPipe";

    private readonly NamedPipeServerStream _serverStream;
    private readonly YaeNativeConfiguration _config;
    private YaeGameProcess? _game;

    /// <summary>
    /// 构造函数即创建管道实例，确保注入的游戏 DLL 连接时管道已存在。
    /// </summary>
    public YaeNamedPipeServer(YaeNativeConfiguration config)
    {
        _config = config;
        _serverStream = new NamedPipeServerStream(PipeName);
    }

    /// <summary>注入完成后绑定游戏进程，供 0xFE 恢复线程与 0xFF 结束游戏使用。</summary>
    public void AttachGame(YaeGameProcess game) => _game = game;

    /// <summary>
    /// 等待游戏内注入的 Yae 客户端连接并收集全部数据，直到会话结束。
    /// </summary>
    public List<YaeData> Collect()
    {
        _serverStream.WaitForConnection();

        var list = new List<YaeData>();
        using var reader = new BinaryReader(_serverStream, Encoding.UTF8, true);
        using var writer = new BinaryWriter(_serverStream, Encoding.UTF8, true);

        while (_game is not null && _game.IsRunning && _serverStream.IsConnected)
        {
            int rawKind;
            try
            {
                rawKind = reader.ReadByte();
            }
            catch (IOException)
            {
                break; // 管道损坏
            }

            if (rawKind < 0)
            {
                break; // 客户端断开
            }

            switch ((YaeCommandKind)rawKind)
            {
                case YaeCommandKind.RequestCmdId:
                    writer.Write(_config.AchievementCmdId);
                    writer.Write(_config.StoreCmdId);
                    break;

                case YaeCommandKind.RequestRva:
                    writer.Write(_config.DoCmd);
                    writer.Write(_config.UpdateNormalProperty);
                    writer.Write(_config.NewString);
                    writer.Write(_config.FindGameObject);
                    writer.Write(_config.EventSystemUpdate);
                    writer.Write(_config.SimulatePointerClick);
                    writer.Write(_config.ToInt32);
                    writer.Write(_config.TcpStatePtr);
                    writer.Write(_config.SharedInfoPtr);
                    writer.Write(_config.Decompress);
                    break;

                case YaeCommandKind.RequestResumeThread:
                    _game.ResumeMainThread();
                    break;

                case YaeCommandKind.ResponseAchievement:
                case YaeCommandKind.ResponsePlayerStore:
                    {
                        int contentLength = reader.ReadInt32();
                        var payload = new byte[contentLength];
                        reader.BaseStream.ReadExactly(payload);
                        list.Add(new YaeData((YaeCommandKind)rawKind, payload));
                        break;
                    }

                case YaeCommandKind.ResponsePlayerProp:
                    {
                        var payload = new byte[Marshal.SizeOf<YaePropertyTypeValue>()];
                        reader.BaseStream.ReadExactly(payload);
                        list.Add(new YaeData(YaeCommandKind.ResponsePlayerProp, payload));
                        break;
                    }

                case YaeCommandKind.SessionEnd:
                    {
                        writer.Write(true);
                        writer.Flush();
                        _game.Kill();
                        list.Add(YaeData.SessionEnd);
                        return list;
                    }
            }
        }

        return list;
    }
}
