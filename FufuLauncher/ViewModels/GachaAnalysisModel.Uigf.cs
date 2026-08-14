/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Messages;
using FufuLauncher.Models;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public partial class GachaAnalysisModel
{
    #region UIGF Import & Export

    [RelayCommand]
    private async Task ExportUigfAsync(string version)
    {
        if (string.IsNullOrEmpty(version)) version = "v4.2";

        try
        {
            var allLogs = _cachedCharacterLogs
                .Concat(_cachedWeaponLogs)
                .Concat(_cachedChronicledLogs)
                .Concat(_cachedNoviceLogs)
                .Concat(_cachedStandardLogs)
                .ToList();

            if (allLogs.Count == 0)
            {
                OnErrorAction?.Invoke("没有可导出的抽卡记录");
                return;
            }

            var uid = _currentUid;
            if (string.IsNullOrEmpty(uid)) uid = "unknown";

            object finalObj;

            if (version.StartsWith("v4"))
            {
                var hk4eObj = new
                {
                    uid = uid,
                    timezone = 8,
                    lang = "zh-cn",
                    list = allLogs.Select(log => new
                    {
                        uigf_gacha_type = GameToUigfGachaType(log.GachaType),
                        gacha_type = log.GachaType,
                        item_id = log.ItemId ?? "",
                        count = log.Count ?? "1",
                        time = log.Time ?? "",
                        name = log.Name ?? "",
                        item_type = log.ItemType ?? "",
                        rank_type = log.RankType ?? "",
                        id = log.Id ?? ""
                    }).ToList()
                };

                var infoObj = new
                {
                    export_timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    export_app = "FufuLauncher",
                    export_app_version = $"{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version}",
                    version = version
                };

                if (version == "v4.2")
                {
                    finalObj = new
                    {
                        hkrpg = Array.Empty<object>(),
                        hk4e_ugc = Array.Empty<object>(),
                        info = infoObj,
                        hk4e = new[] { hk4eObj }
                    };
                }
                else
                {
                    finalObj = new
                    {
                        info = infoObj,
                        hk4e = new[] { hk4eObj }
                    };
                }
            }
            else
            {
                var exportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                finalObj = new
                {
                    info = new
                    {
                        uid = uid,
                        lang = "zh-cn",
                        export_time = exportTime,
                        export_app = "FufuLauncher",
                        export_app_version = $"{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version}",
                        uigf_version = version,
                        export_timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    },
                    list = allLogs.Select(log => new
                    {
                        uigf_gacha_type = GameToUigfGachaType(log.GachaType),
                        gacha_type = log.GachaType,
                        item_id = log.ItemId ?? "",
                        count = log.Count ?? "1",
                        time = log.Time ?? "",
                        name = log.Name ?? "",
                        item_type = log.ItemType ?? "",
                        rank_type = log.RankType ?? "",
                        id = log.Id ?? ""
                    }).ToList()
                };
            }

            var json = JsonSerializer.Serialize(finalObj, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var path = await FilePickerService.PickSaveFileAsync(
                GetWindow?.Invoke(),
                new[] { ("JSON 文件", new[] { ".json" }) },
                $"UIGF_{version.Replace(".", "")}_{uid}_{DateTimeOffset.UtcNow:yyyyMMdd}",
                Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
                msg => OnErrorAction?.Invoke(msg));
            if (string.IsNullOrEmpty(path)) return;

            await File.WriteAllTextAsync(path, json);
            WeakReferenceMessenger.Default.Send(new NotificationMessage("导出成功", $"已导出 {allLogs.Count} 条记录到 {Path.GetFileName(path)} ({version})", NotificationType.Success, 3000));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Gacha] 导出失败: {ex}");
            CrawlerStatus = $"导出失败: {ex.Message}";
            OnErrorAction?.Invoke(CrawlerStatus);
        }
    }

    [RelayCommand]
    private async Task ImportUigfAsync()
    {
        try
        {
            var path = await FilePickerService.PickOpenFileAsync(
                GetWindow?.Invoke(),
                new[] { ("JSON 文件", new[] { ".json" }) },
                Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
                msg => OnErrorAction?.Invoke(msg));
            if (string.IsNullOrEmpty(path)) return;

            IsFetching = true;
            CrawlerStatus = "正在读取 UIGF 文件...";

            var json = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string version = "";
            if (root.TryGetProperty("info", out var infoNode))
            {
                if (infoNode.TryGetProperty("version", out var vNode)) version = vNode.GetString() ?? "";
                else if (infoNode.TryGetProperty("uigf_version", out var uvNode)) version = uvNode.GetString() ?? "";
            }

            if (string.IsNullOrEmpty(version))
            {
                CrawlerStatus = "无法识别 UIGF 版本，请确认文件格式正确";
                IsFetching = false;
                OnErrorAction?.Invoke(CrawlerStatus);
                return;
            }

            List<JsonElement> items = new();
            string importUid = "";
            int entryTimezone = 8;
            string entryLang = "zh-cn";

            if (version.StartsWith("v4"))
            {
                if (!root.TryGetProperty("hk4e", out var hk4eList) || hk4eList.GetArrayLength() == 0)
                {
                    CrawlerStatus = "文件中未找到有效的抽卡记录";
                    IsFetching = false;
                    OnErrorAction?.Invoke(CrawlerStatus);
                    return;
                }

                var entry = hk4eList.EnumerateArray().FirstOrDefault();
                importUid = entry.TryGetProperty("uid", out var u) ? u.GetString() ?? "" : "";
                entryTimezone = entry.TryGetProperty("timezone", out var tz) ? tz.GetInt32() : 8;
                entryLang = entry.TryGetProperty("lang", out var lg) ? lg.GetString() ?? "zh-cn" : "zh-cn";
                if (entry.TryGetProperty("list", out var listNode) && listNode.ValueKind == JsonValueKind.Array)
                {
                    items = listNode.EnumerateArray().ToList();
                }
            }
            else if (version.StartsWith("v2") || version.StartsWith("v3"))
            {
                importUid = infoNode.TryGetProperty("uid", out var u) ? u.GetString() ?? "" : "";
                entryLang = infoNode.TryGetProperty("lang", out var lg) ? lg.GetString() ?? "zh-cn" : "zh-cn";
                if (root.TryGetProperty("list", out var listNode) && listNode.ValueKind == JsonValueKind.Array)
                {
                    items = listNode.EnumerateArray().ToList();
                }
            }
            else
            {
                CrawlerStatus = $"不支持的 UIGF 版本：{version}";
                IsFetching = false;
                OnErrorAction?.Invoke(CrawlerStatus);
                return;
            }

            if (items.Count == 0)
            {
                CrawlerStatus = "文件中未找到抽卡记录";
                IsFetching = false;
                OnErrorAction?.Invoke(CrawlerStatus);
                return;
            }

            foreach (var x in items)
            {
                if (!x.TryGetProperty("id", out _) || !x.TryGetProperty("item_id", out _) ||
                    !x.TryGetProperty("time", out _) || !x.TryGetProperty("gacha_type", out _))
                {
                    CrawlerStatus = "文件中存在不完整的记录（缺少 id/item_id/time/gacha_type），请检查文件格式";
                    IsFetching = false;
                    OnErrorAction?.Invoke(CrawlerStatus);
                    return;
                }
            }

            if (!await HandleUidMismatchAsync(importUid)) { IsFetching = false; return; }

            _currentUid = importUid;

            if (_savedMetadata.Count == 0)
            {
                CrawlerStatus = "正在获取物品元数据用于名称映射...";
                await FetchMetadataFromApiAsync();
                IsFetching = true;
            }

            CrawlerStatus = $"正在导入 {items.Count} 条记录...";

            var newLogs = items.Select(uigfItem =>
            {
                var gachaType = uigfItem.GetProperty("gacha_type").GetString() ?? "";
                var time = uigfItem.GetProperty("time").GetString() ?? "";

                if (entryTimezone != 8 && !string.IsNullOrEmpty(time))
                {
                    try
                    {
                        if (DateTime.TryParse(time, out var dt))
                        {
                            dt = dt.AddHours(8 - entryTimezone);
                            time = dt.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                    }
                    catch { }
                }

                return new GachaLogItem
                {
                    Id = uigfItem.GetProperty("id").GetString() ?? "",
                    Uid = importUid,
                    GachaType = gachaType,
                    ItemId = uigfItem.GetProperty("item_id").GetString() ?? "",
                    Time = time,
                    Name = uigfItem.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    RankType = uigfItem.TryGetProperty("rank_type", out var rt) ? rt.GetString() ?? "" : "",
                    ItemType = uigfItem.TryGetProperty("item_type", out var it) ? it.GetString() ?? "" : "",
                    Lang = entryLang
                };
            }).ToList();

            FillMissingFieldsFromMetadata(newLogs);
            _cachedCharacterLogs = MergeLogs(_cachedCharacterLogs, newLogs.Where(x => GetNormalizedGachaType(x.GachaType) == "301").ToList());
            _cachedWeaponLogs = MergeLogs(_cachedWeaponLogs, newLogs.Where(x => GetNormalizedGachaType(x.GachaType) == "302").ToList());
            _cachedChronicledLogs = MergeLogs(_cachedChronicledLogs, newLogs.Where(x => GetNormalizedGachaType(x.GachaType) == "500").ToList());
            _cachedNoviceLogs = MergeLogs(_cachedNoviceLogs, newLogs.Where(x => GetNormalizedGachaType(x.GachaType) == "100").ToList());
            _cachedStandardLogs = MergeLogs(_cachedStandardLogs, newLogs.Where(x => GetNormalizedGachaType(x.GachaType) == "200").ToList());

            HasGachaData = true;
            SaveGachaDataAsync();

            var total = _cachedCharacterLogs.Count + _cachedWeaponLogs.Count + _cachedChronicledLogs.Count + _cachedNoviceLogs.Count + _cachedStandardLogs.Count;
            CrawlerStatus = $"导入完成，共 {total} 条记录，正在检查图片资源...";
            IsScraping = true;
            if (RequestMetadataScrapeAction != null)
                RequestMetadataScrapeAction.Invoke();
            else
                RefreshUIFromCache();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Gacha] 导入失败: {ex}");
            CrawlerStatus = $"导入失败: [{ex.GetType().Name}] {ex.Message}";
            IsFetching = false;
            OnErrorAction?.Invoke(CrawlerStatus);
        }

        if (!IsScraping) IsFetching = false;
    }

    #endregion
}
