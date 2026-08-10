/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Services.AuthTicket;

public class AuthTicketRequest
{
    [JsonPropertyName("game_biz")]
    public string GameBiz { get; set; } = "hk4e_cn";

    [JsonPropertyName("mid")]
    public string Mid { get; set; } = string.Empty;

    [JsonPropertyName("stoken")]
    public string SToken { get; set; } = string.Empty;

    [JsonPropertyName("uid")]
    public int Uid { get; set; }
}

public class AuthTicketRequestOversea
{
    [JsonPropertyName("biz_name")]
    public string BizName { get; set; } = "hk4e_global";

    [JsonPropertyName("mid")]
    public string Mid { get; set; } = string.Empty;

    [JsonPropertyName("stoken")]
    public string SToken { get; set; } = string.Empty;
}

public class MihoyoApiResponse<T>
{
    [JsonPropertyName("retcode")]
    public int RetCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

public class AuthTicketData
{
    [JsonPropertyName("ticket")]
    public string Ticket { get; set; } = string.Empty;
}

public class AuthTicketResult
{
    public bool Success { get; set; }
    public string Ticket { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
