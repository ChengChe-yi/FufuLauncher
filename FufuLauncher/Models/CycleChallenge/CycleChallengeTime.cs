/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;

namespace FufuLauncher.Models.CycleChallenge;

/// <summary>
/// 时间显示上下文：<see cref="SourceOffset"/> 表示接口墙上时间对应的服务器时区，
/// <see cref="DisplayOffset"/> 表示最终展示时区。
/// 规则（按需求）：
/// <list type="bullet">
/// <item>国服（天空岛 / 世界树等 cn_* 区服）：展示 UTC+8；</item>
/// <item>其它区服（os_*）：展示 UTC。</item>
/// </list>
/// </summary>
public readonly record struct CycleChallengeTimeContext(TimeSpan SourceOffset, TimeSpan DisplayOffset)
{
    /// <summary>国服默认上下文（服务器 UTC+8，展示 UTC+8）。</summary>
    public static CycleChallengeTimeContext Default { get; } = new(CycleChallengeTime.Utc8, CycleChallengeTime.Utc8);
}

/// <summary>周期挑战时间工具：unix 秒 / 墙上时间 / 带偏移字符串 → 展示时区。</summary>
public static class CycleChallengeTime
{
    /// <summary>UTC+8。</summary>
    public static readonly TimeSpan Utc8 = TimeSpan.FromHours(8);

    /// <summary>按区服得到展示时区：与账号所在服务器时区一致（国服 UTC+8，美服 UTC-5，欧服 UTC+1，亚服/港澳台 UTC+8）。</summary>
    public static TimeSpan GetDisplayOffset(string? region) => GetServerOffset(region);

    /// <summary>按区服得到服务器时区（用于解释无时区的墙上时间字段）。</summary>
    public static TimeSpan GetServerOffset(string? region) => region?.ToLowerInvariant() switch
    {
        "os_usa" => TimeSpan.FromHours(-5),
        "os_euro" => TimeSpan.FromHours(1),
        // 亚服 / 港澳台与国服同为 UTC+8
        "os_asia" => Utc8,
        "os_cht" => Utc8,
        _ => Utc8,
    };

    /// <summary>由区服构造时间上下文；region 为空时按国服处理。</summary>
    public static CycleChallengeTimeContext CreateContext(string? region) =>
        new(GetServerOffset(region), GetDisplayOffset(region));

    /// <summary>把任意时刻换算到展示时区。</summary>
    public static DateTimeOffset ToDisplay(DateTimeOffset value, CycleChallengeTimeContext context) =>
        value.ToOffset(context.DisplayOffset);

    /// <summary>
    /// 格式化数据库里保存的期次时间：
    /// 新格式带偏移（如 <c>2026-10-15 20:00:00+00:00</c>）→ 换算到展示（账号服务器）时区；
    /// 旧格式不含偏移（历史缓存，已是服务器墙上时间）→ 直接按原文格式化。
    /// </summary>
    public static string FormatStoredString(string? raw, string format, TimeSpan displayOffset)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw.Trim();
        if (HasExplicitOffset(text))
        {
            return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed.ToOffset(displayOffset).ToString(format, CultureInfo.InvariantCulture)
                : text;
        }

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var wall)
            ? wall.ToString(format, CultureInfo.InvariantCulture)
            : text;
    }

    /// <summary>把绝对时刻规范化为带 +00:00 偏移的 UTC 字符串（用于入库）。</summary>
    public static string ToUtcStorageString(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss'+00:00'", CultureInfo.InvariantCulture);

    private static bool HasExplicitOffset(string text)
    {
        if (text.EndsWith('Z'))
        {
            return true;
        }

        var timeSeparator = text.IndexOf(' ');
        var afterTime = timeSeparator >= 0 ? text[timeSeparator..] : text;
        return afterTime.Contains('+', StringComparison.Ordinal) ||
               (afterTime.LastIndexOf('-') > 0);
    }

    /// <summary>按展示时区格式化时刻。</summary>
    public static string Format(DateTimeOffset value, string format, CycleChallengeTimeContext context) =>
        ToDisplay(value, context).ToString(format, CultureInfo.InvariantCulture);

    /// <summary>按展示时区格式化 unix 秒。</summary>
    public static string FormatUnix(long unixSeconds, string format, CycleChallengeTimeContext context) =>
        Format(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), format, context);

    /// <summary>
    /// 把接口返回的墙上时间（无时区信息）按服务器时区解释，再换算到展示时区。
    /// </summary>
    public static string FormatWallClock(DateTime value, string format, CycleChallengeTimeContext context)
    {
        var unspecified = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, context.SourceOffset)
            .ToOffset(context.DisplayOffset)
            .ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>墙上时间 → 展示时区的 <see cref="DateTimeOffset"/>。</summary>
    public static DateTimeOffset WallClockToDisplay(DateTime value, CycleChallengeTimeContext context)
    {
        var unspecified = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, context.SourceOffset).ToOffset(context.DisplayOffset);
    }
}
