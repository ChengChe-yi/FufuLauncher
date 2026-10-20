/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.CycleChallenge.Dto;

public sealed class SpiralAbyssDto
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


    [JsonPropertyName("end_time")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? EndTime
    {
        get;
        set;
    }


    [JsonPropertyName("total_battle_times")]
    public int TotalBattleTimes
    {
        get;
        set;
    }


    [JsonPropertyName("total_win_times")]
    public int TotalWinTimes
    {
        get;
        set;
    }


    [JsonPropertyName("max_floor")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? MaxFloor
    {
        get;
        set;
    }


    [JsonPropertyName("total_star")]
    public int TotalStar
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


    [JsonPropertyName("is_just_skipped_floor")]
    public bool IsJustSkippedFloor
    {
        get;
        set;
    }


    [JsonPropertyName("skipped_floor")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? SkippedFloor
    {
        get;
        set;
    }


    [JsonPropertyName("reveal_rank")]
    public List<SpiralAbyssRankDto>? RevealRank
    {
        get;
        set;
    }


    [JsonPropertyName("defeat_rank")]
    public List<SpiralAbyssRankDto>? DefeatRank
    {
        get;
        set;
    }


    [JsonPropertyName("damage_rank")]
    public List<SpiralAbyssRankDto>? DamageRank
    {
        get;
        set;
    }


    [JsonPropertyName("take_damage_rank")]
    public List<SpiralAbyssRankDto>? TakeDamageRank
    {
        get;
        set;
    }


    [JsonPropertyName("normal_skill_rank")]
    public List<SpiralAbyssRankDto>? NormalSkillRank
    {
        get;
        set;
    }


    [JsonPropertyName("energy_skill_rank")]
    public List<SpiralAbyssRankDto>? EnergySkillRank
    {
        get;
        set;
    }


    [JsonPropertyName("floors")]
    public List<SpiralAbyssFloorDto>? Floors
    {
        get;
        set;
    }
}

public sealed class SpiralAbyssRankDto
{
    [JsonPropertyName("avatar_id")]
    public int AvatarId
    {
        get;
        set;
    }


    [JsonPropertyName("avatar_icon")]
    public string? AvatarIcon
    {
        get;
        set;
    }


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

public sealed class SpiralAbyssFloorDto
{
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


    [JsonPropertyName("ley_line_disorder")]
    public List<string>? LeyLineDisorder
    {
        get;
        set;
    }


    [JsonPropertyName("ley_line_disorder_upper")]
    public List<string>? LeyLineDisorderUpper
    {
        get;
        set;
    }


    [JsonPropertyName("ley_line_disorder_lower")]
    public List<string>? LeyLineDisorderLower
    {
        get;
        set;
    }


    [JsonPropertyName("settle_date_time")]
    public JsonElement? SettleDateTime
    {
        get;
        set;
    }


    [JsonPropertyName("levels")]
    public List<SpiralAbyssLevelDto>? Levels
    {
        get;
        set;
    }
}

public sealed class SpiralAbyssLevelDto
{
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


    [JsonPropertyName("battles")]
    public List<SpiralAbyssBattleDto>? Battles
    {
        get;
        set;
    }


    [JsonPropertyName("top_half_floor_monster")]
    public JsonElement? TopHalfFloorMonster
    {
        get;
        set;
    }


    [JsonPropertyName("bottom_half_floor_monster")]
    public JsonElement? BottomHalfFloorMonster
    {
        get;
        set;
    }
}

public sealed class SpiralAbyssBattleDto
{
    [JsonPropertyName("index")]
    public int Index
    {
        get;
        set;
    }


    [JsonPropertyName("timestamp")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Timestamp
    {
        get;
        set;
    }


    [JsonPropertyName("settle_date_time")]
    public JsonElement? SettleDateTime
    {
        get;
        set;
    }


    [JsonPropertyName("avatars")]
    public List<SpiralAbyssAvatarDto>? Avatars
    {
        get;
        set;
    }
}

public sealed class SpiralAbyssAvatarDto
{
    [JsonPropertyName("id")]
    public int Id
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

public sealed class FlexibleStringConverter : JsonConverter<string>
{
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

                reader.Skip();
                return null;
        }
    }


    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

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
            else if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0 &&
                     data[0].ValueKind == JsonValueKind.Object)
            {
                root = data[0];
            }
            else
            {
                return null;
            }
        }

        return root.Deserialize<SpiralAbyssDto>(SerializerOptions);
    }
}