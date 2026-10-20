/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;
using FufuLauncher.Services.CycleChallenge;

namespace FufuLauncher.Models.CycleChallenge;

/// <summary>深境螺旋展示层固定数值（避免控件里出现魔法数字）。</summary>
public static class SpiralAbyssConstants
{
    /// <summary>满星总数：12 层 × 3 星。</summary>
    public const int MaxTotalStar = 36;

    /// <summary>每层满星：3 间 × 3 星。</summary>
    public const int MaxStarPerFloor = 9;
}

/// <summary>角色榜单类别（6 组，与接口字段一一对应）。</summary>
public enum SpiralAbyssRankKind
{
    /// <summary>出战次数（reveal_rank）。</summary>
    Reveal = 0,

    /// <summary>最多击破（defeat_rank）。</summary>
    Defeat = 1,

    /// <summary>最强一击（damage_rank）。</summary>
    Damage = 2,

    /// <summary>最多承伤（take_damage_rank）。</summary>
    TakeDamage = 3,

    /// <summary>元素战技（normal_skill_rank）。</summary>
    NormalSkill = 4,

    /// <summary>元素爆发（energy_skill_rank）。</summary>
    EnergySkill = 5,
}

/// <summary>
/// 战斗中的角色。接口只给 id / 头像 URL / 等级 / 稀有度（没有角色名与命座），因此展示层也只保留这些字段。
/// </summary>
/// <param name="AvatarId">角色 ID（用于本地元数据映射或去重）。</param>
/// <param name="IconUrl">头像绝对 URL。</param>
/// <param name="Level">角色等级。</param>
/// <param name="Rarity">稀有度（4 / 5）。</param>
public sealed record SpiralAbyssAvatar(int AvatarId, string? IconUrl, int Level, int Rarity);

/// <summary>单场战斗（上半 / 下半）。</summary>
/// <param name="Index">1 = 上半，2 = 下半。</param>
/// <param name="SettleTime">真实通关时间（来自战斗级 settle_date_time，按接口给的墙上时间原样展示）。</param>
/// <param name="Timestamp">战斗时间戳（settle_date_time 缺失时的兜底）。</param>
/// <param name="Avatars">出战角色（通常 4 个）。</param>
/// <param name="TimeContext">时间显示上下文（国服 UTC+8 / 其它 UTC）。</param>
public sealed record SpiralAbyssBattle(
    int Index,
    DateTime? SettleTime,
    DateTimeOffset? Timestamp,
    IReadOnlyList<SpiralAbyssAvatar> Avatars,
    CycleChallengeTimeContext TimeContext)
{
    /// <summary>是否上半场。</summary>
    public bool IsUpperHalf => Index == 1;

    /// <summary>是否下半场。</summary>
    public bool IsLowerHalf => Index == 2;

    /// <summary>战斗时间文本；优先 settle_date_time（服务器墙上时间），其次时间戳，均按展示时区输出。</summary>
    public string TimeText => SettleTime is { } settleTime
        ? CycleChallengeTime.FormatWallClock(settleTime, "MM-dd HH:mm", TimeContext)
        : Timestamp is { } timestamp
            ? CycleChallengeTime.Format(timestamp, "MM-dd HH:mm", TimeContext)
            : "--";

    /// <summary>是否有出战角色。</summary>
    public bool HasAvatars => Avatars.Count > 0;
}

/// <summary>单个「间」。</summary>
/// <param name="Index">间号（1~3）。</param>
/// <param name="Star">本间星数。</param>
/// <param name="MaxStar">本间满星。</param>
/// <param name="Battles">本间战斗（已按 index 升序排列）。</param>
public sealed record SpiralAbyssLevel(int Index, int Star, int MaxStar, IReadOnlyList<SpiralAbyssBattle> Battles)
{
    /// <summary>星数文本，如 "3/3"。</summary>
    public string StarText => MaxStar > 0
        ? $"{Star.ToString(CultureInfo.InvariantCulture)}/{MaxStar.ToString(CultureInfo.InvariantCulture)}"
        : Star.ToString(CultureInfo.InvariantCulture);

    /// <summary>间号文本。</summary>
    public string IndexText => Index.ToString(CultureInfo.InvariantCulture);

    /// <summary>上半场战斗（index=1），可能为 null（数据缺失或未打）。</summary>
    public SpiralAbyssBattle? UpperBattle => FindBattle(1);

    /// <summary>下半场战斗（index=2），可能为 null。</summary>
    public SpiralAbyssBattle? LowerBattle => FindBattle(2);

    /// <summary>上半场出战角色（无上半场时为空列表，便于控件直接绑定，避免空引用）。</summary>
    public IReadOnlyList<SpiralAbyssAvatar> UpperAvatars => UpperBattle?.Avatars ?? Array.Empty<SpiralAbyssAvatar>();

    /// <summary>下半场出战角色（无下半场时为空列表）。</summary>
    public IReadOnlyList<SpiralAbyssAvatar> LowerAvatars => LowerBattle?.Avatars ?? Array.Empty<SpiralAbyssAvatar>();

