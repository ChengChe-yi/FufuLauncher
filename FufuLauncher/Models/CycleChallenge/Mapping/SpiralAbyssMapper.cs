/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;
using System.Text.Json;
using FufuLauncher.Models.CycleChallenge.Dto;

namespace FufuLauncher.Models.CycleChallenge.Mapping;

/// <summary>
/// 深境螺旋 DTO → 展示模型的纯函数映射。
/// 只做「排序 / 取数 / 文本格式化」，不做 IO、不弹 UI、不抛异常（脏数据退化为占位文本）。
/// </summary>
public static class SpiralAbyssMapper
{
    /// <summary>
    /// 映射入口。传入 null（未参与 / 接口无 data）时返回 <see cref="SpiralAbyssDisplay.Empty"/>。
    /// </summary>
    public static SpiralAbyssDisplay Map(SpiralAbyssDto? dto, CycleChallengeTimeContext? timeContext = null)
    {
        var context = timeContext ?? CycleChallengeTimeContext.Default;
        if (dto is null)
        {
            return SpiralAbyssDisplay.Empty(timeContext: context);
        }

        // 层按 index 升序；每层的间、每间的战斗同样按 index 升序（战斗 1=上半，2=下半）。
        var floors = new List<SpiralAbyssFloor>();
        foreach (var floorDto in dto.Floors ?? new List<SpiralAbyssFloorDto>())
        {
            if (floorDto is null)
            {
                continue;
            }

            floors.Add(MapFloor(floorDto, context));
        }

        floors.Sort(static (left, right) => left.Index.CompareTo(right.Index));

        // 总星口径（校准结论）：直接用接口的 total_star（全 12 层口径），不要用 floors 累加。
        var totalStar = Math.Max(dto.TotalStar, 0);
        var (maxFloorValue, maxFloorText) = ResolveMaxFloor(dto.MaxFloor, floors);

        // 最强一击 = damage_rank 的第一条（接口已按数值倒序返回）。
        var strongestHit = 0L;
        foreach (var rank in dto.DamageRank ?? new List<SpiralAbyssRankDto>())
        {
            if (rank is null)
            {
                continue;
            }

            strongestHit = Math.Max(rank.Value, 0L);
            break;
        }

        return new SpiralAbyssDisplay(
            dto.Uid?.Trim() ?? string.Empty,
            dto.ScheduleId?.Trim() ?? string.Empty,
            ParseTime(dto.StartTime),
            ParseTime(dto.EndTime),
            totalStar,
            SpiralAbyssConstants.MaxTotalStar,
            maxFloorValue,
            maxFloorText,
            Math.Max(dto.TotalBattleTimes, 0),
            Math.Max(dto.TotalWinTimes, 0),
            strongestHit,
            dto.IsUnlock,
            dto.IsJustSkippedFloor,
            string.IsNullOrWhiteSpace(dto.SkippedFloor) ? null : dto.SkippedFloor.Trim(),
            floors,
            MapRank(SpiralAbyssRankKind.Reveal, dto.RevealRank),
            MapRank(SpiralAbyssRankKind.Defeat, dto.DefeatRank),
            MapRank(SpiralAbyssRankKind.Damage, dto.DamageRank),
            MapRank(SpiralAbyssRankKind.TakeDamage, dto.TakeDamageRank),
            MapRank(SpiralAbyssRankKind.NormalSkill, dto.NormalSkillRank),
            MapRank(SpiralAbyssRankKind.EnergySkill, dto.EnergySkillRank),
            context);
    }

