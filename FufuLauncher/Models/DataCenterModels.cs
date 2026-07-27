/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.DataCenter;

internal sealed class LenientDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.TryGetDouble(out var num) ? num : null;
            case JsonTokenType.String:
                var text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text)) return null;
                text = text.Trim().TrimEnd('%', ' ');
                return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : null;
            case JsonTokenType.True:
                return 1d;
            case JsonTokenType.False:
                return 0d;
            case JsonTokenType.Null:
                return null;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteNumberValue(value.Value);
        else writer.WriteNullValue();
    }
}

internal sealed class LenientIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var i)) return i;
                return reader.TryGetDouble(out var d) ? (int)Math.Round(d) : null;
            case JsonTokenType.String:
                var text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text)) return null;
                return double.TryParse(text.Trim().TrimEnd('%', ' '), NumberStyles.Any, CultureInfo.InvariantCulture,
                    out var parsed)
                    ? (int)Math.Round(parsed)
                    : null;
            case JsonTokenType.True:
                return 1;
            case JsonTokenType.False:
                return 0;
            case JsonTokenType.Null:
                return null;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteNumberValue(value.Value);
        else writer.WriteNullValue();
    }
}

internal sealed class LenientStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                return reader.TryGetInt64(out var l)
                    ? l.ToString(CultureInfo.InvariantCulture)
                    : reader.GetDouble().ToString(CultureInfo.InvariantCulture);
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

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

public static class DataCenterJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters =
        {
            new LenientDoubleConverter(),
            new LenientIntConverter()
        }
    };
}

public sealed class RoleAvgResponse
{
    [JsonPropertyName("code")] public int? Code { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("data_from")] public string? DataFrom { get; set; }
    [JsonPropertyName("last_update")] public string? LastUpdate { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("result")] public List<RoleAvgEntry>? Result { get; set; }
}

public sealed class RoleAvgEntry
{
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("ename")] public string? Ename { get; set; }
    [JsonPropertyName("role_sum")] public double? RoleSum { get; set; }
    [JsonPropertyName("star")] public int? Star { get; set; }
    [JsonPropertyName("avatar")] public string? Avatar { get; set; }
    [JsonPropertyName("avg_level")] public double? AvgLevel { get; set; }
    [JsonPropertyName("avg_class")] public double? AvgConstellation { get; set; }
    [JsonPropertyName("ability1")] public double? Ability1 { get; set; }
    [JsonPropertyName("ability2")] public double? Ability2 { get; set; }
    [JsonPropertyName("ability3")] public double? Ability3 { get; set; }
    [JsonPropertyName("c0")] public double? C0 { get; set; }
    [JsonPropertyName("c1")] public double? C1 { get; set; }
    [JsonPropertyName("c2")] public double? C2 { get; set; }
    [JsonPropertyName("c3")] public double? C3 { get; set; }
    [JsonPropertyName("c4")] public double? C4 { get; set; }
    [JsonPropertyName("c5")] public double? C5 { get; set; }
    [JsonPropertyName("c6")] public double? C6 { get; set; }
    [JsonPropertyName("artifacts")] public double? ArtifactScore { get; set; }
    [JsonPropertyName("damage")] public double? Damage { get; set; }
    [JsonPropertyName("damage_name")] public string? DamageName { get; set; }
    [JsonPropertyName("weapon")] public List<WeaponUsageEntry>? Weapons { get; set; }
    [JsonPropertyName("artifacts_set")] public List<ArtifactUsageEntry>? ArtifactSets { get; set; }
}

public sealed class WeaponUsageEntry
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("avatar")] public string? Avatar { get; set; }
    [JsonPropertyName("rate")] public double? Rate { get; set; }
}

public sealed class ArtifactUsageEntry
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("avatars")] public List<string>? Avatars { get; set; }
    [JsonPropertyName("rate")] public double? Rate { get; set; }
}

