/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
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
        var saveFailed = false;
        RawChallengeResponse? preferredAbyssResponse = null;
        string? freshPanelPayload = null;
        string? freshScheduleId = null;

        switch (mode)
        {
            case ChallengeMode.SpiralAbyss:
                for (var schedule = 1; schedule <= 2; schedule++)
                {
                    var response = await _api.GetSpiralAbyssAsync(role, schedule, token).ConfigureAwait(false);
                    if (response.Success)
                    {
                        // 深渊会请求 schedule_type=1/2 两期：优先保留“有楼层数据”的那一期
                        if (preferredAbyssResponse is null
                            || (!HasAbyssData(preferredAbyssResponse.DataJson)
                                && HasAbyssData(response.DataJson)))
                        {
                            preferredAbyssResponse = response;
                        }

                        try
                        {
                            SaveSinglePeriod(mode, role, response.DataJson!, token);
                        }
                        catch (Exception ex)
                        {
                            // 写缓存失败不能让成功的 API 响应变成错误页
                            Debug.WriteLine($"[CycleChallengeService] 深渊写入缓存失败: {ex.Message}");
                            saveFailed = true;
                            freshPanelPayload ??= BuildFreshPanelPayload(mode, response.DataJson, isUnlock: true);
                            freshScheduleId ??= TryGetFreshScheduleId(mode, response.DataJson);
                        }
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
                    var isUnlock = ReadIsUnlock(response.RawJson);
                    try
                    {
                        SavePeriodArray(mode, role, response.DataJson!, isUnlock, timeContext);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CycleChallengeService] 剧诗写入缓存失败: {ex.Message}");
                        saveFailed = true;
                        freshPanelPayload ??= BuildFreshPanelPayload(mode, response.DataJson, isUnlock);
                        freshScheduleId ??= TryGetFreshScheduleId(mode, response.DataJson);
                    }
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
                    var isUnlock = ReadIsUnlock(response.RawJson);
                    try
                    {
                        SavePeriodArray(mode, role, response.DataJson!, isUnlock, timeContext);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CycleChallengeService] 危战写入缓存失败: {ex.Message}");
                        saveFailed = true;
                        freshPanelPayload ??= BuildFreshPanelPayload(mode, response.DataJson, isUnlock);
                        freshScheduleId ??= TryGetFreshScheduleId(mode, response.DataJson);
                    }
                }
                else
                {
                    failures.Add(response);
                }

                break;
            }
        }

        // 深渊：两次请求中优先使用“有楼层数据”的响应作为写缓存失败的兜底 payload
        if (mode == ChallengeMode.SpiralAbyss
            && saveFailed
            && preferredAbyssResponse is not null)
        {
            freshPanelPayload = BuildFreshPanelPayload(
                mode, preferredAbyssResponse.DataJson, isUnlock: true);
            freshScheduleId = TryGetFreshScheduleId(mode, preferredAbyssResponse.DataJson);
        }

        IReadOnlyList<ChallengePeriod> periods = periodsBefore;
        CycleChallengeRecordEntity? latest = null;
        try
        {
            periods = _repository.GetPeriods(role.Role.game_uid, mode);
            latest = PickPreferredRecord(role.Role.game_uid, mode, periods);
        }
        catch (Exception ex) when (saveFailed && freshPanelPayload is not null)
        {
            // API 已成功、只是缓存不可读：保留旧期次列表并让面板直接使用本次新数据
            Debug.WriteLine($"[CycleChallengeService] 读取缓存失败: {ex.Message}");
            periods = periodsBefore;
            latest = null;
        }

        // 写缓存失败但 API 成功：直接返回本次拉取的新数据，避免误报错误或展示旧缓存
        if (saveFailed && freshPanelPayload is not null)
        {
            // 期次标签必须与本次新 payload 一致；若缓存期次列表里还没有该期次，补一个仅含 ID 的项
            string activeScheduleId = freshScheduleId ?? latest?.ScheduleId ?? string.Empty;
            periods = EnsureFreshPeriodPresent(periods, role, mode, activeScheduleId);

            return new ChallengeLoadResult
            {
                Success = true,
                Kind = ChallengeErrorKind.Success,
                Periods = periods,
                Payload = freshPanelPayload,
                ActiveScheduleId = activeScheduleId,
                IsFromCache = false,
            };
        }

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
    /// 写缓存失败时，把本次 API 的 data 节点转换为面板可直接解析的 payload：
    /// 深渊 data 本身就是对象；剧诗/危战需要补回 retcode/message/is_unlock 外壳。
    /// </summary>
    private static string? BuildFreshPanelPayload(ChallengeMode mode, string? dataJson, bool isUnlock)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        if (mode == ChallengeMode.SpiralAbyss)
        {
            return dataJson;
        }

        try
        {
            using var document = JsonDocument.Parse(dataJson);
            var root = document.RootElement;
            JsonElement array;
            if (root.ValueKind is JsonValueKind.Array)
            {
                array = root;
            }
            else if (root.ValueKind is JsonValueKind.Object
                     && root.TryGetProperty("data", out var inner)
                     && inner.ValueKind is JsonValueKind.Array)
            {
                array = inner;
            }
            else
            {
                return null;
            }

            var unlock = isUnlock ? "true" : "false";
            return $"{{\"retcode\":0,\"message\":\"OK\",\"is_unlock\":{unlock},\"data\":{array.GetRawText()}}}";
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[CycleChallengeService] 构造临时 payload 失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>深渊 data 中是否包含非空 floors（用于在两个请求结果里挑选有数据的一期）。</summary>
    private static bool HasAbyssData(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(dataJson);
            var root = document.RootElement;
            return root.ValueKind is JsonValueKind.Object
                   && root.TryGetProperty("floors", out var floors)
                   && floors.ValueKind is JsonValueKind.Array
                   && floors.GetArrayLength() > 0;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[CycleChallengeService] 判断深渊 floors 失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>尝试从本次 API data 节点中解析真实期次 ID（深渊 schedule_id；剧诗/危战取首项 schedule_id）。</summary>
    private static string? TryGetFreshScheduleId(ChallengeMode mode, string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(dataJson);
            var root = document.RootElement;
            if (root.ValueKind is JsonValueKind.Array)
            {
                if (root.GetArrayLength() == 0)
                {
                    return null;
                }

                root = root[0];
            }

            if (root.ValueKind is not JsonValueKind.Object)
            {
                return null;
            }

            if (mode == ChallengeMode.SpiralAbyss)
            {
                return NullIfBlank(GetString(root, "schedule_id"));
            }

            // 剧诗/危战：完整 data 节点可能再包一层 data[]
            if (root.TryGetProperty("data", out var data) && data.ValueKind is JsonValueKind.Array)
            {
                if (data.GetArrayLength() == 0)
                {
                    return null;
                }

                root = data[0];
            }

            if (root.ValueKind is not JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("schedule", out var schedule) && schedule.ValueKind is JsonValueKind.Object)
            {
                var id = NullIfBlank(GetString(schedule, "schedule_id"));
                if (id is not null)
                {
                    return id;
                }
            }

            return NullIfBlank(GetString(root, "schedule_id"));
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[CycleChallengeService] 解析新鲜期次 ID 失败: {ex.Message}");
            return null;
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>写缓存失败时，保证下拉列表包含本次新数据对应的期次（日期未知时留空）。</summary>
    private static IReadOnlyList<ChallengePeriod> EnsureFreshPeriodPresent(
        IReadOnlyList<ChallengePeriod> periods, SelectedGameRole role, ChallengeMode mode, string scheduleId)
    {
        if (string.IsNullOrWhiteSpace(scheduleId)
            || periods.Any(p => string.Equals(p.ScheduleId, scheduleId, StringComparison.Ordinal)))
        {
            return periods;
        }

        var list = new List<ChallengePeriod>(periods.Count + 1)
        {
            new(role.Role.game_uid, mode, scheduleId, null, null, null, DateTimeOffset.UtcNow),
        };
        list.AddRange(periods);
        return list;
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
        if (periods.Count == 0)
        {
            return null;
        }

        // 优先使用期次列表里已带出的摘要（GetPeriods 只查 Summary，不加载大 Payload）
        foreach (var period in periods)
        {
            if (ReadSummaryBool(period.Summary, "has_data"))
            {
                return _repository.Get(uid, mode, period.ScheduleId);
            }
        }

        return _repository.Get(uid, mode, periods[0].ScheduleId);
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
