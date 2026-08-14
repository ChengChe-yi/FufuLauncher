/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace MihoyoBBS;

public class CalendarRewardItem
{
    [JsonPropertyName("icon")]
    public string Icon { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("cnt")]
    public int Count { get; set; }

    public string CountText => $"x{Count}";
}
