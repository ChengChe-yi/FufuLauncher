/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;
using System.Text.Json;
using FufuLauncher.Helpers;
using FufuLauncher.Models.CycleChallenge.Dto;
using FufuLauncher.Services.CycleChallenge;

namespace FufuLauncher.Models.CycleChallenge.Mapping;

/// 映射规则（已用真实 Starward 缓存库校准）：
/// <list type="bullet">
///     <item>KPI 的徽记数只认 <c>stat.medal_num</c>，<c>get_medal_round_list</c> 求和与徽记数不一致，仅作逐幕兜底；</item>
///     <item><c>heraldry</c> 与 <c>difficulty_id</c> 恒等，纹章图标按 heraldry 取 <c>UI_RoleCombat_Medal_S_{n}</c>；</item>
///     <item>逐幕列表按接口顺序渲染，不按 round_id 去重（塔罗轮会复用幕号）；</item>
///     <item>
///         塔罗图标：<c>is_tarot=true</c> 且 <c>is_get_medal=true</c> → Tarot_04，否则 Tarot_05；
///         <c>tarot_serial_no</c> 只用于标题文案（I / II），不作为图标映射依据；
///     </item>
///     <item><c>finish_time</c> 是 unix 秒字符串（完成时刻），逐幕「用时」按相邻完成时刻差值推算；</item>
///     <item>任意可选字段缺失 / 为 null 都返回占位文案，不抛异常。</item>
/// </list>
/// </para>
/// </summary>
public static class RoleCombatMapper
{
    /// <summary>
    ///     相邻两幕完成时刻差值超过该阈值时视为「中途休息 / 跨天」，不再展示为用时。
    ///     接口只给完成时刻（unix 秒），没有真正意义上的单幕战斗用时，只能做保守推算。
    /// </summary>
    private static readonly TimeSpan MaxRoundDuration = TimeSpan.FromMinutes(30);

