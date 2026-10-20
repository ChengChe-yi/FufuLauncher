/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json.Serialization;

namespace FufuLauncher.Models.CycleChallenge.Dto;

/// <summary>
/// 幽境危战（hard_challenge）接口响应的 1:1 反序列化模型。
/// <para>
/// 真实响应外层为 <c>{ retcode, message, data: [ { ...单期数据... } ], is_unlock, links }</c>，
/// 单期对象的 <c>single</c> / <c>mp</c> 结构完全一致（单人 / 联机两套），
/// 每套 <c>challenge[]</c> 描述一个关卡，<c>monster</c> 是 <b>对象</b>（不是数组），
/// <c>best_avatar[].dps</c> 是字符串，<c>monster.desc[]</c> 含 <c>&lt;color=#RRGGBBAA&gt;</c> 富文本，
/// <c>mp.has_data</c> 可能为 false、<c>blings</c> 可能为空数组。
/// </para>
/// 数值字段均声明为可空并允许从字符串读取；缺失 / 为 null 不会导致整份 payload 解析失败。
/// </summary>
public sealed class HardChallengeResponseDto
{
    /// <summary>各期数据（真实接口通常返回 3 期）。</summary>
    [JsonPropertyName("data")]
    public List<HardChallengeEntryDto>? Data { get; set; }

    /// <summary>挑战是否解锁；缺失时按已解锁处理。</summary>
    [JsonPropertyName("is_unlock")]
    public bool? IsUnlock { get; set; }

    /// <summary>官方链接（展示层不使用）。</summary>
    [JsonPropertyName("links")]
    public HardChallengeLinksDto? Links { get; set; }
}

/// <summary>单期幽境危战数据。</summary>
public sealed class HardChallengeEntryDto
{
    /// <summary>期次信息。</summary>
    [JsonPropertyName("schedule")]
    public HardChallengeScheduleDto? Schedule { get; set; }

    /// <summary>单人挑战。</summary>
    [JsonPropertyName("single")]
    public HardChallengeModeDto? Single { get; set; }

    /// <summary>多人（联机）挑战。</summary>
    [JsonPropertyName("mp")]
    public HardChallengeModeDto? MultiPlayer { get; set; }

    /// <summary>上榜角色（可能为空数组，真实数据 14/14 期均为空）。</summary>
    [JsonPropertyName("blings")]
    public List<HardChallengeBlingAvatarDto>? Blings { get; set; }
}