public sealed class AbyssStatsResponse
{
    [JsonPropertyName("code")] public int? Code { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("now_version")] public string? NowVersionLabel { get; set; }
    [JsonPropertyName("old_version")] public string? OldVersionLabel { get; set; }
    [JsonPropertyName("last_update")] public string? LastUpdate { get; set; }
    [JsonPropertyName("update")] public string? UpdateInfo { get; set; }
    [JsonPropertyName("top_own")] public double? SampleCount { get; set; }
    [JsonPropertyName("tips")] public string? Tips { get; set; }
    [JsonPropertyName("tips2")] public string? Tips2 { get; set; }
    [JsonPropertyName("star36_rate")] public string? FullStarRate { get; set; }
    [JsonPropertyName("star36_once_rate")] public string? FullStarOnceRate { get; set; }
    [JsonPropertyName("restart_times_avg")] public double? RestartTimesAvg { get; set; }
    [JsonPropertyName("nandu")] public double? Difficulty { get; set; }
    [JsonPropertyName("select_list")] public List<AbyssOption>? SelectList { get; set; }
    [JsonPropertyName("history_list")] public List<AbyssOption>? HistoryList { get; set; }
    [JsonPropertyName("has_list")] public List<AbyssCharacterEntry>? HasList { get; set; }
    [JsonPropertyName("restart_info")] public List<AbyssRestartEntry>? RestartInfo { get; set; }
    [JsonPropertyName("result")] public JsonElement? ResultRaw { get; set; }
}

public sealed class AbyssOption
{
    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("value")]
    [JsonConverter(typeof(LenientStringConverter))]
    public string? Value { get; set; }
}

public sealed class AbyssCharacterEntry
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("star")] public int? Star { get; set; }
    [JsonPropertyName("avatar")] public string? Avatar { get; set; }
    [JsonPropertyName("use")] public double? UseCount { get; set; }
    [JsonPropertyName("own")] public double? OwnCount { get; set; }
    [JsonPropertyName("use_rate")] public double? UseRate { get; set; }
    [JsonPropertyName("own_rate")] public double? OwnRate { get; set; }
    [JsonPropertyName("collection")] public double? AvgConstellation { get; set; }
    [JsonPropertyName("time")] public double? ClearTime { get; set; }
    [JsonPropertyName("rank_class")] public string? RankClass { get; set; }
}

public sealed class AbyssTierGroup
{
    [JsonPropertyName("rank_name")] public string? RankName { get; set; }
    [JsonPropertyName("rank_class")] public string? RankClass { get; set; }
    [JsonPropertyName("list")] public List<AbyssTierEntry>? List { get; set; }
}

public sealed class AbyssTierEntry
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("ename")] public string? Ename { get; set; }
    [JsonPropertyName("star")] public int? Star { get; set; }
    [JsonPropertyName("avatar")] public string? Avatar { get; set; }
    [JsonPropertyName("use")] public double? UseCount { get; set; }
    [JsonPropertyName("own")] public double? OwnCount { get; set; }
    [JsonPropertyName("use_rate")] public double? UseRate { get; set; }
    [JsonPropertyName("own_rate")] public double? OwnRate { get; set; }
    [JsonPropertyName("collection")] public double? AvgConstellation { get; set; }
    [JsonPropertyName("time")] public double? ClearTime { get; set; }
    [JsonPropertyName("c0_rate")] public double? C0Rate { get; set; }
    [JsonPropertyName("c1_rate")] public double? C1Rate { get; set; }
    [JsonPropertyName("c2_rate")] public double? C2Rate { get; set; }
    [JsonPropertyName("c3_rate")] public double? C3Rate { get; set; }
    [JsonPropertyName("c4_rate")] public double? C4Rate { get; set; }
    [JsonPropertyName("c5_rate")] public double? C5Rate { get; set; }
    [JsonPropertyName("c6_rate")] public double? C6Rate { get; set; }
}

