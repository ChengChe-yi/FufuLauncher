/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace MihoyoBBS;

public class SignResponseData
{
    [JsonPropertyName("success")]
    public int Success
    {
        get;
        set;
    }

    [JsonPropertyName("gt")]
    public string Gt
    {
        get;
        set;
    }

    [JsonPropertyName("challenge")]
    public string Challenge
    {
        get;
        set;
    }
}
