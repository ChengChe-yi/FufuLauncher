/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.CycleChallenge.Dto;

/// <summary>
/// 深境螺旋接口 data 节点的 1:1 反序列化模型（字段名与米游社 game_record 响应原名一致）。
/// <para>
/// 真实数据（已用 Starward 缓存库校准）存在两类「类型漂移」，因此本文件做了两层容错：
/// 1. 数值字段可能被序列化成字符串（如 <c>settle_time="0"</c>、<c>max_floor="12"</c>、
///    <c>total_star="36"</c>），解析时统一打开 <see cref="JsonNumberHandling.AllowReadingFromString"/>；
/// 2. 时间 / 标识类字段既可能是 number 也可能是 string（如 <c>start_time="2026-10-15 20:00:00+00:00"</c>、
///    <c>timestamp</c>、<c>uid</c>、<c>schedule_id</c>），统一交给 <see cref="FlexibleStringConverter"/>
///    转成原始字符串，由映射层再解析成强类型。
/// </para>
/// 可选字段一律用可空类型，缺失时不会让整份 payload 解析失败。
/// </summary>
public sealed class SpiralAbyssDto
{
    /// <summary>玩家 UID（真实数据可能是数字）。</summary>
    [JsonPropertyName("uid")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Uid
    {
        get;
        set;
    }

    /// <summary>期次 ID（真实数据可能是数字）。</summary>
    [JsonPropertyName("schedule_id")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? ScheduleId
    {
        get;
        set;
    }

    /// <summary>本期开始时间，可能是 unix 秒数字，也可能是 "2026-10-15 20:00:00+00:00" 字符串。</summary>
    [JsonPropertyName("start_time")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? StartTime
    {
        get;
        set;
    }

    /// <summary>本期结束时间，形态同 <see cref="StartTime"/>。</summary>
    [JsonPropertyName("end_time")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? EndTime
    {
        get;
        set;
    }

    /// <summary>总战斗次数。</summary>
    [JsonPropertyName("total_battle_times")]
    public int TotalBattleTimes
    {
        get;
        set;
    }

    /// <summary>总获胜次数。</summary>
    [JsonPropertyName("total_win_times")]
    public int TotalWinTimes
    {
        get;
        set;
    }

    /// <summary>最深层数，常见形态为 "12"，也可能被序列化成数字，保留原样。</summary>
    [JsonPropertyName("max_floor")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? MaxFloor
    {
        get;
        set;
    }

    /// <summary>本期总星数（满星 36）。</summary>
    [JsonPropertyName("total_star")]
    public int TotalStar
    {
        get;
        set;
    }

    /// <summary>本期是否解锁（未解锁时 floors 可能为空）。</summary>
    [JsonPropertyName("is_unlock")]
    public bool IsUnlock
    {
        get;
        set;
    }

    /// <summary>本期是否存在「跳层」（直接跳过某层的一半）。</summary>
    [JsonPropertyName("is_just_skipped_floor")]
    public bool IsJustSkippedFloor
    {
        get;
        set;
    }

    /// <summary>被跳过的层/间，形如 "10-3"；未跳层时为空字符串或 null。</summary>
    [JsonPropertyName("skipped_floor")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? SkippedFloor
    {
        get;
        set;
    }

    /// <summary>出战次数榜。</summary>
    [JsonPropertyName("reveal_rank")]
    public List<SpiralAbyssRankDto>? RevealRank
    {
        get;
        set;
    }

    /// <summary>最多击破榜。</summary>
    [JsonPropertyName("defeat_rank")]
    public List<SpiralAbyssRankDto>? DefeatRank
    {
        get;
        set;
    }

    /// <summary>最强一击榜。</summary>
    [JsonPropertyName("damage_rank")]
    public List<SpiralAbyssRankDto>? DamageRank
    {
        get;
        set;
    }

    /// <summary>最多承伤榜。</summary>
    [JsonPropertyName("take_damage_rank")]
    public List<SpiralAbyssRankDto>? TakeDamageRank
    {
        get;
        set;
    }

    /// <summary>元素战技榜。</summary>
    [JsonPropertyName("normal_skill_rank")]
    public List<SpiralAbyssRankDto>? NormalSkillRank
    {
        get;
        set;
    }

    /// <summary>元素爆发榜。</summary>
    [JsonPropertyName("energy_skill_rank")]
    public List<SpiralAbyssRankDto>? EnergySkillRank
    {
        get;
        set;
    }

    /// <summary>逐层成绩。</summary>
    [JsonPropertyName("floors")]
    public List<SpiralAbyssFloorDto>? Floors
    {
        get;
        set;
    }
}

/// <summary>角色榜单行（6 组榜单共用同一结构）。</summary>
public sealed class SpiralAbyssRankDto
{
    [JsonPropertyName("avatar_id")]
    public int AvatarId
    {
        get;
        set;
    }

    /// <summary>角色头像，真实数据是绝对 URL。</summary>
    [JsonPropertyName("avatar_icon")]
    public string? AvatarIcon
    {
        get;
        set;
    }

    /// <summary>榜单数值（伤害可能较大，用 long 承载）。</summary>
    [JsonPropertyName("value")]
    public long Value
    {
        get;
        set;
    }

    [JsonPropertyName("rarity")]
    public int Rarity
    {
        get;
        set;
    }
}

/// <summary>单层成绩。</summary>
public sealed class SpiralAbyssFloorDto
{
    /// <summary>层号，真实数据域 5~12（floors 数量不固定，按响应原样解析）。</summary>
    [JsonPropertyName("index")]
    public int Index
    {
        get;
        set;
    }

    [JsonPropertyName("icon")]
    public string? Icon
    {
        get;
        set;
    }

    [JsonPropertyName("is_unlock")]
    public bool IsUnlock
    {
        get;
        set;
    }

    /// <summary>
    /// 结算时间。校准结论：真实数据恒为字符串 "0"，没有信息量，因此只在 DTO 保留、展示层不使用；
    /// 真实通关时间取 <see cref="SpiralAbyssBattleDto.SettleDateTime"/>。
    /// </summary>
    [JsonPropertyName("settle_time")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? SettleTime
    {
        get;
        set;
    }

    [JsonPropertyName("star")]
    public int Star
    {
        get;
        set;
    }

    [JsonPropertyName("max_star")]
    public int MaxStar
    {
        get;
        set;
    }

    /// <summary>通用地脉异常文案（中文，直接展示；与上/下形态互斥）。</summary>
    [JsonPropertyName("ley_line_disorder")]
    public List<string>? LeyLineDisorder
    {
        get;
        set;
    }

    /// <summary>上半地脉异常（与 common 形态互斥；成对出现时优先于 common）。</summary>
    [JsonPropertyName("ley_line_disorder_upper")]
    public List<string>? LeyLineDisorderUpper
    {
        get;
        set;
    }

    /// <summary>下半地脉异常（与 common 形态互斥；成对出现时优先于 common）。</summary>
    [JsonPropertyName("ley_line_disorder_lower")]
    public List<string>? LeyLineDisorderLower
    {
        get;
        set;
    }

    /// <summary>
    /// 结算日期时间。校准结论：楼层级该字段恒为 null，真实时间在 <see cref="SpiralAbyssBattleDto.SettleDateTime"/>；
    /// 这里仍按 1:1 保留，用 <see cref="JsonElement"/> 原样接收（不展示、不绑定）。
    /// </summary>
    [JsonPropertyName("settle_date_time")]
    public JsonElement? SettleDateTime
    {
        get;
        set;
    }

    /// <summary>本层的「间」。</summary>
    [JsonPropertyName("levels")]
    public List<SpiralAbyssLevelDto>? Levels
    {
        get;
        set;
    }
}

/// <summary>单个「间」。</summary>
public sealed class SpiralAbyssLevelDto
{
    /// <summary>间号，1~3。</summary>
    [JsonPropertyName("index")]
    public int Index
    {
        get;
        set;
    }

    [JsonPropertyName("star")]
    public int Star
    {
        get;
        set;
    }

    [JsonPropertyName("max_star")]
    public int MaxStar
    {
        get;
        set;
    }

    /// <summary>本间的战斗记录（一般为 2 场：index=1 上半 / index=2 下半）。</summary>
    [JsonPropertyName("battles")]
    public List<SpiralAbyssBattleDto>? Battles
    {
        get;
        set;
    }

    /// <summary>上半怪物（可选字段，实际可能是对象/数组/字符串，本项目不展示，原样接收保证不炸）。</summary>
    [JsonPropertyName("top_half_floor_monster")]
    public JsonElement? TopHalfFloorMonster
    {
        get;
        set;
    }

    /// <summary>下半怪物（可选字段，形态同 <see cref="TopHalfFloorMonster"/>）。</summary>
    [JsonPropertyName("bottom_half_floor_monster")]
    public JsonElement? BottomHalfFloorMonster
    {
        get;
        set;
    }
}

/// <summary>单场战斗记录。</summary>
public sealed class SpiralAbyssBattleDto
{
    /// <summary>1 = 上半，2 = 下半。</summary>
    [JsonPropertyName("index")]
    public int Index
    {
        get;
        set;
    }

    /// <summary>战斗发生时间（unix 秒），真实数据可能是数字也可能是字符串。</summary>
    [JsonPropertyName("timestamp")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Timestamp
    {
        get;
        set;
    }

    /// <summary>
    /// 真实通关时间（校准结论：楼层级 settle_date_time 恒为 null，实际时间在战斗级）。
    /// 形如 <c>{ year, month, day, hour, minute, second }</c>，也可能是 null/字符串，
    /// 用 <see cref="JsonElement"/> 原样接收再交给映射层解析，任何形态都不会让整份 payload 解析失败。
    /// </summary>
    [JsonPropertyName("settle_date_time")]
    public JsonElement? SettleDateTime
    {
        get;
        set;
    }

    /// <summary>本场出战的角色（通常 4 个）。</summary>
    [JsonPropertyName("avatars")]
    public List<SpiralAbyssAvatarDto>? Avatars
    {
        get;
        set;
    }
}

/// <summary>战斗中的角色（API 只提供 id / 头像 / 等级 / 稀有度，没有名字与命座）。</summary>
public sealed class SpiralAbyssAvatarDto
{
    [JsonPropertyName("id")]
    public int Id
    {
        get;
        set;
    }

    /// <summary>角色头像，真实数据是绝对 URL。</summary>
    [JsonPropertyName("icon")]
    public string? Icon
    {
        get;
        set;
    }

    [JsonPropertyName("level")]
    public int Level
    {
        get;
        set;
    }

    [JsonPropertyName("rarity")]
    public int Rarity
    {
        get;
        set;
    }
}

/// <summary>
/// 把「数字或字符串」统一读成字符串的转换器。
/// 用于 uid / schedule_id / 起止时间 / max_floor / settle_time / timestamp 这类真实数据里类型会漂移的字段。
/// </summary>
public sealed class FlexibleStringConverter : JsonConverter<string>
{
    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var int64Value))
                {
                    return int64Value.ToString(CultureInfo.InvariantCulture);
                }

                return reader.TryGetDouble(out var doubleValue)
                    ? doubleValue.ToString("R", CultureInfo.InvariantCulture)
                    : null;
            case JsonTokenType.True:
                return "true";
            case JsonTokenType.False:
                return "false";
            case JsonTokenType.Null:
                return null;
            default:
                // 对象/数组等非预期形态：跳过，避免整份 payload 解析失败。
                reader.Skip();
                return null;
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

/// <summary>
/// payload 解析入口：兼容三种入参形态，失败返回 null（调用方负责兜底展示，不抛异常）：
/// 1. 完整米游社响应 <c>{ retcode, message, data: { ... } }</c>；
/// 2. 仅 data 节点（服务端/缓存里可能只存 data）；
/// 3. data 为数组的包装（保守兼容，取首个对象）。
/// </summary>
public static class SpiralAbyssPayloadParser
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// 解析 payload JSON。空串 / 非对象 / data 为 null 时返回 null；JSON 语法错误抛 <see cref="JsonException"/>，
    /// 由展示控件捕获后显示错误占位（不崩溃）。
    /// </summary>
    public static SpiralAbyssDto? Parse(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(payloadJson, DocumentOptions);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            root = root[0];
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Object)
            {
                root = data;
            }
            else if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0 && data[0].ValueKind == JsonValueKind.Object)
            {
                // data 为数组的包装形态（保守兼容，取首个对象）
                root = data[0];
            }
            else
            {
                // data 为 null / 其它形态：视为「无数据」，由调用方展示空态
                return null;
            }
        }

        return root.Deserialize<SpiralAbyssDto>(SerializerOptions);
    }
}
