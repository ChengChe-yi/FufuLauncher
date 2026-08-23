/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.

UIAF v1.1 结果文件的单条成就项，与成就窗口的导入/导出格式一致。
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Services.Yae;

public sealed class YaeUiafItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("current")]
    public int Current { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}
