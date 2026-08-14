/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace MihoyoBBS;

public class IsSignData
{
    [JsonPropertyName("total_sign_day")]
    public int TotalSignDay
    {
        get;
        set;
    }

    [JsonPropertyName("today")]
    public string Today
    {
        get;
        set;
    }

    [JsonPropertyName("is_sign")]
    public bool IsSign
    {
        get;
        set;
    }

    [JsonPropertyName("first_bind")]
    public bool FirstBind
    {
        get;
        set;
    }
}
