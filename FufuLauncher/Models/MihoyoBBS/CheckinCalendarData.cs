/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace MihoyoBBS;

public class CheckinCalendarData
{
    [JsonPropertyName("month")]
    public int Month { get; set; }

    [JsonPropertyName("awards")]
    public List<CalendarRewardItem> Awards { get; set; }
}
