/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;
using FufuLauncher.Helpers;

namespace FufuLauncher.Models.CycleChallenge;

/// <summary>剧诗面板的展示状态。面板只根据该状态决定显示内容区还是空/错态提示。</summary>
public enum TheaterDisplayState
{
    /// <summary>有可展示的明细。</summary>
    Content = 0,

    /// <summary>本期未解锁（外层 is_unlock=false）。</summary>
    NotUnlocked,

    /// <summary>未参与本期（has_data=false）。</summary>
    NotParticipated,

    /// <summary>已参与但接口没有返回逐幕明细（has_detail_data=false）。</summary>
    NoDetail,

    /// <summary>payload 解析失败。</summary>
    Error,
}

/// <summary>剧诗难度显示名（以 Hutao 的 RoleCombatDifficultyLevel 枚举为准：0 无 / 1 简单 / 2 普通 / 3 困难 / 4 映辉 / 5 月演）。</summary>
public static class TheaterDifficulty
{
    /// <summary>把难度 ID 转成显示名。</summary>
    /// <param name="difficultyId">接口 difficulty_id。</param>
    /// <returns>中文显示名；未知值返回「未知」。</returns>
    public static string GetDisplayName(int difficultyId) => difficultyId switch
    {
        1 => "Theater_Difficulty_1".GetLocalized(),
        2 => "Theater_Difficulty_2".GetLocalized(),
        3 => "Theater_Difficulty_3".GetLocalized(),
        4 => "Theater_Difficulty_4".GetLocalized(),
        5 => "Theater_Difficulty_5".GetLocalized(),
        _ => "Theater_Difficulty_Unknown".GetLocalized(),
    };
}

/// <summary>剧诗出战角色。</summary>
/// <param name="AvatarId">角色 ID。</param>
/// <param name="IconUrl">头像绝对 URL。</param>
/// <param name="Level">角色等级。</param>
/// <param name="Rarity">稀有度（4 / 5）。</param>
/// <param name="Name">角色名。</param>
/// <param name="AvatarType">来源：1 自有、2 试用、3 借用（助演）。</param>
public sealed record TheaterAvatar(int AvatarId, string? IconUrl, int Level, int Rarity, string? Name, int AvatarType)
{
    /// <summary>是否试用角色。</summary>
    public bool IsTrial => AvatarType == 2;

    /// <summary>是否借用（助演）角色。</summary>
    public bool IsSupport => AvatarType == 3;

    /// <summary>来源角标文案（自有角色返回空串）。</summary>
    public string TypeText => AvatarType switch
    {
        2 => "Theater_AvatarTrial".GetLocalized(),
        3 => "Theater_AvatarSupport".GetLocalized(),
        _ => string.Empty,
    };

    /// <summary>是否需要显示来源角标。</summary>
    public bool HasTypeText => TypeText.Length > 0;

    /// <summary>是否有头像。</summary>
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconUrl);

    /// <summary>角色名（缺失时用占位符）。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "?" : Name!;
}

/// <summary>剧诗敌人。</summary>
/// <param name="Name">敌人名。</param>
/// <param name="IconUrl">敌人图标绝对 URL。</param>
/// <param name="Level">敌人等级。</param>
public sealed record TheaterEnemy(string? Name, string? IconUrl, int Level)
{
    /// <summary>敌人名（缺失时用占位符）。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "?" : Name!;

    /// <summary>等级文本（未知等级返回空串，避免出现 Lv.0）。</summary>
    public string LevelText => Level > 0 ? $"Lv.{Level}" : string.Empty;

    /// <summary>是否有等级文本。</summary>
    public bool HasLevel => Level > 0;
}

