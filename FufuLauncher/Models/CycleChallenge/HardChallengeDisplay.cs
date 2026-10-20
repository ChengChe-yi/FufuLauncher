/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;
using FufuLauncher.Helpers;

namespace FufuLauncher.Models.CycleChallenge;

/// <summary>危战面板的展示状态。</summary>
public enum StygianDisplayState
{
    /// <summary>有可展示的数据。</summary>
    Content = 0,

    /// <summary>本期未解锁（外层 is_unlock=false）。</summary>
    NotUnlocked,

    /// <summary>未参与本期（单人 / 联机都没有数据）。</summary>
    NotParticipated,

    /// <summary>payload 解析失败。</summary>
    Error,
}

/// <summary>危战难度显示名（与 Hutao 的 HardChallengeDifficultyLevel 枚举一致：1 普通 / 2 进阶 / 3 困难 / 4 险恶 / 5 无畏 / 6 绝境）。</summary>
public static class StygianDifficulty
{
    /// <summary>把难度 ID 转成显示名。</summary>
    /// <param name="difficulty">接口 best.difficulty。</param>
    /// <returns>中文显示名；未知值返回「未知」。</returns>
    public static string GetDisplayName(int difficulty) => difficulty switch
    {
        1 => "Stygian_Difficulty_1".GetLocalized(),
        2 => "Stygian_Difficulty_2".GetLocalized(),
        3 => "Stygian_Difficulty_3".GetLocalized(),
        4 => "Stygian_Difficulty_4".GetLocalized(),
        5 => "Stygian_Difficulty_5".GetLocalized(),
        6 => "Stygian_Difficulty_6".GetLocalized(),
        _ => "Stygian_Difficulty_Unknown".GetLocalized(),
    };
}

/// <summary>危战出战角色。</summary>
/// <param name="AvatarId">角色 ID。</param>
/// <param name="IconUrl">头像绝对 URL。</param>
/// <param name="Level">角色等级。</param>
/// <param name="Rarity">稀有度（4 / 5）。</param>
/// <param name="Name">角色名。</param>
/// <param name="Rank">命座数（接口字段名为 rank）。</param>
public sealed record StygianAvatar(int AvatarId, string? IconUrl, int Level, int Rarity, string? Name, int Rank)
{
    /// <summary>角色名（缺失时用占位符）。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "?" : Name!;

    /// <summary>是否有命座信息。</summary>
    public bool HasRank => Rank > 0;

    /// <summary>命座文本。</summary>
    public string RankText => Rank > 0 ? Rank.ToString(CultureInfo.InvariantCulture) : string.Empty;
}

/// <summary>最佳输出角色（最强一击 / 最高总伤害）。</summary>
/// <param name="IconUrl">侧头像绝对 URL。</param>
/// <param name="ValueText">数值文本（接口给字符串，已按需要加千分位）。</param>
/// <param name="TypeText">类别文案（最强一击 / 最高总伤害）。</param>
/// <param name="Type">接口 type：1 最强一击、2 最高总伤害。</param>
public sealed record StygianBestAvatar(int AvatarId, string? IconUrl, string ValueText, string TypeText, int Type)
{
    /// <summary>是否有类别文案。</summary>
    public bool HasTypeText => TypeText.Length > 0;

    /// <summary>是否有数值。</summary>
    public bool HasValue => ValueText.Length > 0;
}

/// <summary>危战怪物。</summary>
/// <param name="Name">怪物名。</param>
/// <param name="IconUrl">怪物图标绝对 URL。</param>
/// <param name="Level">怪物等级。</param>
/// <param name="Descriptions">怪物描述（原样保留 &lt;color=#RRGGBBAA&gt; 富文本标记，由控件解析）。</param>
public sealed record StygianMonster(string? Name, string? IconUrl, int Level, IReadOnlyList<string> Descriptions)
{
    /// <summary>是否有怪物信息。</summary>
    public bool IsPresent => !string.IsNullOrWhiteSpace(Name) || !string.IsNullOrWhiteSpace(IconUrl);

