/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json;
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.CycleChallenge.Dto;

public sealed class RoleCombatResponseDto
{
    [JsonPropertyName("data")]
    public List<RoleCombatEntryDto>? Data
    {
        get;
        set;
    }


    [JsonPropertyName("is_unlock")]
    public bool? IsUnlock
    {
        get;
        set;
    }


    [JsonPropertyName("links")]
    public RoleCombatLinksDto? Links
    {
        get;
        set;
    }
}

public sealed class RoleCombatEntryDto
{
    [JsonPropertyName("uid")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Uid
    {
        get;
        set;
    }


    [JsonPropertyName("schedule_id")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? ScheduleId
    {
        get;
        set;
    }


    [JsonPropertyName("start_time")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? StartTime
    {
        get;
        set;
    }

    /// <summary>本期结束时间，形态同 <see cref="StartTime" />。</summary>
    [JsonPropertyName("end_time")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? EndTime
    {
        get;
        set;
    }

    /// <summary>难度 ID（1 简单 / 2 普通 / 3 困难 / 4 映辉 / 5 月演）。</summary>
    [JsonPropertyName("difficulty_id")]
    public int? DifficultyId
    {
        get;
        set;
    }

    /// <summary>历史最高幕数（注意：它不是 rounds_data 的长度，塔罗轮会额外增加轮次）。</summary>
    [JsonPropertyName("max_round_id")]
    public int? MaxRoundId
    {
        get;
        set;
    }

    /// <summary>纹章等级。真实数据中与 difficulty_id 恒等。</summary>
    [JsonPropertyName("heraldry")]
    public int? Heraldry
    {
        get;
        set;
    }

    /// <summary>徽记数量（唯一可信来源，不要用 get_medal_round_list 求和）。</summary>
    [JsonPropertyName("medal_num")]
    public int? MedalNum
    {
        get;
        set;
    }

    /// <summary>明细数据；<c>has_detail_data=false</c> 时可能为空对象或 null。</summary>
    [JsonPropertyName("detail")]
    public RoleCombatDetailDto? Detail
    {
        get;
        set;
    }

    /// <summary>本期统计（与 <see cref="Detail" /> 内的 detail_stat 内容一致）。</summary>
    [JsonPropertyName("stat")]
    public RoleCombatStatDto? Stat
    {
        get;
        set;
    }

    /// <summary>期次时间范围。</summary>
    [JsonPropertyName("schedule")]
    public RoleCombatScheduleDto? Schedule
    {
        get;
        set;
    }

    /// <summary>本期是否有数据（false 表示未参与）。</summary>
    [JsonPropertyName("has_data")]
    public bool? HasData
    {
        get;
        set;
    }

    /// <summary>本期是否有明细（false 表示只有汇总、没有逐幕数据）。</summary>
    [JsonPropertyName("has_detail_data")]
    public bool? HasDetailData
    {
        get;
        set;
    }
}

/// <summary>本期统计。</summary>
public sealed class RoleCombatStatDto
{
    /// <summary>难度 ID。</summary>
    [JsonPropertyName("difficulty_id")]
    public int? DifficultyId
    {
        get;
        set;
    }

    /// <summary>最高幕数。</summary>
    [JsonPropertyName("max_round_id")]
    public int? MaxRoundId
    {
        get;
        set;
    }

    /// <summary>纹章等级。</summary>
    [JsonPropertyName("heraldry")]
    public int? Heraldry
    {
        get;
        set;
    }

    /// <summary>
    ///     获得徽记的轮次列表（逐轮 0/1）。
    ///     真实数据里它包含塔罗轮，求和结果与 medal_num 不一致，因此只作为「逐轮是否得徽记」的兜底。
    /// </summary>
    [JsonPropertyName("get_medal_round_list")]
    public List<int>? GetMedalRoundList
    {
        get;
        set;
    }

    /// <summary>徽记数量。</summary>
    [JsonPropertyName("medal_num")]
    public int? MedalNum
    {
        get;
        set;
    }

    /// <summary>消耗的幻剧之花数量。</summary>
    [JsonPropertyName("coin_num")]
    public int? CoinNum
    {
        get;
        set;
    }

    /// <summary>触发场外声援次数。</summary>
    [JsonPropertyName("avatar_bonus_num")]
    public int? AvatarBonusNum
    {
        get;
        set;
    }

    /// <summary>助演角色被借用次数。</summary>
    [JsonPropertyName("rent_cnt")]
    public int? RentCount
    {
        get;
        set;
    }

    /// <summary>完成的塔罗牌数量；该键可能缺失。</summary>
    [JsonPropertyName("tarot_finished_cnt")]
    public int? TarotFinishedCount
    {
        get;
        set;
    }
}

/// <summary>剧诗明细。</summary>
public sealed class RoleCombatDetailDto
{
    /// <summary>逐幕数据（升序；含塔罗轮，round_id 可能重复）。</summary>
    [JsonPropertyName("rounds_data")]
    public List<RoleCombatRoundDto>? RoundsData
    {
        get;
        set;
    }

    /// <summary>明细统计（与顶层 stat 一致，可作兜底）。</summary>
    [JsonPropertyName("detail_stat")]
    public RoleCombatStatDto? DetailStat
    {
        get;
        set;
    }

    /// <summary>阵容分享链接；真实数据恒为空。</summary>
    [JsonPropertyName("lineup_link")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? LineupLink
    {
        get;
        set;
    }

    /// <summary>待命（替补）角色。</summary>
    [JsonPropertyName("backup_avatars")]
    public List<RoleCombatAvatarDto>? BackupAvatars
    {
        get;
        set;
    }

    /// <summary>战斗统计（注意接口拼写就是 fight_statisic）。</summary>
    [JsonPropertyName("fight_statisic")]
    public RoleCombatFightStatisticsDto? FightStatistics
    {
        get;
        set;
    }
}

/// <summary>单幕数据。</summary>
public sealed class RoleCombatRoundDto
{
    /// <summary>本幕出战角色。</summary>
    [JsonPropertyName("avatars")]
    public List<RoleCombatAvatarDto>? Avatars
    {
        get;
        set;
    }

    /// <summary>本幕选择的神秘收获。</summary>
    [JsonPropertyName("choice_cards")]
    public List<RoleCombatBuffDto>? ChoiceCards
    {
        get;
        set;
    }

    /// <summary>本幕生效的祝福。</summary>
    [JsonPropertyName("buffs")]
    public List<RoleCombatBuffDto>? Buffs
    {
        get;
        set;
    }

    /// <summary>本幕是否获得徽记。</summary>
    [JsonPropertyName("is_get_medal")]
    public bool? IsGetMedal
    {
        get;
        set;
    }

    /// <summary>幕号（塔罗轮会复用前一个普通幕的 round_id，不能当唯一键）。</summary>
    [JsonPropertyName("round_id")]
    public int? RoundId
    {
        get;
        set;
    }

    /// <summary>本幕完成时间（unix 秒字符串，不是战斗用时！）。</summary>
    [JsonPropertyName("finish_time")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? FinishTime
    {
        get;
        set;
    }

    /// <summary>本幕完成日期时间（服务器时区，展示优先使用该字段）。</summary>
    [JsonPropertyName("finish_date_time")]
    public RoleCombatDateTimeDto? FinishDateTime
    {
        get;
        set;
    }

    /// <summary>本幕敌人。</summary>
    [JsonPropertyName("enemies")]
    public List<RoleCombatEnemyDto>? Enemies
    {
        get;
        set;
    }

    /// <summary>辉彩祝福汇总。</summary>
    [JsonPropertyName("splendour_buff")]
    public RoleCombatSplendourBuffDto? SplendourBuff
    {
        get;
        set;
    }

    /// <summary>本幕是否为塔罗（圣牌）挑战；该键可能缺失。</summary>
    [JsonPropertyName("is_tarot")]
    public bool? IsTarot
    {
        get;
        set;
    }

    /// <summary>塔罗序号（仅用于文案，如 1 → I、2 → II；不是图标映射依据）。</summary>
    [JsonPropertyName("tarot_serial_no")]
    public int? TarotSerialNumber
    {
        get;
        set;
    }
}

/// <summary>剧诗角色（出战 / 待命通用）。</summary>
public sealed class RoleCombatAvatarDto
{
    /// <summary>角色 ID。</summary>
    [JsonPropertyName("avatar_id")]
    public int? AvatarId
    {
        get;
        set;
    }

    /// <summary>角色来源：1 自有、2 试用、3 借用（助演）。</summary>
    [JsonPropertyName("avatar_type")]
    public int? AvatarType
    {
        get;
        set;
    }

    /// <summary>角色名。</summary>
    [JsonPropertyName("name")]
    public string? Name
    {
        get;
        set;
    }

    /// <summary>元素名（如 Cryo / Pyro，国际化字符串）。</summary>
    [JsonPropertyName("element")]
    public string? Element
    {
        get;
        set;
    }

    /// <summary>头像绝对 URL（act-webstatic 域名）。</summary>
    [JsonPropertyName("image")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Image
    {
        get;
        set;
    }

    /// <summary>角色等级。</summary>
    [JsonPropertyName("level")]
    public int? Level
    {
        get;
        set;
    }

    /// <summary>稀有度（4 / 5）。</summary>
    [JsonPropertyName("rarity")]
    public int? Rarity
    {
        get;
        set;
    }
}

/// <summary>剧诗敌人。</summary>
public sealed class RoleCombatEnemyDto
{
    /// <summary>敌人名。</summary>
    [JsonPropertyName("name")]
    public string? Name
    {
        get;
        set;
    }

    /// <summary>敌人图标绝对 URL。</summary>
    [JsonPropertyName("icon")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Icon
    {
        get;
        set;
    }

    /// <summary>敌人等级。</summary>
    [JsonPropertyName("level")]
    public int? Level
    {
        get;
        set;
    }
}

/// <summary>祝福 / 神秘收获条目。</summary>
public sealed class RoleCombatBuffDto
{
    /// <summary>祝福 ID。</summary>
    [JsonPropertyName("id")]
    public int? Id
    {
        get;
        set;
    }

    /// <summary>图标名。</summary>
    [JsonPropertyName("icon")]
    public string? Icon
    {
        get;
        set;
    }

    /// <summary>名称。</summary>
    [JsonPropertyName("name")]
    public string? Name
    {
        get;
        set;
    }

    /// <summary>描述（可能包含 &lt;color=#RRGGBBAA&gt; 富文本）。</summary>
    [JsonPropertyName("desc")]
    public string? Description
    {
        get;
        set;
    }

    /// <summary>是否强化过。</summary>
    [JsonPropertyName("is_enhanced")]
    public bool? IsEnhanced
    {
        get;
        set;
    }
}

/// <summary>辉彩祝福汇总。</summary>
public sealed class RoleCombatSplendourBuffDto
{
    /// <summary>祝福等级与描述。</summary>
    [JsonPropertyName("summary")]
    public RoleCombatSplendourBuffSummaryDto? Summary
    {
        get;
        set;
    }

    /// <summary>已获得的祝福。</summary>
    [JsonPropertyName("buffs")]
    public List<RoleCombatSplendourBuffItemDto>? Buffs
    {
        get;
        set;
    }
}

/// <summary>辉彩祝福汇总信息。</summary>
public sealed class RoleCombatSplendourBuffSummaryDto
{
    /// <summary>总等级。</summary>
    [JsonPropertyName("total_level")]
    public int? TotalLevel
    {
        get;
        set;
    }

    /// <summary>描述（含富文本颜色标记）。</summary>
    [JsonPropertyName("desc")]
    public string? Description
    {
        get;
        set;
    }
}

/// <summary>单条辉彩祝福。</summary>
public sealed class RoleCombatSplendourBuffItemDto
{
    /// <summary>图标名。</summary>
    [JsonPropertyName("icon")]
    public string? Icon
    {
        get;
        set;
    }

    /// <summary>名称。</summary>
    [JsonPropertyName("name")]
    public string? Name
    {
        get;
        set;
    }

    /// <summary>等级。</summary>
    [JsonPropertyName("level")]
    public int? Level
    {
        get;
        set;
    }

    /// <summary>等级效果。</summary>
    [JsonPropertyName("level_effect")]
    public List<RoleCombatBuffDto>? LevelEffects
    {
        get;
        set;
    }
}

/// <summary>战斗统计（接口拼写 fight_statisic）。</summary>
public sealed class RoleCombatFightStatisticsDto
{
    /// <summary>击败敌人最多的角色；可能为 null。</summary>
    [JsonPropertyName("max_defeat_avatar")]
    public RoleCombatAvatarStatisticsDto? MaxDefeatAvatar
    {
        get;
        set;
    }

    /// <summary>造成伤害最高的角色；可能为 null。</summary>
    [JsonPropertyName("max_damage_avatar")]
    public RoleCombatAvatarStatisticsDto? MaxDamageAvatar
    {
        get;
        set;
    }

    /// <summary>承受伤害最高的角色；可能为 null。</summary>
    [JsonPropertyName("max_take_damage_avatar")]
    public RoleCombatAvatarStatisticsDto? MaxTakeDamageAvatar
    {
        get;
        set;
    }

    /// <summary>消耗的幻剧之花（真实数据中为 null）。</summary>
    [JsonPropertyName("total_coin_consumed")]
    public RoleCombatAvatarStatisticsDto? TotalCoinConsumed
    {
        get;
        set;
    }

    /// <summary>最快通关队伍中的角色。</summary>
    [JsonPropertyName("shortest_avatar_list")]
    public List<RoleCombatAvatarStatisticsDto>? ShortestAvatarList
    {
        get;
        set;
    }

    /// <summary>演出总时长（秒）。</summary>
    [JsonPropertyName("total_use_time")]
    public int? TotalUseTime
    {
        get;
        set;
    }

    /// <summary>是否展示战斗统计。</summary>
    [JsonPropertyName("is_show_battle_stats")]
    public bool? IsShowBattleStats
    {
        get;
        set;
    }
}

/// <summary>战斗统计中的角色数值条目。</summary>
public sealed class RoleCombatAvatarStatisticsDto
{
    /// <summary>角色 ID。</summary>
    [JsonPropertyName("avatar_id")]
    public int? AvatarId
    {
        get;
        set;
    }

    /// <summary>角色头像绝对 URL。</summary>
    [JsonPropertyName("avatar_icon")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? AvatarIcon
    {
        get;
        set;
    }

    /// <summary>数值字符串（可能是 "" 表示 0）。</summary>
    [JsonPropertyName("value")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Value
    {
        get;
        set;
    }

    /// <summary>稀有度。</summary>
    [JsonPropertyName("rarity")]
    public int? Rarity
    {
        get;
        set;
    }
}

/// <summary>期次信息。</summary>
public sealed class RoleCombatScheduleDto
{
    /// <summary>开始时间（unix 秒字符串）。</summary>
    [JsonPropertyName("start_time")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? StartTime
    {
        get;
        set;
    }

    /// <summary>结束时间（unix 秒字符串）。</summary>
    [JsonPropertyName("end_time")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? EndTime
    {
        get;
        set;
    }

    /// <summary>期次类型。</summary>
    [JsonPropertyName("schedule_type")]
    public int? ScheduleType
    {
        get;
        set;
    }

    /// <summary>期次 ID。</summary>
    [JsonPropertyName("schedule_id")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? ScheduleId
    {
        get;
        set;
    }

    /// <summary>开始日期时间（对象形式，展示优先使用）。</summary>
    [JsonPropertyName("start_date_time")]
    public RoleCombatDateTimeDto? StartDateTime
    {
        get;
        set;
    }

    /// <summary>结束日期时间（对象形式，展示优先使用）。</summary>
    [JsonPropertyName("end_date_time")]
    public RoleCombatDateTimeDto? EndDateTime
    {
        get;
        set;
    }
}

/// <summary>接口给出的日期时间对象（服务器时区，按原样展示，不进行时区换算）。</summary>
public sealed class RoleCombatDateTimeDto
{
    /// <summary>年。</summary>
    [JsonPropertyName("year")]
    public int? Year
    {
        get;
        set;
    }

    /// <summary>月。</summary>
    [JsonPropertyName("month")]
    public int? Month
    {
        get;
        set;
    }

    /// <summary>日。</summary>
    [JsonPropertyName("day")]
    public int? Day
    {
        get;
        set;
    }

    /// <summary>时。</summary>
    [JsonPropertyName("hour")]
    public int? Hour
    {
        get;
        set;
    }

    /// <summary>分。</summary>
    [JsonPropertyName("minute")]
    public int? Minute
    {
        get;
        set;
    }

    /// <summary>秒。</summary>
    [JsonPropertyName("second")]
    public int? Second
    {
        get;
        set;
    }
}

/// <summary>攻略 / 阵容分享链接（展示层不使用，仅为 1:1 完整性保留）。</summary>
public sealed class RoleCombatLinksDto
{
    /// <summary>阵容详情链接。</summary>
    [JsonPropertyName("lineup_link")]
    public string? LineupLink
    {
        get;
        set;
    }

    /// <summary>阵容详情链接（PC）。</summary>
    [JsonPropertyName("lineup_link_pc")]
    public string? LineupLinkPc
    {
        get;
        set;
    }

    /// <summary>攻略链接。</summary>
    [JsonPropertyName("strategy_link")]
    public string? StrategyLink
    {
        get;
        set;
    }

    /// <summary>阵容发布链接。</summary>
    [JsonPropertyName("lineup_publish_link")]
    public string? LineupPublishLink
    {
        get;
        set;
    }

    /// <summary>阵容发布链接（PC）。</summary>
    [JsonPropertyName("lineup_publish_link_pc")]
    public string? LineupPublishLinkPc
    {
        get;
        set;
    }
}

/// <summary>解包结果。<see cref="Entry" /> 为单期数据对象，无法定位时为 null。</summary>
/// <param name="Entry">单期数据元素（已 Clone，可脱离 JsonDocument 使用）。</param>
/// <param name="IsUnlock">外层 is_unlock；未给出时为 null（按已解锁处理）。</param>
public sealed record CycleChallengePayloadEnvelope(JsonElement? Entry, bool? IsUnlock);

/// <summary>
///     剧诗 / 危战面板共用的 payload 解包器。
///     <para>
///         支持三种入参形态（服务层当前会把单期 payload 包成 <c>{ retcode, data: [entry] }</c>，
///         但缓存迁移 / 其它调用方可能直接给 data 节点或单期对象）：
///         <list type="number">
///             <item><c>{ retcode, message, data: [ {...} ], is_unlock }</c>（完整响应）；</item>
///             <item><c>{ data: { ... } }</c> 或直接 <c>{ schedule, single, ... }</c>（仅 data / 单期）；</item>
///             <item>最外层数组（保守兼容，取首个对象）。</item>
///         </list>
///         无法定位对象时返回 <c>Entry = null</c>，不抛异常；JSON 语法错误仍会抛 <see cref="JsonException" />，
///         由展示控件捕获并显示错误占位。
///     </para>
/// </summary>
public static class CycleChallengePayloadReader
{
    /// <summary>剧诗 / 危战 DTO 共用的反序列化选项。</summary>
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

    /// <summary>解析 payload 并定位单期数据对象。</summary>
    /// <param name="payloadJson">单期 payload JSON。</param>
    /// <returns>解包结果；无法定位单期对象时 <c>Entry</c> 为 null。</returns>
    /// <exception cref="JsonException">JSON 本身不合法。</exception>
    public static CycleChallengePayloadEnvelope Read(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new CycleChallengePayloadEnvelope(null, null);
        }

        using var document = JsonDocument.Parse(payloadJson, DocumentOptions);
        var root = document.RootElement;

        // 顶层是数组时取首个对象
        if (root.ValueKind == JsonValueKind.Array)
        {
            if (root.GetArrayLength() == 0)
            {
                return new CycleChallengePayloadEnvelope(null, null);
            }

            root = root[0];
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return new CycleChallengePayloadEnvelope(null, null);
        }

        bool? isUnlock = TryReadBool(root, "is_unlock");

        // 最多向下解包 3 层 data，兼容完整响应 → data 对象 → 单期对象等形态
        var current = root;
        for (var depth = 0; depth < 3; depth++)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                return new CycleChallengePayloadEnvelope(null, isUnlock);
            }

            isUnlock ??= TryReadBool(current, "is_unlock");

            if (!current.TryGetProperty("data", out var data))
            {
                return new CycleChallengePayloadEnvelope(current.Clone(), isUnlock);
            }

            switch (data.ValueKind)
            {
                case JsonValueKind.Array:
                    if (data.GetArrayLength() == 0)
                    {
                        return new CycleChallengePayloadEnvelope(null, isUnlock);
                    }

                    var first = data[0];
                    return first.ValueKind == JsonValueKind.Object
                        ? new CycleChallengePayloadEnvelope(first.Clone(), isUnlock)
                        : new CycleChallengePayloadEnvelope(null, isUnlock);

                case JsonValueKind.Object:
                    current = data;
                    break;

                default:
                    return new CycleChallengePayloadEnvelope(null, isUnlock);
            }
        }

        return new CycleChallengePayloadEnvelope(current.ValueKind == JsonValueKind.Object ? current.Clone() : null,
            isUnlock);
    }

    private static bool? TryReadBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }
}