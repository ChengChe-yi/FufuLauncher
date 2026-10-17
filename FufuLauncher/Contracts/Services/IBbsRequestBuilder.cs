/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using FufuLauncher.Models.MiHoYo.Identity;

namespace FufuLauncher.Contracts.Services;

public enum BbsRequestScene
{
    DailyNote,


    DailyNoteWidget,


    UserFullInfo,


    CommunitySign,


    Geetest,


    WebLogin,


    GetFpNative
}

public sealed class BbsRequestOptions
{
    public string? AppId
    {
        get;
        init;
    }


    public string? ClientType
    {
        get;
        init;
    }


    public string? SdkVersion
    {
        get;
        init;
    }


    public string? Cookie
    {
        get;
        init;
    }


    public string? Referer
    {
        get;
        init;
    }


    public string? LifecycleId
    {
        get;
        init;
    }


    public string? ChallengeGame
    {
        get;
        init;
    }


    public string? ChallengePath
    {
        get;
        init;
    }


    public bool IncludeDs
    {
        get;
        init;
    }


    public bool Minimal
    {
        get;
        init;
    }
}

public interface IBbsRequestBuilder
{
    HttpRequestMessage Build(
        AccountContext ctx,
        BbsRequestScene scene,
        HttpMethod method,
        string url,
        string? body = null,
        string? challenge = null,
        BbsRequestOptions? options = null);
}