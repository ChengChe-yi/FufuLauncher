/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;
using FufuLauncher.Helpers;

namespace MihoyoBBS;

public class DisplayConfig
{
    [JsonPropertyName("Nickname")]
    public string Nickname
    {
        get;
        set;
    } = "";

    [JsonPropertyName("GameUid")]
    public string GameUid
    {
        get;
        set;
    } = "";

    [JsonPropertyName("Server")]
    public string Server
    {
        get;
        set;
    } = "";

    [JsonPropertyName("AvatarUrl")]
    public string AvatarUrl
    {
        get;
        set;
    } = "ms-appx:///Assets/DefaultAvatar.png";

    [JsonPropertyName("Level")]
    public string Level
    {
        get;
        set;
    } = "";

    [JsonPropertyName("Sign")]
    public string Sign
    {
        get;
        set;
    } = "Status_None".GetLocalized();

    [JsonPropertyName("IpRegion")]
    public string IpRegion
    {
        get;
        set;
    } = "Status_Unknown".GetLocalized();

    [JsonPropertyName("Gender")]
    public int Gender
    {
        get;
        set;
    } = 0;

    [JsonPropertyName("HasBoundRole")]
    public bool HasBoundRole
    {
        get;
        set;
    } = true;
}