/// <summary>剧诗单幕展示数据。</summary>
/// <param name="RoundId">幕号（塔罗轮会复用前一幕的幕号，仅用于文案）。</param>
/// <param name="IsGetMedal">本幕是否获得徽记。</param>
/// <param name="IsTarot">本幕是否塔罗（圣牌）挑战。</param>
/// <param name="Title">标题：普通幕「第 N 幕」，塔罗轮「圣牌 I/II」。</param>
/// <param name="MedalIconPath">本幕星章图标（获得 Star1 / 未获得 Star2，参考 Hutao）。</param>
/// <param name="FinishTimeText">完成时间文案（服务器时区）。</param>
/// <param name="DurationText">用时文案：与上一幕完成时间的差值（首幕相对本期开始时间）。</param>
/// <param name="TarotIconPath">塔罗图标路径（非塔罗轮为空）。</param>
/// <param name="Avatars">出战角色。</param>
/// <param name="Enemies">敌人。</param>
/// <param name="ChoiceCards">神秘收获（每幕抽取的事件卡）。</param>
/// <param name="SplendourSummary">辉彩祝福汇总描述（含富文本颜色标记）。</param>
/// <param name="SplendourBuffs">辉彩祝福列表（含等级与效果文本）。</param>
public sealed record TheaterRound(
    int RoundId,
    bool IsGetMedal,
    bool IsTarot,
    string Title,
    string MedalIconPath,
    string FinishTimeText,
    string DurationText,
    string TarotIconPath,
    IReadOnlyList<TheaterAvatar> Avatars,
    IReadOnlyList<TheaterEnemy> Enemies,
    IReadOnlyList<TheaterBuff> ChoiceCards,
    string SplendourSummary,
    IReadOnlyList<TheaterSplendourBuff> SplendourBuffs)
{
    /// <summary>是否获得徽记（用于显示强调色徽记标签）。</summary>
    public bool HasMedal => IsGetMedal;

    /// <summary>是否未获得徽记（用于显示弱化色徽记标签）。</summary>
    public bool HasNoMedal => !IsGetMedal;

    /// <summary>是否显示星章图标。</summary>
    public bool HasMedalIcon => !string.IsNullOrWhiteSpace(MedalIconPath);

    /// <summary>是否显示塔罗图标。</summary>
    public bool HasTarotIcon => !string.IsNullOrWhiteSpace(TarotIconPath);

    /// <summary>是否有出战角色。</summary>
    public bool HasAvatars => Avatars.Count > 0;

    /// <summary>是否有敌人。</summary>
    public bool HasEnemies => Enemies.Count > 0;

    /// <summary>是否有完成时间。</summary>
    public bool HasFinishTime => FinishTimeText.Length > 0;

    /// <summary>是否有用时。</summary>
    public bool HasDuration => DurationText.Length > 0;

    /// <summary>是否有神秘收获。</summary>
    public bool HasChoiceCards => ChoiceCards.Count > 0;

    /// <summary>是否有辉彩祝福（汇总或列表任一非空）。</summary>
    public bool HasSplendour => HasSplendourSummary || SplendourBuffs.Count > 0;

    /// <summary>是否有辉彩祝福汇总描述。</summary>
    public bool HasSplendourSummary => !string.IsNullOrWhiteSpace(SplendourSummary);
}

/// <summary>
/// 神秘收获 / 祝福条目（名称 + 效果描述）。
/// </summary>
/// <param name="IconUrl">图标绝对 URL。</param>
/// <param name="Name">名称（可能含富文本颜色标记）。</param>
/// <param name="Description">效果描述（保留富文本颜色标记与换行）。</param>
/// <param name="IsEnhanced">是否强化过。</param>
public sealed record TheaterBuff(string? IconUrl, string Name, string Description, bool IsEnhanced)
{
    /// <summary>是否有图标。</summary>
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconUrl);

    /// <summary>是否有名称。</summary>
    public bool HasName => !string.IsNullOrWhiteSpace(Name);

    /// <summary>是否有效果描述。</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
}

/// <summary>
/// 辉彩祝福条目（图标 + 名称 + 等级 + 各等级效果文本）。
/// 效果文本来自 <c>level_effect[].desc</c>，鼠标悬浮时展示。
/// </summary>
/// <param name="IconUrl">图标绝对 URL。</param>
/// <param name="Name">祝福名称。</param>
/// <param name="Level">当前等级。</param>
/// <param name="Effects">各等级效果描述（保留富文本）。</param>
public sealed record TheaterSplendourBuff(
    string? IconUrl,
    string Name,
    int Level,
    IReadOnlyList<string> Effects)
{
    /// <summary>是否有图标。</summary>
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconUrl);

    /// <summary>是否有名称。</summary>
    public bool HasName => !string.IsNullOrWhiteSpace(Name);

    /// <summary>是否有等级。</summary>
    public bool HasLevel => Level > 0;

    /// <summary>等级文案（如 Lv.2）。</summary>
    public string LevelText => Level > 0 ? $"Lv.{Level}" : string.Empty;

    /// <summary>是否有可悬浮查看的效果文本。</summary>
    public bool HasEffects => Effects.Count > 0;
}

/// <summary>战斗统计中的角色数值条目。</summary>
/// <param name="IconUrl">角色头像绝对 URL。</param>
/// <param name="Rarity">稀有度。</param>
/// <param name="Value">数值（接口给字符串，已解析为整数）。</param>
public sealed record TheaterAvatarDamage(string? IconUrl, int Rarity, long Value)
{
    /// <summary>数值文本（带千分位；无数据时显示 --）。</summary>
    public string ValueText => Value > 0
        ? Value.ToString("N0", CultureInfo.InvariantCulture)
        : "--";
}

