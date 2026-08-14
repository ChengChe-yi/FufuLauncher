/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
public class GameAccountData
{
    public Guid Id
    {
        get; set;
    }
    public string Name { get; set; } = string.Empty;
    public string SdkData { get; set; } = string.Empty;
    public DateTime LastUsed
    {
        get; set;
    }
    public string? Remark
    {
        get; set;
    }
}
public class RedeemCodeItem
{
    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("codes")]
    public List<string> Codes { get; set; } = new List<string>();

    [System.Text.Json.Serialization.JsonPropertyName("valid")]
    public string Valid { get; set; } = string.Empty;
}

public class HoyoCodeResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("codes")]
    public List<HoyoCodeItem>? Codes { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("game")]
    public string Game { get; set; } = string.Empty;
}

public class HoyoCodeItem
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public int Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("game")]
    public string Game { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("rewards")]
    public string Rewards { get; set; } = string.Empty;
}
public class GameConfigData
{
    public string GamePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ServerType { get; set; } = string.Empty;
    public string DirectorySize { get; set; } = "0 MB";
}