    private static SpiralAbyssFloor MapFloor(SpiralAbyssFloorDto dto, CycleChallengeTimeContext context)
    {
        var levels = new List<SpiralAbyssLevel>();
        foreach (var levelDto in dto.Levels ?? new List<SpiralAbyssLevelDto>())
        {
            if (levelDto is null)
            {
                continue;
            }

            levels.Add(MapLevel(levelDto, context));
        }

        levels.Sort(static (left, right) => left.Index.CompareTo(right.Index));

        // 地脉异常三形态互斥（校准结论）：common、upper、lower 通常只有一组非空。
        // 文案为中文，原样保留（只去掉空行），由控件按「上/下优先，common 回退」的规则展示。
        var leyLineCommon = NormalizeTextList(dto.LeyLineDisorder);
        var leyLineUpper = NormalizeTextList(dto.LeyLineDisorderUpper);
        var leyLineLower = NormalizeTextList(dto.LeyLineDisorderLower);

        return new SpiralAbyssFloor(
            dto.Index,
            dto.IsUnlock,
            Math.Max(dto.Star, 0),
            Math.Max(dto.MaxStar, 0),
            leyLineCommon,
            leyLineUpper,
            leyLineLower,
            levels);
    }

    private static SpiralAbyssLevel MapLevel(SpiralAbyssLevelDto dto, CycleChallengeTimeContext context)
    {
        var battles = new List<SpiralAbyssBattle>();
        foreach (var battleDto in dto.Battles ?? new List<SpiralAbyssBattleDto>())
        {
            if (battleDto is null)
            {
                continue;
            }

            battles.Add(MapBattle(battleDto, context));
        }

        // 1 = 上半，2 = 下半，按 index 升序后控件取 [0]/[1]。
        battles.Sort(static (left, right) => left.Index.CompareTo(right.Index));

        return new SpiralAbyssLevel(
            dto.Index,
            Math.Max(dto.Star, 0),
            Math.Max(dto.MaxStar, 0),
            battles);
    }

    private static SpiralAbyssBattle MapBattle(SpiralAbyssBattleDto dto, CycleChallengeTimeContext context)
    {
        var avatars = new List<SpiralAbyssAvatar>();
        foreach (var avatarDto in dto.Avatars ?? new List<SpiralAbyssAvatarDto>())
        {
            if (avatarDto is null)
            {
                continue;
            }

            avatars.Add(new SpiralAbyssAvatar(
                avatarDto.Id,
                NormalizeIconUrl(avatarDto.Icon),
                Math.Max(avatarDto.Level, 0),
                Math.Max(avatarDto.Rarity, 0)));
        }

        return new SpiralAbyssBattle(
            dto.Index,
            ParseDateTimeElement(dto.SettleDateTime, context),
            ParseTime(dto.Timestamp),
            avatars,
            context);
    }

    private static SpiralAbyssRankSection MapRank(SpiralAbyssRankKind kind, List<SpiralAbyssRankDto>? rankDtos)
    {
        var rows = new List<SpiralAbyssRankRow>();
        foreach (var rankDto in rankDtos ?? new List<SpiralAbyssRankDto>())
        {
            if (rankDto is null)
            {
                continue;
            }

            rows.Add(new SpiralAbyssRankRow(
                rankDto.AvatarId,
                NormalizeIconUrl(rankDto.AvatarIcon),
                Math.Max(rankDto.Rarity, 0),
                rankDto.Value));
        }

        return new SpiralAbyssRankSection(kind, rows);
    }

    /// <summary>
    /// 解析 max_floor：既能吃 "12" 这种纯数字字符串，也能吃 "12-3" 这种原始形态；
    /// 都不可用时退回「最深的已解锁层」。
    /// </summary>
    private static (int Value, string Text) ResolveMaxFloor(string? raw, IReadOnlyList<SpiralAbyssFloor> floors)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var text = raw.Trim();
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            {
                var value = Math.Max(numeric, 0);
                return (value, value > 0 ? value.ToString(CultureInfo.InvariantCulture) : string.Empty);
            }

            var head = text.Split('-', '－')[0];
            if (int.TryParse(head, NumberStyles.Integer, CultureInfo.InvariantCulture, out var floorNo))
            {
                return (Math.Max(floorNo, 0), text);
            }

