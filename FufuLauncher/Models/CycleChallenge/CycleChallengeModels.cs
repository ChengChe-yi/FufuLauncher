/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Models.CycleChallenge;

public enum ChallengeMode
{
    SpiralAbyss = 0,
    Theater = 1,
    Stygian = 2,
}

public static class ChallengeModeExtensions
{
    public static string ToDisplayKey(this ChallengeMode mode) => mode switch
    {
        ChallengeMode.SpiralAbyss => "CycleChallenge_TabAbyss",
        ChallengeMode.Theater => "CycleChallenge_TabTheater",
        ChallengeMode.Stygian => "CycleChallenge_TabStygian",
        _ => "CycleChallenge_Title",
    };

    public static string ToPeriodFolder(this ChallengeMode mode) => mode switch
    {
        ChallengeMode.SpiralAbyss => "abyss",
        ChallengeMode.Theater => "theater",
        ChallengeMode.Stygian => "stygian",
        _ => "unknown",
    };
}

public enum ChallengeErrorKind
{
    Success = 0,
    NotParticipated,
    NotUnlocked,
    LoginExpired,
    RiskControlRequired,
    RateLimited,
    NetworkError,
    ServerError,
    Unsupported,
}

public sealed record ChallengePeriod(
    string Uid,
    ChallengeMode Mode,
    string ScheduleId,
    string? StartUtc,
    string? EndUtc,
    string? Summary,
    DateTimeOffset FetchedAt);

public sealed class ChallengeLoadResult
{
    public bool Success
    {
        get;
        init;
    }

    public ChallengeErrorKind Kind
    {
        get;
        init;
    } = ChallengeErrorKind.ServerError;

    public string? Message
    {
        get;
        init;
    }

    public IReadOnlyList<ChallengePeriod> Periods
    {
        get;
        init;
    } = [];


    public string? Payload
    {
        get;
        init;
    }

    public string? ActiveScheduleId
    {
        get;
        init;
    }

    public bool IsFromCache
    {
        get;
        init;
    }

    public DateTimeOffset? FetchedAt
    {
        get;
        init;
    }
}