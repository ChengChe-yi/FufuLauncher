/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Models.CycleChallenge;

/// <summary>
/// 周期挑战模式。数值会写入数据库，不可随意调整。
/// </summary>
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

/// <summary>
/// 一次请求的结果分类。UI 只根据该分类做提示，不解析 retcode。
/// </summary>
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

/// <summary>
/// 一期挑战的摘要信息（不包含完整 Payload）。
/// </summary>
public sealed record ChallengePeriod(
    string Uid,
    ChallengeMode Mode,
    string ScheduleId,
    string? StartUtc,
    string? EndUtc,
    DateTimeOffset FetchedAt);

/// <summary>
/// 单次加载/刷新结果。
/// </summary>
public sealed class ChallengeLoadResult
{
    public bool Success { get; init; }

    public ChallengeErrorKind Kind { get; init; } = ChallengeErrorKind.ServerError;

    public string? Message { get; init; }

    public IReadOnlyList<ChallengePeriod> Periods { get; init; } = [];

    /// <summary>本期（或指定期）的 Payload JSON，供面板直接展示。</summary>
    public string? Payload { get; init; }

    public string? ActiveScheduleId { get; init; }

    public bool IsFromCache { get; init; }

    public DateTimeOffset? FetchedAt { get; init; }
}
