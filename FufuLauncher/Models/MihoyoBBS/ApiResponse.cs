/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace MihoyoBBS;

public class ApiResponse<T>
{
    [JsonPropertyName("retcode")]
    public int RetCode
    {
        get;
        set;
    }

    [JsonPropertyName("message")]
    public string Message
    {
        get;
        set;
    }

    [JsonPropertyName("data")]
    public T Data
    {
        get;
        set;
    }
}