    /// <summary>怪物名（缺失时用占位符）。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "?" : Name!;

    /// <summary>登记文本（未知等级返回空串）。</summary>
    public string LevelText => Level > 0 ? $"Lv.{Level}" : string.Empty;

    /// <summary>是否有等级文本。</summary>
    public bool HasLevel => Level > 0;

    /// <summary>是否有描述。</summary>
    public bool HasDescriptions => Descriptions.Count > 0;

    /// <summary>创建一个空怪物对象。</summary>
    public static StygianMonster Empty
    {
        get;
    } = new(null, null, 0, []);
}

/// <summary>危战单个关卡。</summary>
/// <param name="Name">关卡名（通常与怪物名一致）。</param>
/// <param name="Second">关卡用时（秒）。</param>
/// <param name="TimeText">用时文案。</param>
/// <param name="Team">出战队伍。</param>
/// <param name="BestAvatars">最佳输出角色。</param>
/// <param name="Monster">怪物信息。</param>
public sealed record StygianChallenge(
    string Name,
    int Second,
    string TimeText,
    IReadOnlyList<StygianAvatar> Team,
    IReadOnlyList<StygianBestAvatar> BestAvatars,
    StygianMonster Monster)
{
    /// <summary>关卡名（缺失时用占位符）。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "?" : Name!;

    /// <summary>是否有队伍。</summary>
    public bool HasTeam => Team.Count > 0;

    /// <summary>是否有最佳输出。</summary>
    public bool HasBestAvatars => BestAvatars.Count > 0;
}

/// <summary>单人 / 联机一套挑战数据。</summary>
/// <param name="HasData">该模式是否有数据（联机 10/14 期为 false）。</param>
/// <param name="DifficultyName">最高难度名。</param>
/// <param name="DifficultyIconPath">难度徽章路径（API icon 优先，其次按 difficulty 回退）。</param>
/// <param name="BestTimeText">最佳用时文案。</param>
/// <param name="ExtremeValueText">极数文案（该模式所有最佳输出中的最大值）。</param>
/// <param name="Challenges">关卡列表。</param>
public sealed record StygianMode(
    bool HasData,
    string DifficultyName,
    string DifficultyIconPath,
    string BestTimeText,
    string ExtremeValueText,
    IReadOnlyList<StygianChallenge> Challenges)
{
    /// <summary>是否有难度信息。</summary>
    public bool HasDifficulty => DifficultyName.Length > 0 &&
                                 DifficultyName != "Stygian_Difficulty_Unknown".GetLocalized();

    /// <summary>是否有难度徽章。</summary>
    public bool HasDifficultyIcon => DifficultyIconPath.Length > 0;

    /// <summary>是否有最佳用时。</summary>
    public bool HasBestTime => BestTimeText.Length > 0;

    /// <summary>是否有极数。</summary>
    public bool HasExtremeValue => ExtremeValueText.Length > 0;

    /// <summary>是否有关卡。</summary>
    public bool HasChallenges => Challenges.Count > 0;

    /// <summary>无数据提示文案。</summary>
    public string EmptyText => HasData ? string.Empty : "Stygian_NoCoopData".GetLocalized();

    /// <summary>创建一个空模式对象。</summary>
    /// <param name="hasData">是否有数据。</param>
    /// <returns>空模式对象。</returns>
    public static StygianMode CreateEmpty(bool hasData = false) =>
        new(hasData, string.Empty, string.Empty, string.Empty, string.Empty, []);
}