/// <summary>剧诗战斗统计。</summary>
/// <param name="TotalTimeText">演出总时长。</param>
/// <param name="MaxDefeat">击败敌人最多的角色。</param>
/// <param name="MaxDamage">造成伤害最高的角色。</param>
/// <param name="MaxTakeDamage">承受伤害最高的角色。</param>
/// <param name="ShortestClear">最快通关队伍。</param>
public sealed record TheaterBattleStats(
    string TotalTimeText,
    TheaterAvatarDamage? MaxDefeat,
    TheaterAvatarDamage? MaxDamage,
    TheaterAvatarDamage? MaxTakeDamage,
    IReadOnlyList<TheaterAvatarDamage> ShortestClear)
{
    /// <summary>是否有总时长。</summary>
    public bool HasTotalTime => TotalTimeText.Length > 0;

    /// <summary>是否有击败最多角色。</summary>
    public bool HasMaxDefeat => MaxDefeat is not null;

    /// <summary>是否有最高伤害角色。</summary>
    public bool HasMaxDamage => MaxDamage is not null;

    /// <summary>是否有最高承伤角色。</summary>
    public bool HasMaxTakeDamage => MaxTakeDamage is not null;

    /// <summary>是否有最快通关队伍。</summary>
    public bool HasShortestClear => ShortestClear.Count > 0;
}

/// <summary>
/// 「挑战星章」概览中的单个图标：普通幕为塔罗/星章，塔罗轮为塔罗牌图标（参考 Hutao）。
/// </summary>
/// <param name="IconPath">本地图标路径（ms-appx）。</param>
public sealed record TheaterMedalIcon(string IconPath)
{
    /// <summary>是否有图标。</summary>
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconPath);
}

/// <summary>
/// 幻想真境剧诗展示层模型：KPI、逐幕、替补角色、战斗统计。
/// 由 <see cref="Mapping.RoleCombatMapper"/> 从 DTO 纯函数映射得到，控件只负责绑定。
/// </summary>
public sealed record RoleCombatDisplay
{
    /// <summary>展示状态。</summary>
    public TheaterDisplayState State { get; init; } = TheaterDisplayState.NotParticipated;

    /// <summary>解析失败时的错误信息。</summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>期次时间范围文案（如 2025.08.01 - 2025.09.01）。</summary>
    public string PeriodText { get; init; } = string.Empty;

    /// <summary>难度显示名。</summary>
    public string DifficultyName { get; init; } = string.Empty;

    /// <summary>纹章（= 难度）图标路径。</summary>
    public string HeraldryIconPath { get; init; } = string.Empty;

    /// <summary>最高幕文本（含圣牌数：「第 10 幕 圣牌 2」；无圣牌时仅「第 10 幕」）。</summary>
    public string MaxRoundText { get; init; } = "--";

    /// <summary>「挑战星章」概览图标（逐幕：普通幕星章 / 塔罗轮塔罗牌，参考 Hutao）。</summary>
    public IReadOnlyList<TheaterMedalIcon> MedalIcons { get; init; } = [];

    /// <summary>幻剧之花消耗文本。</summary>
    public string CoinText { get; init; } = "--";

    /// <summary>场外声援次数文本。</summary>
    public string AvatarBonusText { get; init; } = "--";

    /// <summary>助演次数文本。</summary>
    public string RentText { get; init; } = "--";

    /// <summary>逐幕数据（按接口返回顺序）。</summary>
    public IReadOnlyList<TheaterRound> Rounds { get; init; } = [];

    /// <summary>待命（替补）角色。</summary>
    public IReadOnlyList<TheaterAvatar> BackupAvatars { get; init; } = [];

    /// <summary>战斗统计；不展示时为 null。</summary>
    public TheaterBattleStats? BattleStats { get; init; }

    /// <summary>是否处于可展示内容的状态。</summary>
    public bool ShowContent => State == TheaterDisplayState.Content;

    /// <summary>是否显示空 / 未解锁 / 错误提示。</summary>
    public bool HasState => State != TheaterDisplayState.Content;

    /// <summary>空 / 错误状态标题。</summary>
    public string StateTitle => State switch
    {
        TheaterDisplayState.NotUnlocked => "CycleChallenge_NotUnlocked".GetLocalized(),
        TheaterDisplayState.NotParticipated => "CycleChallenge_NotParticipated".GetLocalized(),
        TheaterDisplayState.NoDetail => "Theater_NoDetail".GetLocalized(),
        TheaterDisplayState.Error => "CycleChallenge_LoadFailed".GetLocalized(),
        _ => string.Empty,
    };

    /// <summary>空 / 错误状态补充说明（无则为空串）。</summary>
    public string StateDetail => State == TheaterDisplayState.Error ? ErrorMessage : string.Empty;

