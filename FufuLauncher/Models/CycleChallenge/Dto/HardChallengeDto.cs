/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json.Serialization;

namespace FufuLauncher.Models.CycleChallenge.Dto;

public sealed class HardChallengeResponseDto
{
    [JsonPropertyName("data")]
    public List<HardChallengeEntryDto>? Data
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
    public HardChallengeLinksDto? Links
    {
        get;
        set;
    }
}

public sealed class HardChallengeEntryDto
{
    [JsonPropertyName("schedule")]
    public HardChallengeScheduleDto? Schedule
    {
        get;
        set;
    }


    [JsonPropertyName("single")]
    public HardChallengeModeDto? Single
    {
        get;
        set;
    }


    [JsonPropertyName("mp")]
    public HardChallengeModeDto? MultiPlayer
    {
        get;
        set;
    }


    [JsonPropertyName("blings")]
    public List<HardChallengeBlingAvatarDto>? Blings
    {
        get;
        set;
    }
}

public sealed class HardChallengeScheduleDto
{
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


    [JsonPropertyName("end_time")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? EndTime
    {
        get;
        set;
    }


    [JsonPropertyName("start_date_time")]
    public HardChallengeDateTimeDto? StartDateTime
    {
        get;
        set;
    }


    [JsonPropertyName("end_date_time")]
    public HardChallengeDateTimeDto? EndDateTime
    {
        get;
        set;
    }


    [JsonPropertyName("is_valid")]
    public bool? IsValid
    {
        get;
        set;
    }


    [JsonPropertyName("name")]
    public string? Name
    {
        get;
        set;
    }
}

public sealed class HardChallengeModeDto
{
    [JsonPropertyName("best")]
    public HardChallengeBestDto? Best
    {
        get;
        set;
    }


    [JsonPropertyName("challenge")]
    public List<HardChallengeChallengeDto>? Challenges
    {
        get;
        set;
    }


    [JsonPropertyName("has_data")]
    public bool? HasData
    {
        get;
        set;
    }
}

public sealed class HardChallengeBestDto
{
    [JsonPropertyName("difficulty")]
    public int? Difficulty
    {
        get;
        set;
    }

    /// <summary>最佳用时（秒）。</summary>
    [JsonPropertyName("second")]
    public int? Second
    {
        get;
        set;
    }

    /// <summary>难度徽章图标名，如 <c>UI_LeyLineChallenge_Medal_5</c>（可能形如 "A,B"）。</summary>
    [JsonPropertyName("icon")]
    public string? Icon
    {
        get;
        set;
    }
}

/// <summary>单个关卡。</summary>
public sealed class HardChallengeChallengeDto
{
    /// <summary>关卡（敌人）名称。</summary>
    [JsonPropertyName("name")]
    public string? Name
    {
        get;
        set;
    }

    /// <summary>关卡用时（秒）。</summary>
    [JsonPropertyName("second")]
    public int? Second
    {
        get;
        set;
    }

    /// <summary>出战队伍。</summary>
    [JsonPropertyName("teams")]
    public List<HardChallengeAvatarDto>? Teams
    {
        get;
        set;
    }

    /// <summary>最佳输出角色（最强一击 / 最高总伤害）。</summary>
    [JsonPropertyName("best_avatar")]
    public List<HardChallengeBestAvatarDto>? BestAvatars
    {
        get;
        set;
    }

    /// <summary>怪物信息（对象，不是数组）。</summary>
    [JsonPropertyName("monster")]
    public HardChallengeMonsterDto? Monster
    {
        get;
        set;
    }
}

/// <summary>队伍角色。</summary>
public sealed class HardChallengeAvatarDto
{
    /// <summary>角色 ID。</summary>
    [JsonPropertyName("avatar_id")]
    public int? AvatarId
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

    /// <summary>元素名（如 Pyro / Hydro，国际化字符串）。</summary>
    [JsonPropertyName("element")]
    public string? Element
    {
        get;
        set;
    }

    /// <summary>头像绝对 URL。</summary>
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

    /// <summary>命座数（接口字段名是 rank，实际含义为命座）。</summary>
    [JsonPropertyName("rank")]
    public int? Rank
    {
        get;
        set;
    }
}

/// <summary>最佳输出角色条目。</summary>
public sealed class HardChallengeBestAvatarDto
{
    /// <summary>角色 ID。</summary>
    [JsonPropertyName("avatar_id")]
    public int? AvatarId
    {
        get;
        set;
    }

    /// <summary>侧头像绝对 URL。</summary>
    [JsonPropertyName("side_icon")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? SideIcon
    {
        get;
        set;
    }

    /// <summary>数值字符串（最强一击伤害 / 最高总伤害）。</summary>
    [JsonPropertyName("dps")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Dps
    {
        get;
        set;
    }

    /// <summary>1 = 最强一击，2 = 最高总伤害。</summary>
    [JsonPropertyName("type")]
    public int? Type
    {
        get;
        set;
    }
}

/// <summary>怪物信息。</summary>
public sealed class HardChallengeMonsterDto
{
    /// <summary>怪物名。</summary>
    [JsonPropertyName("name")]
    public string? Name
    {
        get;
        set;
    }

    /// <summary>怪物等级。</summary>
    [JsonPropertyName("level")]
    public int? Level
    {
        get;
        set;
    }

    /// <summary>怪物图标绝对 URL。</summary>
    [JsonPropertyName("icon")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Icon
    {
        get;
        set;
    }

    /// <summary>怪物描述（数组，元素含 &lt;color=#RRGGBBAA&gt; 富文本与空串占位）。</summary>
    [JsonPropertyName("desc")]
    public List<string>? Descriptions
    {
        get;
        set;
    }

    /// <summary>怪物标签（如「受到…伤害提升」）。</summary>
    [JsonPropertyName("tags")]
    public List<HardChallengeMonsterTagDto>? Tags
    {
        get;
        set;
    }

    /// <summary>怪物 ID。</summary>
    [JsonPropertyName("monster_id")]
    public int? MonsterId
    {
        get;
        set;
    }
}

/// <summary>怪物标签。</summary>
public sealed class HardChallengeMonsterTagDto
{
    /// <summary>标签类型。</summary>
    [JsonPropertyName("type")]
    public int? Type
    {
        get;
        set;
    }

    /// <summary>标签描述。</summary>
    [JsonPropertyName("desc")]
    public string? Description
    {
        get;
        set;
    }
}

/// <summary>上榜角色。</summary>
public sealed class HardChallengeBlingAvatarDto
{
    /// <summary>角色 ID。</summary>
    [JsonPropertyName("avatar_id")]
    public int? AvatarId
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

    /// <summary>元素名。</summary>
    [JsonPropertyName("element")]
    public string? Element
    {
        get;
        set;
    }

    /// <summary>头像绝对 URL。</summary>
    [JsonPropertyName("image")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Image
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

    /// <summary>是否最终上榜。</summary>
    [JsonPropertyName("is_plus")]
    public bool? IsPlus
    {
        get;
        set;
    }
}

/// <summary>官方链接（展示层不使用）。</summary>
public sealed class HardChallengeLinksDto
{
    /// <summary>阵容链接。</summary>
    [JsonPropertyName("lineup_link")]
    public string? LineupLink
    {
        get;
        set;
    }

    /// <summary>游玩链接。</summary>
    [JsonPropertyName("play_link")]
    public string? PlayLink
    {
        get;
        set;
    }
}

/// <summary>接口给出的日期时间对象（服务器时区，按原样展示，不进行时区换算）。</summary>
public sealed class HardChallengeDateTimeDto
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