    /// <summary>上半场战斗时间文本。</summary>
    public string UpperTimeText => UpperBattle?.TimeText ?? "--";

    /// <summary>下半场战斗时间文本。</summary>
    public string LowerTimeText => LowerBattle?.TimeText ?? "--";

    /// <summary>是否有上半场战斗。</summary>
    public bool HasUpperBattle => UpperBattle is not null;

    /// <summary>是否有下半场战斗。</summary>
    public bool HasLowerBattle => LowerBattle is not null;

    private SpiralAbyssBattle? FindBattle(int battleIndex)
    {
        foreach (var battle in Battles)
        {
            if (battle.Index == battleIndex)
            {
                return battle;
            }
        }

        return null;
    }
}

/// <summary>单层成绩。</summary>
/// <param name="Index">层号（真实数据域 5~12，按响应原样展示，不写死 12 层）。</param>
/// <param name="IsUnlocked">本层是否解锁（未解锁的层也会出现在 floors 里，需要单独提示）。</param>
/// <param name="Star">本层星数。</param>
/// <param name="MaxStar">本层满星（通常 9）。</param>
/// <param name="LeyLineDisorder">通用地脉异常文案（中文，原样保留）。</param>
/// <param name="LeyLineDisorderUpper">上半地脉异常文案（与通用形态互斥）。</param>
/// <param name="LeyLineDisorderLower">下半地脉异常文案（与通用形态互斥）。</param>
/// <param name="Levels">本层的「间」（已按 index 升序排列）。</param>
public sealed record SpiralAbyssFloor(
    int Index,
    bool IsUnlocked,
    int Star,
    int MaxStar,
    IReadOnlyList<string> LeyLineDisorder,
    IReadOnlyList<string> LeyLineDisorderUpper,
    IReadOnlyList<string> LeyLineDisorderLower,
    IReadOnlyList<SpiralAbyssLevel> Levels)
{
    /// <summary>层号文本，如 "12"。</summary>
    public string IndexText => Index.ToString(CultureInfo.InvariantCulture);

    /// <summary>星数文本，如 "9/9"。</summary>
    public string StarText => MaxStar > 0
        ? $"{Star.ToString(CultureInfo.InvariantCulture)}/{MaxStar.ToString(CultureInfo.InvariantCulture)}"
        : Star.ToString(CultureInfo.InvariantCulture);

    public string AbyssStar = CycleChallengeIcons.GetLocal(CycleChallengeIcons.AbyssStar());

    /// <summary>是否有通用地脉异常。</summary>
    public bool HasCommonLeyLine => LeyLineDisorder.Count > 0;

    /// <summary>是否有上半地脉异常。</summary>
    public bool HasUpperLeyLine => LeyLineDisorderUpper.Count > 0;

    /// <summary>是否有下半地脉异常。</summary>
    public bool HasLowerLeyLine => LeyLineDisorderLower.Count > 0;

    /// <summary>是否按上半/下半分开展示（校准：upper/lower 与 common 互斥，成对出现时优先）。</summary>
    public bool ShowSplitLeyLine => HasUpperLeyLine || HasLowerLeyLine;

    /// <summary>是否展示通用地脉异常（仅当上/下形态皆空时回退到 common）。</summary>
    public bool ShowCommonLeyLine => !ShowSplitLeyLine && HasCommonLeyLine;

    /// <summary>是否有任何地脉异常可展示（三形态全空则不显示该区块）。</summary>
    public bool HasLeyLineDisorder => ShowSplitLeyLine || ShowCommonLeyLine;

    /// <summary>通用地脉异常文本（多行）。</summary>
    public string LeyLineText => string.Join(Environment.NewLine, LeyLineDisorder);

    /// <summary>上半地脉异常文本（多行）。</summary>
    public string UpperLeyLineText => string.Join(Environment.NewLine, LeyLineDisorderUpper);

    /// <summary>下半地脉异常文本（多行）。</summary>
    public string LowerLeyLineText => string.Join(Environment.NewLine, LeyLineDisorderLower);

    /// <summary>是否有「间」数据。</summary>
    public bool HasLevels => Levels.Count > 0;

    /// <summary>是否未解锁。</summary>
    public bool IsLocked => !IsUnlocked;
}

/// <summary>角色榜单行（只有头像 + 数值，接口没有角色名）。</summary>
/// <param name="AvatarId">角色 ID。</param>
/// <param name="IconUrl">头像绝对 URL。</param>
/// <param name="Rarity">稀有度（4 / 5）。</param>
/// <param name="Value">榜单数值。</param>
public sealed record SpiralAbyssRankRow(int AvatarId, string? IconUrl, int Rarity, long Value)
{
    /// <summary>数值文本（千分位），如 "1,234,567"。</summary>
    public string ValueText => Value > 0 ? Value.ToString("N0", CultureInfo.InvariantCulture) : "--";
}

