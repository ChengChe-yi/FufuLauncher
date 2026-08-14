/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace MihoyoBBS;

public class CheckinRewardsData
{
    [JsonPropertyName("awards")]
    public List<RewardItem> Awards
    {
        get;
        set;
    }
}
