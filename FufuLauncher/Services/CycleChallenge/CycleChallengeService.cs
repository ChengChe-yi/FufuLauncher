/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using FufuLauncher.Data.Entities;
using FufuLauncher.Data.Repositories;
using FufuLauncher.Helpers;
using FufuLauncher.Models.CycleChallenge;
using FufuLauncher.Services;

namespace FufuLauncher.Services.CycleChallenge;

/// <summary>
/// 周期挑战编排：请求 API → 解析期次/摘要 → 同键覆盖入库；失败时回退本地缓存。
/// 存储唯一键 (Uid, Mode, ScheduleId)，Payload 为单期 JSON（剧诗/危战为 data[] 中的一项）。
/// </summary>
public sealed class CycleChallengeService
{
    private const string ApiVersion = "cn-2.112.0/os-2.54.0";

    private readonly GameRecordApiClient _api;
    private readonly CycleChallengeRepository _repository;

    public CycleChallengeService(GameRecordApiClient api, CycleChallengeRepository repository)
    {
        _api = api;
        _repository = repository;
    }

    /// <summary>从本地缓存读取（不做网络请求）。</summary>
    public ChallengeLoadResult LoadFromCache(ChallengeMode mode, SelectedGameRole role, string? scheduleId)
    {
        var periods = _repository.GetPeriods(role.Role.game_uid, mode);
        var record = scheduleId is null
            ? PickPreferredRecord(role.Role.game_uid, mode, periods)
            : _repository.Get(role.Role.game_uid, mode, scheduleId);

        if (record is null)
        {
            return new ChallengeLoadResult
            {
                Success = false,
                Kind = periods.Count == 0 ? ChallengeErrorKind.NotParticipated : ChallengeErrorKind.ServerError,
                Message = periods.Count == 0
                    ? "CycleChallenge_NotParticipated".GetLocalized()
                    : "CycleChallenge_NoCachedPeriod".GetLocalized(),
                Periods = periods,
            };
        }

        return new ChallengeLoadResult
        {
            Success = true,
            Kind = ChallengeErrorKind.Success,
            Periods = periods,
            Payload = WrapForPanel(mode, record),
            ActiveScheduleId = record.ScheduleId,
            IsFromCache = true,
            FetchedAt = DateTimeOffset.TryParse(record.FetchedAt, out var fetched) ? fetched : null,
        };
    }

    /// <summary>请求 API 并覆盖写入；失败回退缓存。深境螺旋需要 schedule_type=1/2 两次请求。</summary>
    public async Task<ChallengeLoadResult> RefreshAsync(
        ChallengeMode mode, SelectedGameRole role, CancellationToken token)
    {
        var periodsBefore = _repository.GetPeriods(role.Role.game_uid, mode);
        var timeContext = CycleChallengeTime.CreateContext(role.Role.region);
        var failures = new List<RawChallengeResponse>();

        switch (mode)
        {
            case ChallengeMode.SpiralAbyss:
                for (var schedule = 1; schedule <= 2; schedule++)
                {
                    var response = await _api.GetSpiralAbyssAsync(role, schedule, token).ConfigureAwait(false);
                    if (response.Success)
                    {
                        SaveSinglePeriod(mode, role, response.DataJson!, token);
                    }
                    else if (response.Kind is not ChallengeErrorKind.NotParticipated)
                    {
                        failures.Add(response);
                    }
                }

                break;

            case ChallengeMode.Theater:
            {
                var response = await _api.GetRoleCombatAsync(role, token).ConfigureAwait(false);
                if (response.Success)
                {
                    SavePeriodArray(mode, role, response.DataJson!, ReadIsUnlock(response.RawJson), timeContext);
                }
                else
                {
                    failures.Add(response);
                }

                break;
            }

            case ChallengeMode.Stygian:
            {
                var response = await _api.GetHardChallengeAsync(role, token).ConfigureAwait(false);
                if (response.Success)
                {
                    SavePeriodArray(mode, role, response.DataJson!, ReadIsUnlock(response.RawJson), timeContext);
                }
                else
                {
                    failures.Add(response);
                }

                break;
            }
        }

        var periods = _repository.GetPeriods(role.Role.game_uid, mode);
        var latest = PickPreferredRecord(role.Role.game_uid, mode, periods);

        if (latest is null)
        {
            var firstFailure = failures.FirstOrDefault();
            return new ChallengeLoadResult
            {
                Success = false,
                Kind = firstFailure?.Kind ?? ChallengeErrorKind.NotParticipated,
                Message = firstFailure?.Message,
                Periods = periods,
            };
        }

        // 有失败但仍有缓存：标记为缓存态 + 携带失败原因
        var flagCache = failures.Count > 0 && periodsBefore.Count > 0;
        return new ChallengeLoadResult
        {
            Success = true,
            Kind = flagCache ? failures[0].Kind : ChallengeErrorKind.Success,
            Message = flagCache ? failures[0].Message : null,
            Periods = periods,
            Payload = WrapForPanel(mode, latest),
            ActiveScheduleId = latest.ScheduleId,
            IsFromCache = flagCache,
            FetchedAt = DateTimeOffset.TryParse(latest.FetchedAt, out var fetched) ? fetched : null,
        };
    }