/// <summary>期次信息。</summary>
public sealed class HardChallengeScheduleDto
{
    /// <summary>期次 ID（真实数据类型为字符串，如 "5269002"）。</summary>
    [JsonPropertyName("schedule_id")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? ScheduleId { get; set; }

    /// <summary>开始时间（unix 秒字符串）。</summary>
    [JsonPropertyName("start_time")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? StartTime { get; set; }

    /// <summary>结束时间（unix 秒字符串）。</summary>
    [JsonPropertyName("end_time")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? EndTime { get; set; }

    /// <summary>开始日期时间（对象形式；校准结论：优先使用它，每期 10:00 UTC+8）。</summary>
    [JsonPropertyName("start_date_time")]
    public HardChallengeDateTimeDto? StartDateTime { get; set; }

    /// <summary>结束日期时间（对象形式）。</summary>
    [JsonPropertyName("end_date_time")]
    public HardChallengeDateTimeDto? EndDateTime { get; set; }

    /// <summary>期次是否有效。</summary>
    [JsonPropertyName("is_valid")]
    public bool? IsValid { get; set; }

    /// <summary>期次名称（如「幽境危战」）。</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>单套（单人 / 联机）挑战数据。</summary>
public sealed class HardChallengeModeDto
{
    /// <summary>最佳成绩；<c>has_data=false</c> 时为 null。</summary>
    [JsonPropertyName("best")]
    public HardChallengeBestDto? Best { get; set; }

    /// <summary>关卡列表。</summary>
    [JsonPropertyName("challenge")]
    public List<HardChallengeChallengeDto>? Challenges { get; set; }

    /// <summary>该模式是否有数据。</summary>
    [JsonPropertyName("has_data")]
    public bool? HasData { get; set; }
}

/// <summary>最佳成绩。</summary>
public sealed class HardChallengeBestDto
{
    /// <summary>达到的最高难度（1 普通 / 2 进阶 / 3 困难 / 4 险恶 / 5 无畏 / 6 绝境）。</summary>
    [JsonPropertyName("difficulty")]
    public int? Difficulty { get; set; }

    /// <summary>最佳用时（秒）。</summary>
    [JsonPropertyName("second")]
    public int? Second { get; set; }

    /// <summary>难度徽章图标名，如 <c>UI_LeyLineChallenge_Medal_5</c>（可能形如 "A,B"）。</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

/// <summary>单个关卡。</summary>
public sealed class HardChallengeChallengeDto
{
    /// <summary>关卡（敌人）名称。</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>关卡用时（秒）。</summary>
    [JsonPropertyName("second")]
    public int? Second { get; set; }

    /// <summary>出战队伍。</summary>
    [JsonPropertyName("teams")]
    public List<HardChallengeAvatarDto>? Teams { get; set; }

    /// <summary>最佳输出角色（最强一击 / 最高总伤害）。</summary>
    [JsonPropertyName("best_avatar")]
    public List<HardChallengeBestAvatarDto>? BestAvatars { get; set; }

    /// <summary>怪物信息（对象，不是数组）。</summary>
    [JsonPropertyName("monster")]
    public HardChallengeMonsterDto? Monster { get; set; }
}

/// <summary>队伍角色。</summary>
public sealed class HardChallengeAvatarDto
{
    /// <summary>角色 ID。</summary>
    [JsonPropertyName("avatar_id")]
    public int? AvatarId { get; set; }

    /// <summary>角色名。</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>元素名（如 Pyro / Hydro，国际化字符串）。</summary>
    [JsonPropertyName("element")]
    public string? Element { get; set; }

    /// <summary>头像绝对 URL。</summary>
    [JsonPropertyName("image")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Image { get; set; }

    /// <summary>角色等级。</summary>
    [JsonPropertyName("level")]
    public int? Level { get; set; }

    /// <summary>稀有度（4 / 5）。</summary>
    [JsonPropertyName("rarity")]
    public int? Rarity { get; set; }

    /// <summary>命座数（接口字段名是 rank，实际含义为命座）。</summary>
    [JsonPropertyName("rank")]
    public int? Rank { get; set; }
}

/// <summary>最佳输出角色条目。</summary>
public sealed class HardChallengeBestAvatarDto
{
    /// <summary>角色 ID。</summary>
    [JsonPropertyName("avatar_id")]
    public int? AvatarId { get; set; }

    /// <summary>侧头像绝对 URL。</summary>
    [JsonPropertyName("side_icon")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? SideIcon { get; set; }

    /// <summary>数值字符串（最强一击伤害 / 最高总伤害）。</summary>
    [JsonPropertyName("dps")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Dps { get; set; }

    /// <summary>1 = 最强一击，2 = 最高总伤害。</summary>
    [JsonPropertyName("type")]
    public int? Type { get; set; }
}

/// <summary>怪物信息。</summary>
public sealed class HardChallengeMonsterDto
{
    /// <summary>怪物名。</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>怪物等级。</summary>
    [JsonPropertyName("level")]
    public int? Level { get; set; }

    /// <summary>怪物图标绝对 URL。</summary>
    [JsonPropertyName("icon")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Icon { get; set; }

    /// <summary>怪物描述（数组，元素含 &lt;color=#RRGGBBAA&gt; 富文本与空串占位）。</summary>
    [JsonPropertyName("desc")]
    public List<string>? Descriptions { get; set; }

    /// <summary>怪物标签（如「受到…伤害提升」）。</summary>
    [JsonPropertyName("tags")]
    public List<HardChallengeMonsterTagDto>? Tags { get; set; }

    /// <summary>怪物 ID。</summary>
    [JsonPropertyName("monster_id")]
    public int? MonsterId { get; set; }
}

/// <summary>怪物标签。</summary>
public sealed class HardChallengeMonsterTagDto
{
    /// <summary>标签类型。</summary>
    [JsonPropertyName("type")]
    public int? Type { get; set; }

    /// <summary>标签描述。</summary>
    [JsonPropertyName("desc")]
    public string? Description { get; set; }
}

/// <summary>上榜角色。</summary>
public sealed class HardChallengeBlingAvatarDto
{
    /// <summary>角色 ID。</summary>
    [JsonPropertyName("avatar_id")]
    public int? AvatarId { get; set; }

    /// <summary>角色名。</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>元素名。</summary>
    [JsonPropertyName("element")]
    public string? Element { get; set; }

    /// <summary>头像绝对 URL。</summary>
    [JsonPropertyName("image")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Image { get; set; }

    /// <summary>稀有度。</summary>
    [JsonPropertyName("rarity")]
    public int? Rarity { get; set; }

    /// <summary>是否最终上榜。</summary>
    [JsonPropertyName("is_plus")]
    public bool? IsPlus { get; set; }
}

/// <summary>官方链接（展示层不使用）。</summary>
public sealed class HardChallengeLinksDto
{
    /// <summary>阵容链接。</summary>
    [JsonPropertyName("lineup_link")]
    public string? LineupLink { get; set; }

    /// <summary>游玩链接。</summary>
    [JsonPropertyName("play_link")]
    public string? PlayLink { get; set; }
}

/// <summary>接口给出的日期时间对象（服务器时区，按原样展示，不进行时区换算）。</summary>
public sealed class HardChallengeDateTimeDto
{
    /// <summary>年。</summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>月。</summary>
    [JsonPropertyName("month")]
    public int? Month { get; set; }

    /// <summary>日。</summary>
    [JsonPropertyName("day")]
    public int? Day { get; set; }

    /// <summary>时。</summary>
    [JsonPropertyName("hour")]
    public int? Hour { get; set; }

    /// <summary>分。</summary>
    [JsonPropertyName("minute")]
    public int? Minute { get; set; }

    /// <summary>秒。</summary>
    [JsonPropertyName("second")]
    public int? Second { get; set; }
}