            return (0, text);
        }

        var deepest = 0;
        foreach (var floor in floors)
        {
            if (floor.Index > deepest)
            {
                deepest = floor.Index;
            }
        }

        return (deepest, deepest > 0 ? deepest.ToString(CultureInfo.InvariantCulture) : string.Empty);
    }

    /// <summary>
    /// 解析时间：兼容 unix 秒（数字或"数字字符串"）、unix 毫秒、以及
    /// "2026-10-15 20:00:00+00:00" / ISO8601 字符串；0 或无法解析返回 null。
    /// </summary>
    private static DateTimeOffset? ParseTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            if (unix <= 0)
            {
                return null;
            }

            return unix > 100_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(unix)
                : DateTimeOffset.FromUnixTimeSeconds(unix);
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var unixDouble) && unixDouble > 0)
        {
            return unixDouble > 100_000_000_000d
                ? DateTimeOffset.FromUnixTimeMilliseconds((long)unixDouble)
                : DateTimeOffset.FromUnixTimeSeconds((long)unixDouble);
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime))
        {
            return new DateTimeOffset(dateTime, TimeSpan.Zero);
        }

        return null;
    }

    /// <summary>
    /// 解析 settle_date_time（校准：战斗级才是真实通关时间；楼层级恒 null）。
    /// 兼容三种形态：1) <c>{ year, month, day, hour, minute, second }</c> 对象；2) 日期字符串；3) unix 秒数字。
    /// 返回 <see cref="DateTimeKind.Unspecified"/> 的「墙上时间」——接口给的就是服务器本地时间，不做时区换算。
    /// </summary>
    private static DateTime? ParseDateTimeElement(JsonElement? element, CycleChallengeTimeContext context)
    {
        if (element is not { } value)
        {
            return null;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var year = ReadInt(value, "year");
                var month = ReadInt(value, "month");
                var day = ReadInt(value, "day");
                if (year is not > 0 || month is not > 0 || day is not > 0)
                {
                    return null;
                }

                var hour = ReadInt(value, "hour") ?? 0;
                var minute = ReadInt(value, "minute") ?? 0;
                var second = ReadInt(value, "second") ?? 0;
                try
                {
                    return new DateTime(
                        year.Value,
                        month.Value,
                        day.Value,
                        Math.Clamp(hour, 0, 23),
                        Math.Clamp(minute, 0, 59),
                        Math.Clamp(second, 0, 59),
                        DateTimeKind.Unspecified);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }
            }

            case JsonValueKind.String:
            {
                var text = value.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    break;
                }

                // 无偏移：接口给的是服务器墙上时间，原样返回，由 FormatWallClock 统一换算展示时区
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var wall)
                    && wall.Kind == DateTimeKind.Unspecified)
                {
                    return wall;
                }

                // 带偏移（含 Z）：先换到服务器时区，再由 FormatWallClock 换算一次
                if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                {
                    return parsed.ToOffset(context.SourceOffset).DateTime;
                }

                break;
            }

            case JsonValueKind.Number:
                if (value.TryGetInt64(out var unix) && unix > 0)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unix).ToOffset(context.SourceOffset).DateTime;
                }

                break;
        }

        return null;
    }

    /// <summary>读取日期对象里的整数字段；兼容 number 与数字字符串两种序列化。</summary>
    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            // 兼容首字母大写写法（保守容错）
            var pascalName = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
            if (!element.TryGetProperty(pascalName, out property))
            {
                return null;
            }
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    /// <summary>把字符串数组规范化为「去空白、去空行」的文案列表，顺序与原文保持不变。</summary>
    private static List<string> NormalizeTextList(List<string>? texts)
    {
        var result = new List<string>();
        foreach (var text in texts ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Add(text.Trim());
            }
        }

        return result;
    }

    /// <summary>头像 URL 轻量规范化：去空白、补协议相对路径；无效返回 null。</summary>
    private static string? NormalizeIconUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (text.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + text;
        }

        return text;
    }
}