    private void SaveSinglePeriod(ChallengeMode mode, SelectedGameRole role, string dataJson, CancellationToken token)
    {
        using var document = JsonDocument.Parse(dataJson);
        var element = document.RootElement.Clone();
        if (element.ValueKind is not JsonValueKind.Object)
        {
            return;
        }

        var scheduleId = GetString(element, "schedule_id");
        if (string.IsNullOrEmpty(scheduleId))
        {
            return;
        }

        var start = NormalizeTime(element, "start_time");
        var end = NormalizeTime(element, "end_time");
        var summary = new JsonObject
        {
            ["total_star"] = GetInt(element, "total_star"),
            ["max_floor"] = GetString(element, "max_floor"),
            ["total_battle_times"] = GetInt(element, "total_battle_times"),
            ["total_win_times"] = GetInt(element, "total_win_times"),
            ["is_unlock"] = GetBool(element, "is_unlock"),
            ["has_data"] = element.TryGetProperty("floors", out var floors) &&
                           floors.ValueKind is JsonValueKind.Array &&
                           floors.GetArrayLength() > 0,
        };

        Upsert(mode, role, scheduleId, start, end, summary.ToJsonString(), dataJson);
    }

    private void SavePeriodArray(
        ChallengeMode mode,
        SelectedGameRole role,
        string dataJson,
        bool isUnlock,
        CycleChallengeTimeContext timeContext)
    {
        using var document = JsonDocument.Parse(dataJson);
        var root = document.RootElement;
        JsonElement array;
        bool unlock = isUnlock;

        if (root.ValueKind is JsonValueKind.Array)
        {
            array = root;
        }
        else if (root.ValueKind is JsonValueKind.Object &&
                 root.TryGetProperty("data", out var inner) &&
                 inner.ValueKind is JsonValueKind.Array)
        {
            // 剧诗/危战响应形如 { retcode, message, data: { data: [...], is_unlock, links } }
            array = inner;
            if (root.TryGetProperty("is_unlock", out _))
            {
                unlock = GetBool(root, "is_unlock");
            }
        }
        else
        {
            return;
        }

        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind is not JsonValueKind.Object)
            {
                continue;
            }

            var scheduleElement = element.TryGetProperty("schedule", out var schedule) &&
                                  schedule.ValueKind is JsonValueKind.Object
                ? schedule
                : element;

            var scheduleId = GetString(scheduleElement, "schedule_id");
            if (string.IsNullOrEmpty(scheduleId))
            {
                continue;
            }