public sealed class AbyssRankEntry
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("star")] public int? Star { get; set; }
    [JsonPropertyName("avatar")] public string? Avatar { get; set; }
    [JsonPropertyName("use")] public double? UseCount { get; set; }
    [JsonPropertyName("own")] public double? OwnCount { get; set; }
    [JsonPropertyName("use_rate")] public double? UseRate { get; set; }
    [JsonPropertyName("own_rate")] public double? OwnRate { get; set; }
    [JsonPropertyName("collection")] public double? AvgConstellation { get; set; }
    [JsonPropertyName("time")] public double? ClearTime { get; set; }
    [JsonPropertyName("rank_class")] public string? RankClass { get; set; }
    [JsonPropertyName("use_rate_old")] public double? UseRateOld { get; set; }
    [JsonPropertyName("use_rate_change")] public double? UseRateChange { get; set; }
}

public sealed class AbyssTeamEntry
{
    [JsonPropertyName("role")] public List<AbyssTeamMember>? Members { get; set; }
    [JsonPropertyName("use")] public double? UseCount { get; set; }
    [JsonPropertyName("use_rate")] public double? UseRate { get; set; }
    [JsonPropertyName("has")] public double? HasCount { get; set; }
    [JsonPropertyName("has_rate")] public double? HasRate { get; set; }
    [JsonPropertyName("attend_rate")] public double? AttendRate { get; set; }
    [JsonPropertyName("time")] public double? ClearTime { get; set; }
    [JsonPropertyName("up_use")] public double? FirstHalfRate { get; set; }
    [JsonPropertyName("mid_use")] public double? MidHalfRate { get; set; }
    [JsonPropertyName("down_use")] public double? SecondHalfRate { get; set; }
    [JsonPropertyName("up_use_num")] public double? FirstHalfCount { get; set; }
    [JsonPropertyName("mid_use_num")] public double? MidHalfCount { get; set; }
    [JsonPropertyName("down_use_num")] public double? SecondHalfCount { get; set; }
}

public sealed class AbyssTeamMember
{
    [JsonPropertyName("avatar")] public string? Avatar { get; set; }
    [JsonPropertyName("star")] public int? Star { get; set; }
}

public sealed class AbyssRestartEntry
{
    [JsonPropertyName("intro")] public string? Intro { get; set; }
    [JsonPropertyName("rate")] public double? Rate { get; set; }
    [JsonPropertyName("width")] public double? Width { get; set; }
}

public sealed class AbyssStatsBundle
{
    public AbyssStatsResponse Response { get; }
    public List<AbyssTierGroup> Tiers { get; }
    public List<AbyssRankEntry> Ranks { get; }
    public List<AbyssTeamEntry> Teams { get; }
    public Dictionary<string, AbyssCharacterEntry> ByAvatar { get; }

    public Dictionary<string, AbyssCharacterEntry> ByName { get; }

    public AbyssStatsBundle(AbyssStatsResponse response)
    {
        Response = response;
        Tiers = new List<AbyssTierGroup>();
        Ranks = new List<AbyssRankEntry>();
        Teams = new List<AbyssTeamEntry>();
        ByAvatar = new Dictionary<string, AbyssCharacterEntry>(StringComparer.OrdinalIgnoreCase);
        ByName = new Dictionary<string, AbyssCharacterEntry>(StringComparer.Ordinal);

        foreach (var entry in response.HasList ?? new List<AbyssCharacterEntry>())
        {
            if (!string.IsNullOrEmpty(entry.Avatar)) ByAvatar[entry.Avatar!] = entry;
            if (!string.IsNullOrEmpty(entry.Name)) ByName[entry.Name!] = entry;
        }

        Split(response.ResultRaw);
    }
    