/// <summary>上榜角色。</summary>
/// <param name="IconUrl">头像绝对 URL。</param>
/// <param name="Rarity">稀有度。</param>
/// <param name="Name">角色名。</param>
/// <param name="IsPlus">是否最终上榜（未上榜显示为半透明）。</param>
public sealed record StygianBling(int AvatarId, string? IconUrl, int Rarity, string? Name, bool IsPlus)
{
    /// <summary>未上榜时降低不透明度。</summary>
    public double IconOpacity => IsPlus ? 1d : 0.45d;

    /// <summary>角色名（缺失时用占位符）。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "?" : Name!;
}

/// <summary>
///     幽境危战展示层模型：单人 / 联机两套 KPI + 关卡，以及上榜角色。
///     由 <see cref="Mapping.HardChallengeMapper" /> 从 DTO 纯函数映射得到。
/// </summary>
public sealed record HardChallengeDisplay
{
    /// <summary>展示状态。</summary>
    public StygianDisplayState State
    {
        get;
        init;
    } = StygianDisplayState.NotParticipated;

    /// <summary>解析失败时的错误信息。</summary>
    public string ErrorMessage
    {
        get;
        init;
    } = string.Empty;

    /// <summary>期次名称（接口 schedule.name，可能为空）。</summary>
    public string ScheduleName
    {
        get;
        init;
    } = string.Empty;

    /// <summary>期次时间范围文案。</summary>
    public string PeriodText
    {
        get;
        init;
    } = string.Empty;

    /// <summary>单人挑战。</summary>
    public StygianMode Solo
    {
        get;
        init;
    } = StygianMode.CreateEmpty();

    /// <summary>联机挑战。</summary>
    public StygianMode Coop
    {
        get;
        init;
    } = StygianMode.CreateEmpty();

    /// <summary>上榜角色（真实数据可能为空）。</summary>
    public IReadOnlyList<StygianBling> Blings
    {
        get;
        init;
    } = [];

    /// <summary>是否处于可展示内容的状态。</summary>
    public bool ShowContent => State == StygianDisplayState.Content;

    /// <summary>是否显示空 / 未解锁 / 错误提示。</summary>
    public bool HasState => State != StygianDisplayState.Content;

    /// <summary>空 / 错误状态标题。</summary>
    public string StateTitle => State switch
    {
        StygianDisplayState.NotUnlocked => "CycleChallenge_NotUnlocked".GetLocalized(),
        StygianDisplayState.NotParticipated => "CycleChallenge_NotParticipated".GetLocalized(),
        StygianDisplayState.Error => "CycleChallenge_LoadFailed".GetLocalized(),
        _ => string.Empty,
    };

    /// <summary>空 / 错误状态补充说明（无则为空串）。</summary>
    public string StateDetail => State == StygianDisplayState.Error ? ErrorMessage : string.Empty;

    /// <summary>是否有状态补充说明。</summary>
    public bool HasStateDetail => StateDetail.Length > 0;

    /// <summary>是否有期次时间范围。</summary>
    public bool HasPeriod => PeriodText.Length > 0;

    /// <summary>是否有期次名称。</summary>
    public bool HasScheduleName => ScheduleName.Length > 0;

    /// <summary>是否有联机数据（用于禁用联机切换）。</summary>
    public bool HasCoopData => Coop.HasData;

    /// <summary>是否有上榜角色。</summary>
    public bool HasBlings => Blings.Count > 0;

    /// <summary>创建指定状态的占位展示对象。</summary>
    /// <param name="state">目标状态。</param>
    /// <param name="detail">错误说明（仅 Error 状态使用）。</param>
    /// <returns>占位展示对象。</returns>
    public static HardChallengeDisplay CreateState(StygianDisplayState state, string? detail = null) => new()
    {
        State = state,
        ErrorMessage = detail ?? string.Empty,
    };

    /// <summary>创建解析失败的展示对象。</summary>
    /// <param name="message">错误说明。</param>
    /// <returns>错误状态展示对象。</returns>
    public static HardChallengeDisplay CreateError(string message) =>
        CreateState(StygianDisplayState.Error, message);
}