            var start = NormalizeScheduleTime(scheduleElement, "start_time", "start_date_time", timeContext);
            var end = NormalizeScheduleTime(scheduleElement, "end_time", "end_date_time", timeContext);

            JsonObject summary;
            if (mode == ChallengeMode.Theater)
            {
                var stat = element.TryGetProperty("stat", out var statElement) &&
                           statElement.ValueKind is JsonValueKind.Object
                    ? statElement
                    : default;
                summary = new JsonObject
                {
                    ["difficulty_id"] = SafeInt(stat, "difficulty_id"),
                    ["max_round_id"] = SafeInt(stat, "max_round_id"),
                    ["heraldry"] = SafeInt(stat, "heraldry"),
                    ["medal_num"] = SafeInt(stat, "medal_num"),
                    ["has_data"] = GetBool(element, "has_data"),
                    ["is_unlock"] = unlock,
                };
            }
            else
            {
                var single = element.TryGetProperty("single", out var singleElement) &&
                             singleElement.ValueKind is JsonValueKind.Object
                    ? singleElement
                    : default;
                var best = single.ValueKind is JsonValueKind.Object &&
                           single.TryGetProperty("best", out var bestElement) &&
                           bestElement.ValueKind is JsonValueKind.Object
                    ? bestElement
                    : default;
                summary = new JsonObject
                {
                    ["difficulty"] = SafeInt(best, "difficulty"),
                    ["second"] = SafeInt(best, "second"),
                    ["has_data"] = single.ValueKind is JsonValueKind.Object && GetBool(single, "has_data"),
                    ["name"] = GetString(scheduleElement, "name"),
                    ["is_unlock"] = unlock,
                };
            }

