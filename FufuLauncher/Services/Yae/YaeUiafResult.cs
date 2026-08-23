/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.

UIAF v1.1 结果文件契约，提权子进程读取游戏数据后写出该 JSON，主进程读取并应用。
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Services.Yae;

public sealed class YaeUiafResult
{
    [JsonPropertyName("info")]
    public YaeUiafInfo Info { get; set; } = new();

    [JsonPropertyName("list")]
    public List<YaeUiafItem> List { get; set; } = [];
}