    private void Split(JsonElement? raw)
    {
        if (raw is not { ValueKind: JsonValueKind.Array } root) return;

        foreach (var child in root.EnumerateArray())
        {
            if (child.ValueKind != JsonValueKind.Array) continue;

            JsonElement first = default;
            var hasFirst = false;
            foreach (var item in child.EnumerateArray())
            {
                first = item;
                hasFirst = true;
                break;
            }

            if (!hasFirst || first.ValueKind != JsonValueKind.Object) continue;

            if (first.TryGetProperty("rank_name", out _))
            {
                if (Tiers.Count > 0) continue;
                var tiers = child.Deserialize<List<AbyssTierGroup>>(DataCenterJson.Options);
                if (tiers != null) Tiers.AddRange(tiers);
            }
            else if (first.TryGetProperty("use_rate_change", out _))
            {
                if (Ranks.Count > 0) continue;
                var ranks = child.Deserialize<List<AbyssRankEntry>>(DataCenterJson.Options);
                if (ranks != null) Ranks.AddRange(ranks);
            }
            else if (first.TryGetProperty("role", out var roleProp) && roleProp.ValueKind == JsonValueKind.Array)
            {
                var teams = child.Deserialize<List<AbyssTeamEntry>>(DataCenterJson.Options);
                if (teams != null && teams.Count > Teams.Count)
                {
                    Teams.Clear();
                    Teams.AddRange(teams);
                }
            }
        }
        
        if (Ranks.Count == 0 && Response.HasList is { Count: > 0 } fallback)
        {
            foreach (var entry in fallback)
            {
                Ranks.Add(new AbyssRankEntry
                {
                    Name = entry.Name,
                    Star = entry.Star,
                    Avatar = entry.Avatar,
                    UseCount = entry.UseCount,
                    OwnCount = entry.OwnCount,
                    UseRate = entry.UseRate,
                    OwnRate = entry.OwnRate,
                    AvgConstellation = entry.AvgConstellation,
                    ClearTime = entry.ClearTime,
                    RankClass = entry.RankClass
                });
            }
        }
    }

    public string? ResolveName(string? avatar)
        => !string.IsNullOrEmpty(avatar) && ByAvatar.TryGetValue(avatar!, out var entry) ? entry.Name : null;
}

public sealed class WishHistoryResponse
{
    [JsonPropertyName("code")] public int? Code { get; set; }
    [JsonPropertyName("result")] public List<WishBannerEntry>? Characters { get; set; }
    [JsonPropertyName("weapon")] public List<WishBannerEntry>? Weapons { get; set; }
    [JsonPropertyName("avatar_list")] public Dictionary<string, string>? AvatarList { get; set; }
}

public sealed class WishBannerEntry
{
    [JsonPropertyName("avatar")] public string? Avatar { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("time")] public string? Time { get; set; }
    [JsonPropertyName("star5_role")] public List<string>? Star5 { get; set; }
    [JsonPropertyName("star4_role")] public List<string>? Star4 { get; set; }
}

public sealed class RerunResponse
{
    [JsonPropertyName("code")] public int? Code { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("result")] public List<List<RerunEntry>>? Result { get; set; }
}

public sealed class RerunEntry
{
    [JsonPropertyName("role")] public string? Name { get; set; }
    [JsonPropertyName("avatar")] public string? Avatar { get; set; }
    [JsonPropertyName("star")] public int? Star { get; set; }
    [JsonPropertyName("days")] public double? Days { get; set; }
    [JsonPropertyName("intro")] public string? Intro { get; set; }
    [JsonPropertyName("avg_days")] public double? AvgDays { get; set; }
    [JsonPropertyName("up_times")] public int? UpTimes { get; set; }
    [JsonPropertyName("history")] public List<string>? History { get; set; }
    [JsonPropertyName("tags")] public string? Tags { get; set; }
    [JsonPropertyName("max_gap_days")] public double? MaxGapDays { get; set; }
    [JsonPropertyName("max_gap_pool")] public string? MaxGapPool { get; set; }
    [JsonPropertyName("min_gap_days")] public double? MinGapDays { get; set; }
    [JsonPropertyName("min_gap_pool")] public string? MinGapPool { get; set; }
    [JsonPropertyName("width_rate")] public double? WidthRate { get; set; }
}
