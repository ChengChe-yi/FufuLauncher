/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace MihoyoBBS;

public class RewardItem
{
    [JsonPropertyName("name")]
    public string Name
    {
        get;
        set;
    }

    [JsonPropertyName("cnt")]
    public int Count
    {
        get;
        set;
    }
}
