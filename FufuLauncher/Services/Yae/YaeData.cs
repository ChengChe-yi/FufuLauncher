/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Services.Yae;

/// <summary>
/// 从命名管道读到的单条 Yae 数据。
/// </summary>
public sealed class YaeData
{
    public YaeData(YaeCommandKind kind, byte[] payload)
    {
        Kind = kind;
        Payload = payload;
    }

    /// <summary>会话结束标记，无负载。</summary>
    public static YaeData SessionEnd { get; } = new(YaeCommandKind.SessionEnd, []);

    public YaeCommandKind Kind { get; }

    /// <summary>原始字节负载（成就/背包数据）。</summary>
    public byte[] Payload { get; }
}
