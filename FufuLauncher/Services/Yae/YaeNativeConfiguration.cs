/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Services.Yae;

/// <summary>
/// 针对具体游戏版本解析出的原生配置：命令 ID + 方法 RVA 表。
/// 由 <see cref="YaeMetadataService"/> 根据游戏哈希从元数据中解析。
/// </summary>
public sealed class YaeNativeConfiguration
{
    public uint StoreCmdId { get; init; }
    public uint AchievementCmdId { get; init; }

    public uint DoCmd { get; init; }
    public uint UpdateNormalProperty { get; init; }
    public uint NewString { get; init; }
    public uint FindGameObject { get; init; }
    public uint EventSystemUpdate { get; init; }
    public uint SimulatePointerClick { get; init; }
    public uint ToInt32 { get; init; }
    public uint TcpStatePtr { get; init; }
    public uint SharedInfoPtr { get; init; }
    public uint Decompress { get; init; }
}