            Upsert(mode, role, scheduleId, start, end, summary.ToJsonString(), element.GetRawText());
        }
    }

    private void Upsert(
        ChallengeMode mode,
        SelectedGameRole role,
        string scheduleId,
        string? start,
        string? end,
        string summary,
        string payload)
    {
        _repository.Upsert(new CycleChallengeRecordEntity
        {
            Uid = role.Role.game_uid,
            Mode = (int)mode,
            ScheduleId = scheduleId,
            AccountId = role.AccountId,
            StartUtc = start,
            EndUtc = end,
            Summary = summary,
            Payload = payload,
            FetchedAt = DateTimeOffset.UtcNow.ToString("O"),
            ApiVersion = ApiVersion,
        });
    }

    /// <summary>
    /// 剧诗/危战面板按 data[] 结构解析，因此包一层 retcode/message/data，并补回响应根上的 is_unlock；
    /// 深境螺旋面板直接接收 data 对象（is_unlock 已在 data 内部）。
    /// </summary>
    private static string WrapForPanel(ChallengeMode mode, CycleChallengeRecordEntity record)
    {
        if (mode == ChallengeMode.SpiralAbyss)
        {
            return record.Payload;
        }

        var isUnlock = ReadSummaryBool(record.Summary, "is_unlock");
        return $"{{\"retcode\":0,\"message\":\"OK\",\"is_unlock\":{isUnlock.ToString().ToLowerInvariant()},\"data\":[{record.Payload}]}}";
    }

    /// <summary>从完整响应 JSON 的根节点读取 is_unlock（剧诗/危战不在 data 内）。</summary>
    private static bool ReadIsUnlock(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;

            // 剧诗/危战：is_unlock 在同一层（data 对象内）；深渊：在 data 对象内
            if (root.TryGetProperty("is_unlock", out _))
            {
                return GetBool(root, "is_unlock");
            }

            if (root.TryGetProperty("data", out var data) &&
                data.ValueKind is JsonValueKind.Object &&
                data.TryGetProperty("is_unlock", out _))
            {
                return GetBool(data, "is_unlock");
            }

            // 字段缺失默认视为已解锁，避免把正常数据误判为未解锁
            return true;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    /// <summary>
    /// 选择默认展示的期次：优先最近一期“有数据”的期次（当前期可能尚未参与），
    /// 全部无数据时退回最新一期。期次仍可在下拉框中手动切换。
    /// </summary>
    private CycleChallengeRecordEntity? PickPreferredRecord(
        string uid, ChallengeMode mode, IReadOnlyList<ChallengePeriod> periods)
    {
        foreach (var period in periods)
        {
            var candidate = _repository.Get(uid, mode, period.ScheduleId);
            if (candidate is not null && ReadSummaryBool(candidate.Summary, "has_data"))
            {
                return candidate;
            }
        }

        return periods.Count > 0 ? _repository.Get(uid, mode, periods[0].ScheduleId) : null;
    }

    /// <summary>从摘要 JSON 读取布尔字段，缺失时默认 true（避免把正常数据误判为未解锁）。</summary>
    private static bool ReadSummaryBool(string? summaryJson, string property)
    {
        if (string.IsNullOrWhiteSpace(summaryJson))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(summaryJson);
            return !document.RootElement.TryGetProperty(property, out _) ||
                   GetBool(document.RootElement, property);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    /// <summary>把接口时间规范化为带 +00:00 的 UTC 字符串入库（展示时再按账号服务器时区换算）。</summary>
    private static string? NormalizeTime(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var unix) =>
                CycleChallengeTime.ToUtcStorageString(DateTimeOffset.FromUnixTimeSeconds(unix)),
            JsonValueKind.String => NormalizeTimeString(value.GetString()),
            _ => null,
        };
    }

    private static string? NormalizeScheduleTime(
        JsonElement element, string timeProperty, string dateTimeProperty, CycleChallengeTimeContext context)
    {
        // 优先 unix 秒（绝对时间）；其次接口的墙上时间对象（按服务器时区解释）
        if (element.TryGetProperty(timeProperty, out var timeValue) &&
            timeValue.ValueKind is JsonValueKind.String &&
            long.TryParse(timeValue.GetString(), out var unix) && unix > 0)
        {
            return CycleChallengeTime.ToUtcStorageString(DateTimeOffset.FromUnixTimeSeconds(unix));
        }

        if (element.TryGetProperty(dateTimeProperty, out var dateTime) &&
            dateTime.ValueKind is JsonValueKind.Object)
        {
            var year = GetInt(dateTime, "year");
            var month = GetInt(dateTime, "month");
            var day = GetInt(dateTime, "day");
            var hour = GetInt(dateTime, "hour");
            var minute = GetInt(dateTime, "minute");
            var second = GetInt(dateTime, "second");
            if (year > 0 && month > 0 && day > 0)
            {
                var wallClock = new DateTime(
                    year,
                    Math.Clamp(month, 1, 12),
                    Math.Clamp(day, 1, 31),
                    Math.Clamp(hour, 0, 23),
                    Math.Clamp(minute, 0, 59),
                    Math.Clamp(second, 0, 59),
                    DateTimeKind.Unspecified);
                return CycleChallengeTime.ToUtcStorageString(
                    CycleChallengeTime.WallClockToDisplay(wallClock, context));
            }
        }

        return NormalizeTime(element, timeProperty);
    }

    private static string? NormalizeTimeString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (long.TryParse(text, out var unix))
        {
            return CycleChallengeTime.ToUtcStorageString(DateTimeOffset.FromUnixTimeSeconds(unix));
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? CycleChallengeTime.ToUtcStorageString(parsed)
            : null;
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.GetRawText(),
                _ => string.Empty,
            }
            : string.Empty;

    private static int GetInt(JsonElement element, string property) => SafeInt(element, property);

    private static int SafeInt(JsonElement element, string property)
    {
        if (element.ValueKind is not JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0,
        };
    }

    private static bool GetBool(JsonElement element, string property)
    {
        if (element.ValueKind is not JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => false,
        };
    }
}