    /// <summary>解析单期 payload 并映射为展示模型（解析失败抛 <see cref="JsonException" />，由控件兜底）。</summary>
    /// <param name="payloadJson">服务层给出的单期 payload（完整响应 / data 节点 / 单期对象均可）。</param>
    /// <param name="region">角色区服（cn_* 展示 UTC+8，其它展示 UTC）。</param>
    /// <returns>展示模型；无数据时返回对应空态。</returns>
    public static RoleCombatDisplay Parse(string? payloadJson, string? region = null)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return RoleCombatDisplay.CreateState(TheaterDisplayState.NotParticipated);
        }

        var envelope = CycleChallengePayloadReader.Read(payloadJson);
        if (envelope.Entry is not { } entryElement)
        {
            return RoleCombatDisplay.CreateState(TheaterDisplayState.NotParticipated);
        }

        var entry = entryElement.Deserialize<RoleCombatEntryDto>(CycleChallengePayloadReader.SerializerOptions);
        return Map(entry, envelope.IsUnlock, region);
    }

    /// <summary>DTO → 展示模型。</summary>
    /// <param name="entry">单期剧诗数据。</param>
    /// <param name="isUnlock">外层 is_unlock；null 表示接口未给出（按已解锁处理）。</param>
    /// <param name="region">角色区服（决定时间展示时区）。</param>
    /// <returns>展示模型。</returns>
    public static RoleCombatDisplay Map(RoleCombatEntryDto? entry, bool? isUnlock, string? region = null)
    {
        var context = CycleChallengeTime.CreateContext(region);

        if (entry is null)
        {
            return RoleCombatDisplay.CreateState(isUnlock == false
                ? TheaterDisplayState.NotUnlocked
                : TheaterDisplayState.NotParticipated);
        }

        var hasData = entry.HasData ?? false;
        var hasDetailData = entry.HasDetailData ?? entry.Detail is not null;

        if (isUnlock == false || (!hasData && !hasDetailData))
        {
            return RoleCombatDisplay.CreateState(isUnlock == false
                ? TheaterDisplayState.NotUnlocked
                : TheaterDisplayState.NotParticipated);
        }

        if (!hasDetailData)
        {
            return BuildSummaryOnly(entry, context);
        }

        var detail = entry.Detail;
        var stat = entry.Stat ?? detail?.DetailStat;
        var schedule = entry.Schedule;

        var rounds = MapRounds(detail, stat, context);
        var backupAvatars = MapAvatars(detail?.BackupAvatars);
        var battleStats = MapBattleStats(detail?.FightStatistics);
        var heraldry = stat?.Heraldry ?? entry.Heraldry ?? 0;

        return new RoleCombatDisplay
        {
            State = TheaterDisplayState.Content,
            PeriodText = FormatPeriod(schedule, entry, context),
            DifficultyName = TheaterDifficulty.GetDisplayName(stat?.DifficultyId ?? entry.DifficultyId ?? 0),
            HeraldryIconPath = CycleChallengeIcons.TheaterHeraldry(heraldry),
            MaxRoundText = FormatMaxRound(stat, entry),
            MedalIcons = MapMedalIcons(rounds),
            CoinText = FormatNumber(stat?.CoinNum ?? 0),
            AvatarBonusText = FormatNumber(stat?.AvatarBonusNum ?? 0),
            RentText = FormatNumber(stat?.RentCount ?? 0),
            Rounds = rounds,
            BackupAvatars = backupAvatars,
            BattleStats = battleStats,
        };
    }

    /// <summary>
    ///     只有汇总、没有逐幕明细（has_detail_data=false）时仍然展示 KPI，避免整页空着。
    ///     此时状态仍为 Content，只是 Rounds 为空。
    /// </summary>
    private static RoleCombatDisplay BuildSummaryOnly(RoleCombatEntryDto entry, CycleChallengeTimeContext context)
    {
        var stat = entry.Stat ?? entry.Detail?.DetailStat;
        var schedule = entry.Schedule;
        var heraldry = stat?.Heraldry ?? entry.Heraldry ?? 0;

        return new RoleCombatDisplay
        {
            State = TheaterDisplayState.Content,
            PeriodText = FormatPeriod(schedule, entry, context),
            DifficultyName = TheaterDifficulty.GetDisplayName(stat?.DifficultyId ?? entry.DifficultyId ?? 0),
            HeraldryIconPath = CycleChallengeIcons.TheaterHeraldry(heraldry),
            MaxRoundText = FormatMaxRound(stat, entry),
            CoinText = FormatNumber(stat?.CoinNum ?? 0),
            AvatarBonusText = FormatNumber(stat?.AvatarBonusNum ?? 0),
            RentText = FormatNumber(stat?.RentCount ?? 0),
        };
    }

    private static List<TheaterRound> MapRounds(
        RoleCombatDetailDto? detail, RoleCombatStatDto? stat, CycleChallengeTimeContext context)
    {
        var roundsData = detail?.RoundsData;
        if (roundsData is null || roundsData.Count == 0)
        {
            return [];
        }

        var medalRoundList = stat?.GetMedalRoundList;
        var result = new List<TheaterRound>(roundsData.Count);

        // 逐幕「用时」：以上一幕（或本期开始）的完成时刻为起点做差值；首幕不显示（跨度过大不可信）
        long? lastFinishUnix = null;

        for (var index = 0; index < roundsData.Count; index++)
        {
            var round = roundsData[index];
            if (round is null)
            {
                continue;
            }

            var isGetMedal = round.IsGetMedal
                             ?? (medalRoundList is not null && index < medalRoundList.Count &&
                                 medalRoundList[index] != 0);
            var isTarot = round.IsTarot ?? false;
            var roundId = round.RoundId ?? 0;

            var finishUnix = ParseUnixSeconds(round.FinishTime);
            var durationText = string.Empty;
            if (lastFinishUnix is { } start && finishUnix is { } end && end >= start)
            {
                var delta = end - start;
                if (delta > 0 && delta <= (long)MaxRoundDuration.TotalSeconds)
                {
                    durationText = string.Format("Theater_UsedTime".GetLocalized(), FormatDuration(delta));
                }
            }

            if (finishUnix is not null)
            {
                lastFinishUnix = finishUnix;
            }

            var tarotIcon = isTarot
                ? CycleChallengeIcons.Tarot(isGetMedal)
                : string.Empty;

            result.Add(new TheaterRound(
                roundId,
                isGetMedal,
                isTarot,
                FormatRoundTitle(round, isTarot),
                CycleChallengeIcons.TheaterStar(isGetMedal),
                FormatFinishTime(round, context),
                durationText,
                tarotIcon,
                MapAvatars(round.Avatars),
                MapEnemies(round.Enemies),
                MapBuffs(round.ChoiceCards),
                round.SplendourBuff?.Summary?.Description ?? string.Empty,
                MapSplendourBuffs(round.SplendourBuff)));
        }

        return result;
    }

    /// <summary>
    ///     最佳记录文案（参考 Hutao）：有圣牌完成数时「第 X 幕 圣牌 Y」，否则仅「第 X 幕」。
    /// </summary>
    private static string FormatMaxRound(RoleCombatStatDto? stat, RoleCombatEntryDto entry)
    {
        var maxRound = stat?.MaxRoundId ?? entry.MaxRoundId;
        var tarotFinished = stat?.TarotFinishedCount ?? 0;
        return tarotFinished > 0
            ? string.Format("Theater_MaxRoundWithTarot".GetLocalized(), maxRound, tarotFinished)
            : string.Format("Theater_RoundTitle".GetLocalized(), maxRound);
    }

    /// <summary>
    ///     「挑战星章」概览图标（参考 Hutao）：普通幕用星章（获得 Star1 / 未获得 Star2），塔罗轮用塔罗牌图标。
    /// </summary>
    private static List<TheaterMedalIcon> MapMedalIcons(List<TheaterRound> rounds)
    {
        if (rounds.Count == 0)
        {
            return [];
        }

        var result = new List<TheaterMedalIcon>(rounds.Count);
        foreach (var round in rounds)
        {
            var icon = round.IsTarot
                ? CycleChallengeIcons.Tarot(round.IsGetMedal)
                : CycleChallengeIcons.TheaterStar(round.IsGetMedal);
            result.Add(new TheaterMedalIcon(icon));
        }

        return result;
    }

    private static List<TheaterAvatar> MapAvatars(List<RoleCombatAvatarDto>? avatars)
    {
        if (avatars is null || avatars.Count == 0)
        {
            return [];
        }

        var result = new List<TheaterAvatar>(avatars.Count);
        foreach (var avatar in avatars)
        {
            if (avatar is null)
            {
                continue;
            }

            result.Add(new TheaterAvatar(
                avatar.AvatarId ?? 0,
                avatar.Image,
                avatar.Level ?? 0,
                avatar.Rarity ?? 0,
                avatar.Name,
                avatar.AvatarType ?? 1));
        }

        return result;
    }

    private static List<TheaterEnemy> MapEnemies(List<RoleCombatEnemyDto>? enemies)
    {
        if (enemies is null || enemies.Count == 0)
        {
            return [];
        }

        var result = new List<TheaterEnemy>(enemies.Count);
        foreach (var enemy in enemies)
        {
            if (enemy is null)
            {
                continue;
            }

            // 敌人名为空的条目（如尚未遭遇）没有展示价值，直接跳过
            if (string.IsNullOrWhiteSpace(enemy.Name) && string.IsNullOrWhiteSpace(enemy.Icon))
            {
                continue;
            }

            result.Add(new TheaterEnemy(enemy.Name, enemy.Icon, enemy.Level ?? 0));
        }

        return result;
    }

    /// <summary>映射神秘收获 / 普通祝福条目。</summary>
    private static List<TheaterBuff> MapBuffs(List<RoleCombatBuffDto>? buffs)
    {
        if (buffs is null || buffs.Count == 0)
        {
            return [];
        }

        var result = new List<TheaterBuff>(buffs.Count);
        foreach (var buff in buffs)
        {
            if (buff is null)
            {
                continue;
            }

            var name = NormalizeRichText(buff.Name);
            var description = NormalizeRichText(buff.Description);
            if (name.Length == 0 && description.Length == 0 && string.IsNullOrWhiteSpace(buff.Icon))
            {
                continue;
            }

            result.Add(new TheaterBuff(buff.Icon, name, description, buff.IsEnhanced ?? false));
        }

        return result;
    }

    /// <summary>映射辉彩祝福（含各等级效果文本）。</summary>
    private static List<TheaterSplendourBuff> MapSplendourBuffs(RoleCombatSplendourBuffDto? splendour)
    {
        if (splendour?.Buffs is null || splendour.Buffs.Count == 0)
        {
            return [];
        }

        var result = new List<TheaterSplendourBuff>(splendour.Buffs.Count);
        foreach (var item in splendour.Buffs)
        {
            if (item is null)
            {
                continue;
            }

            var effects = new List<string>();
            if (item.LevelEffects is { Count: > 0 })
            {
                foreach (var effect in item.LevelEffects)
                {
                    if (effect is null)
                    {
                        continue;
                    }

                    var text = NormalizeRichText(effect.Description);
                    if (text.Length > 0)
                    {
                        effects.Add(text);
                    }
                }
            }

            var name = NormalizeRichText(item.Name);
            if (name.Length == 0 && string.IsNullOrWhiteSpace(item.Icon))
            {
                continue;
            }

            result.Add(new TheaterSplendourBuff(item.Icon, name, item.Level ?? 0, effects));
        }

        return result;
    }

    /// <summary>接口的 desc 可能返回字面量 <c>\n</c>（反斜杠 + n），统一转换为真实换行。</summary>
    private static string NormalizeRichText(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\\n", "\n", StringComparison.Ordinal);

    private static TheaterBattleStats? MapBattleStats(RoleCombatFightStatisticsDto? fight)
    {
        if (fight is null)
        {
            return null;
        }

        var totalUseTime = fight.TotalUseTime ?? 0;
        var isShow = fight.IsShowBattleStats ?? false;

        // 接口在没有战斗统计时会给出 total_use_time=0 且四个统计字段为 null，此时整块隐藏
        if (!isShow && totalUseTime <= 0)
        {
            return null;
        }

        var shortest = new List<TheaterAvatarDamage>();
        if (fight.ShortestAvatarList is { Count: > 0 })
        {
            foreach (var item in fight.ShortestAvatarList)
            {
                if (item is not null && (item.AvatarId ?? 0) > 0)
                {
                    shortest.Add(new TheaterAvatarDamage(item.AvatarId ?? 0, item.AvatarIcon, item.Rarity ?? 0,
                        ParseValue(item.Value)));
                }
            }
        }

        return new TheaterBattleStats(
            totalUseTime > 0 ? FormatDuration(totalUseTime) : string.Empty,
            MapAvatarDamage(fight.MaxDefeatAvatar),
            MapAvatarDamage(fight.MaxDamageAvatar),
            MapAvatarDamage(fight.MaxTakeDamageAvatar),
            shortest);
    }

    private static TheaterAvatarDamage? MapAvatarDamage(RoleCombatAvatarStatisticsDto? avatar)
    {
        if (avatar is null || (avatar.AvatarId ?? 0) <= 0)
        {
            return null;
        }

        return new TheaterAvatarDamage(avatar.AvatarId ?? 0, avatar.AvatarIcon, avatar.Rarity ?? 0,
            ParseValue(avatar.Value));
    }

    /// <summary>把接口的数值字符串解析为整数；空串 / 非数字按 0 处理。</summary>
    private static long ParseValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    /// <summary>解析 unix 秒字符串（不合法返回 null）。</summary>
    private static long? ParseUnixSeconds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
               parsed > 0
            ? parsed
            : null;
    }

    /// <summary>标题：普通幕「第 N 幕」，塔罗轮「圣牌挑战 {罗马数字}」。</summary>
    private static string FormatRoundTitle(RoleCombatRoundDto round, bool isTarot)
    {
        if (!isTarot)
        {
            return string.Format("Theater_RoundTitle".GetLocalized(), round.RoundId ?? 0);
        }

        var serial = round.TarotSerialNumber ?? 0;
        return serial > 0
            ? string.Format("Theater_TarotTitle".GetLocalized(), ToRoman(serial))
            : "Theater_TarotTitleShort".GetLocalized();
    }

    /// <summary>完成时间：优先 finish_date_time 对象（服务器时区），否则回退 unix 秒转本地时间。</summary>
    private static string FormatFinishTime(RoleCombatRoundDto round, CycleChallengeTimeContext context)
    {
        if (round.FinishDateTime is { } dateTime
            && (dateTime.Year ?? 0) > 0
            && (dateTime.Month ?? 0) > 0
            && (dateTime.Day ?? 0) > 0)
        {
            var wallClock = new DateTime(
                dateTime.Year ?? 1,
                dateTime.Month ?? 1,
                dateTime.Day ?? 1,
                Math.Clamp(dateTime.Hour ?? 0, 0, 23),
                Math.Clamp(dateTime.Minute ?? 0, 0, 59),
                Math.Clamp(dateTime.Second ?? 0, 0, 59),
                DateTimeKind.Unspecified);
            return string.Format(
                "Theater_FinishAt".GetLocalized(),
                CycleChallengeTime.FormatWallClock(wallClock, "MM-dd HH:mm", context));
        }

        var unix = ParseUnixSeconds(round.FinishTime);
        if (unix is null)
        {
            return string.Empty;
        }

        return string.Format(
            "Theater_FinishAt".GetLocalized(),
            CycleChallengeTime.FormatUnix(unix.Value, "MM-dd HH:mm", context));
    }

    /// <summary>期次时间范围：优先 unix 秒，其次 schedule 的日期时间对象。</summary>
    private static string FormatPeriod(
        RoleCombatScheduleDto? schedule, RoleCombatEntryDto entry, CycleChallengeTimeContext context)
    {
        var start = FormatUnixTime(schedule?.StartTime ?? entry.StartTime, context);
        var end = FormatUnixTime(schedule?.EndTime ?? entry.EndTime, context);
        if (start.Length == 0)
        {
            start = FormatDateTime(schedule?.StartDateTime, context);
        }

        if (end.Length == 0)
        {
            end = FormatDateTime(schedule?.EndDateTime, context);
        }

        if (start.Length > 0 && end.Length > 0)
        {
            return $"{start} - {end}";
        }

        return start.Length > 0 ? start : end;
    }

    private static string FormatDateTime(RoleCombatDateTimeDto? dateTime, CycleChallengeTimeContext context)
    {
        if (dateTime is null || (dateTime.Year ?? 0) <= 0 || (dateTime.Month ?? 0) <= 0 || (dateTime.Day ?? 0) <= 0)
        {
            return string.Empty;
        }

        var wallClock = new DateTime(
            dateTime.Year ?? 1,
            dateTime.Month ?? 1,
            dateTime.Day ?? 1,
            0,
            0,
            0,
            DateTimeKind.Unspecified);
        return CycleChallengeTime.FormatWallClock(wallClock, "yyyy.MM.dd", context);
    }

    private static string FormatUnixTime(string? raw, CycleChallengeTimeContext context)
    {
        var unix = ParseUnixSeconds(raw);
        if (unix is null)
        {
            return string.Empty;
        }

        return CycleChallengeTime.FormatUnix(unix.Value, "yyyy.MM.dd", context);
    }

    /// <summary>把秒数格式化为 mm:ss / hh:mm:ss。</summary>
    internal static string FormatDuration(long seconds)
    {
        if (seconds <= 0)
        {
            return string.Empty;
        }

        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture,
                $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}")
            : string.Create(CultureInfo.InvariantCulture, $"{span.Minutes:D2}:{span.Seconds:D2}");
    }

    private static string FormatNumber(int? value) =>
        value is null ? "--" : (value.Value > 0 ? value.Value.ToString(CultureInfo.InvariantCulture) : "0");

    /// <summary>1 → I、2 → II …（超出 1..20 时回退为阿拉伯数字）。</summary>
    private static string ToRoman(int value)
    {
        string[] romans =
        [
            "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
            "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX",
        ];

        return value >= 1 && value <= romans.Length
            ? romans[value - 1]
            : value.ToString(CultureInfo.InvariantCulture);
    }
}