    /// <summary>是否有状态补充说明。</summary>
    public bool HasStateDetail => StateDetail.Length > 0;

    /// <summary>是否有期次时间范围。</summary>
    public bool HasPeriod => PeriodText.Length > 0;

    /// <summary>是否有纹章图标。</summary>
    public bool HasHeraldryIcon => HeraldryIconPath.Length > 0;

    /// <summary>是否有逐幕数据。</summary>
    public bool HasRounds => Rounds.Count > 0;

    /// <summary>是否没有逐幕数据（用于显示「本期无逐幕明细」提示）。</summary>
    public bool HasNoRounds => Rounds.Count == 0;

    /// <summary>是否显示「挑战星章」图标概览。</summary>
    public bool HasMedalIcons => MedalIcons.Count > 0;

    /// <summary>是否有待命角色。</summary>
    public bool HasBackupAvatars => BackupAvatars.Count > 0;

    /// <summary>是否展示战斗统计区块。</summary>
    public bool ShowBattleStats => BattleStats is not null;

    /// <summary>战斗统计：演出总时长文本。</summary>
    public string BattleTotalTimeText => BattleStats?.TotalTimeText ?? string.Empty;

    /// <summary>战斗统计：是否有总时长。</summary>
    public bool HasBattleTotalTime => BattleTotalTimeText.Length > 0;

    /// <summary>战斗统计：最多击败角色。</summary>
    public TheaterAvatarDamage? MaxDefeat => BattleStats?.MaxDefeat;

    /// <summary>战斗统计：最多击败数值文本。</summary>
    public string MaxDefeatValueText => MaxDefeat?.ValueText ?? "--";

    /// <summary>战斗统计：最多击败角色头像。</summary>
    public string? MaxDefeatIconUrl => MaxDefeat?.IconUrl;

    /// <summary>战斗统计：最多击败角色稀有度。</summary>
    public int MaxDefeatRarity => MaxDefeat?.Rarity ?? 0;

    /// <summary>战斗统计：是否有最多击败角色。</summary>
    public bool HasMaxDefeat => MaxDefeat is not null;

    /// <summary>战斗统计：最高伤害角色。</summary>
    public TheaterAvatarDamage? MaxDamage => BattleStats?.MaxDamage;

    /// <summary>战斗统计：最高伤害数值文本。</summary>
    public string MaxDamageValueText => MaxDamage?.ValueText ?? "--";

    /// <summary>战斗统计：最高伤害角色头像。</summary>
    public string? MaxDamageIconUrl => MaxDamage?.IconUrl;

    /// <summary>战斗统计：最高伤害角色稀有度。</summary>
    public int MaxDamageRarity => MaxDamage?.Rarity ?? 0;

    /// <summary>战斗统计：是否有最高伤害角色。</summary>
    public bool HasMaxDamage => MaxDamage is not null;

    /// <summary>战斗统计：最高承伤角色。</summary>
    public TheaterAvatarDamage? MaxTakeDamage => BattleStats?.MaxTakeDamage;

    /// <summary>战斗统计：最高承伤数值文本。</summary>
    public string MaxTakeDamageValueText => MaxTakeDamage?.ValueText ?? "--";

    /// <summary>战斗统计：最高承伤角色头像。</summary>
    public string? MaxTakeDamageIconUrl => MaxTakeDamage?.IconUrl;

    /// <summary>战斗统计：最高承伤角色稀有度。</summary>
    public int MaxTakeDamageRarity => MaxTakeDamage?.Rarity ?? 0;

    /// <summary>战斗统计：是否有最高承伤角色。</summary>
    public bool HasMaxTakeDamage => MaxTakeDamage is not null;

    /// <summary>战斗统计：最快通关队伍。</summary>
    public IReadOnlyList<TheaterAvatarDamage> ShortestClear => BattleStats?.ShortestClear ?? [];

    /// <summary>战斗统计：是否有最快通关队伍。</summary>
    public bool HasShortestClear => ShortestClear.Count > 0;

    /// <summary>创建一个指定状态的占位展示对象。</summary>
    /// <param name="state">目标状态。</param>
    /// <param name="detail">错误说明（仅 Error 状态使用）。</param>
    /// <returns>占位展示对象。</returns>
    public static RoleCombatDisplay CreateState(TheaterDisplayState state, string? detail = null) => new()
    {
        State = state,
        ErrorMessage = detail ?? string.Empty,
    };

    /// <summary>创建一个解析失败的展示对象。</summary>
    /// <param name="message">错误说明。</param>
    /// <returns>错误状态展示对象。</returns>
    public static RoleCombatDisplay CreateError(string message) =>
        CreateState(TheaterDisplayState.Error, message);
}