/// <summary>一组角色榜单。</summary>
/// <param name="Kind">榜单类别。</param>
/// <param name="Rows">榜单行（按接口返回顺序，通常 4 行）。</param>
public sealed record SpiralAbyssRankSection(SpiralAbyssRankKind Kind, IReadOnlyList<SpiralAbyssRankRow> Rows)
{
    /// <summary>是否有数据。</summary>
    public bool HasRows => Rows.Count > 0;
}

/// <summary>
/// 深境螺旋展示模型（不可变）。由 <see cref="Mapping.SpiralAbyssMapper"/> 从 DTO 纯函数生成，
/// 控件只负责把这里的字段画出来，不再做业务判断。
/// </summary>
public sealed record SpiralAbyssDisplay(
    string Uid,
    string ScheduleId,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    int TotalStar,
    int MaxTotalStar,
    int MaxFloor,
    string MaxFloorText,
    int TotalBattleTimes,
    int TotalWinTimes,
    long StrongestHitValue,
    bool IsUnlocked,
    bool IsJustSkippedFloor,
    string? SkippedFloor,
    IReadOnlyList<SpiralAbyssFloor> Floors,
    SpiralAbyssRankSection RevealRank,
    SpiralAbyssRankSection DefeatRank,
    SpiralAbyssRankSection DamageRank,
    SpiralAbyssRankSection TakeDamageRank,
    SpiralAbyssRankSection NormalSkillRank,
    SpiralAbyssRankSection EnergySkillRank,
    CycleChallengeTimeContext TimeContext)
{
    /// <summary>KPI「总星数」，形如 "36/36"。</summary>
    public string StarText => $"{TotalStar.ToString(CultureInfo.InvariantCulture)}/{MaxTotalStar.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>KPI「最深」文本。</summary>
    public string MaxFloorDisplay => string.IsNullOrWhiteSpace(MaxFloorText) ? "--" : MaxFloorText;

    /// <summary>KPI「战斗次数」文本。</summary>
    public string BattleTimesText => TotalBattleTimes.ToString(CultureInfo.InvariantCulture);

    /// <summary>KPI「获胜次数」文本。</summary>
    public string WinTimesText => TotalWinTimes.ToString(CultureInfo.InvariantCulture);

    /// <summary>KPI「最强一击」文本（千分位）。</summary>
    public string StrongestHitText => StrongestHitValue > 0
        ? StrongestHitValue.ToString("N0", CultureInfo.InvariantCulture)
        : "--";

    /// <summary>是否有最强一击数据。</summary>
    public bool HasStrongestHit => StrongestHitValue > 0;

    /// <summary>期次时间范围文本（仅用于信息展示，可为空）。</summary>
    public string PeriodText
    {
        get
        {
            if (StartTime is not { } start || EndTime is not { } end)
            {
                return string.Empty;
            }

            return $"{CycleChallengeTime.Format(start, "yyyy-MM-dd", TimeContext)} ~ {CycleChallengeTime.Format(end, "yyyy-MM-dd", TimeContext)}";
        }
    }

    /// <summary>是否有楼层数据（为空时展示「本期未参与」空态）。</summary>
    public bool HasFloors => Floors.Count > 0;

    /// <summary>是否展示跳层提示（is_just_skipped_floor 或 skipped_floor 任一有值）。</summary>
    public bool ShowSkippedFloorHint => IsJustSkippedFloor || !string.IsNullOrWhiteSpace(SkippedFloor);

    /// <summary>被跳过的层/间文本，如 "10-3"。</summary>
    public string SkippedFloorText => string.IsNullOrWhiteSpace(SkippedFloor) ? "--" : SkippedFloor;

    /// <summary>六组榜单是否有任意数据。</summary>
    public bool HasAnyRank => RevealRank.HasRows || DefeatRank.HasRows || DamageRank.HasRows
        || TakeDamageRank.HasRows || NormalSkillRank.HasRows || EnergySkillRank.HasRows;

    /// <summary>空展示模型（未参与 / 解析不到数据时使用，避免控件到处判空）。</summary>
    public static SpiralAbyssDisplay Empty(
        string? uid = null,
        string? scheduleId = null,
        CycleChallengeTimeContext? timeContext = null)
    {
        return new SpiralAbyssDisplay(
            uid ?? string.Empty,
            scheduleId ?? string.Empty,
            null,
            null,
            0,
            SpiralAbyssConstants.MaxTotalStar,
            0,
            string.Empty,
            0,
            0,
            0,
            true,
            false,
            null,
            [],
            new SpiralAbyssRankSection(SpiralAbyssRankKind.Reveal, []),
            new SpiralAbyssRankSection(SpiralAbyssRankKind.Defeat, []),
            new SpiralAbyssRankSection(SpiralAbyssRankKind.Damage, []),
            new SpiralAbyssRankSection(SpiralAbyssRankKind.TakeDamage, []),
            new SpiralAbyssRankSection(SpiralAbyssRankKind.NormalSkill, []),
            new SpiralAbyssRankSection(SpiralAbyssRankKind.EnergySkill, []),
            timeContext ?? CycleChallengeTimeContext.Default);
    }
}
