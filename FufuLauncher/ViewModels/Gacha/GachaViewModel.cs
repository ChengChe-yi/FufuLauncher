/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using FufuLauncher.Services;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;

namespace FufuLauncher.ViewModels;

public class GachaConfig
{
    public string? Url
    {
        get;
        set;
    }
}

public partial class GachaViewModel : ObservableRecipient
{
    private readonly GachaService _gachaService;
    private Process? _captureProcess;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _isManualStop;

    private List<dynamic> _allGachaLogs = new();

    private readonly string _configPath = Path.Combine(Helpers.AppPaths.DataDir, "gacha_config.json");

    [ObservableProperty] private string _inputUrl;
    [ObservableProperty] private string _statusMessage = "Common_Ready".GetLocalized();

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanExport))]
    private bool _isAnalyzing;

    [ObservableProperty] private bool _isCapturing;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CaptureButtonText))]
    private ObservableCollection<GachaStatistic> _statistics = new();

    public string CaptureButtonText =>
        IsCapturing ? "Gacha_StopCapture".GetLocalized() : "Gacha_StartCaptureApp".GetLocalized();

    public bool CanExport => !IsAnalyzing && Statistics.Count > 0;

    public GachaViewModel(GachaService gachaService)
    {
        _gachaService = gachaService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    [RelayCommand]
    public async Task OnViewLoadedAsync()
    {
        await LoadConfigAndAutoRunAsync();
    }

    partial void OnIsCapturingChanged(bool value)
    {
        OnPropertyChanged(nameof(CaptureButtonText));
    }

    partial void OnIsAnalyzingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanExport));
    }

    [RelayCommand]
    private async Task StartAnalysisAsync()
    {
        if (string.IsNullOrWhiteSpace(InputUrl))
        {
            StatusMessage = "Gacha_PleaseEnterValidUrl".GetLocalized();
            return;
        }

        var cleanUrl = _gachaService.ExtractBaseUrl(InputUrl);
        if (cleanUrl == null)
        {
            StatusMessage = "Gacha_InvalidUrlFormat".GetLocalized();
            return;
        }

        await SaveConfigAsync(InputUrl);

        IsAnalyzing = true;
        StatusMessage = "Gacha_WaitingForData".GetLocalized();
        Statistics.Clear();
        _allGachaLogs.Clear();

        try
        {
            foreach (var typeCode in GachaService.GachaTypes.Keys)
            {
                StatusMessage = string.Format("Gacha_FetchingType_Format".GetLocalized(),
                    GachaService.GetPoolName(typeCode));
                var items = await _gachaService.FetchGachaLogAsync(cleanUrl, typeCode);

                if (items.Count > 0)
                {
                    var stat = _gachaService.AnalyzePool(typeCode, items);
                    Statistics.Add(stat);
                    foreach (var item in items)
                    {
                        _allGachaLogs.Add(new { TypeCode = typeCode, Data = item });
                    }
                }
            }

            StatusMessage = "Gacha_AnalysisCompleted".GetLocalized();
            OnPropertyChanged(nameof(CanExport));
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format("Common_Error_Format".GetLocalized(), ex.Message);
        }
        finally
        {
            IsAnalyzing = false;
        }
    }


    [RelayCommand]
    private async Task ExportUigfAsync()
    {
        if (_allGachaLogs.Count == 0)
        {
            StatusMessage = "Gacha_NoDataToExport".GetLocalized();
            return;
        }

        try
        {
            StatusMessage = "Gacha_ExportingUigf".GetLocalized();

            string uid = "";
            if (_allGachaLogs.Count > 0)
            {
                var firstItem = _allGachaLogs[0].Data;
                var prop = firstItem.GetType().GetProperty("Uid");
                if (prop != null)
                {
                    var val = prop.GetValue(firstItem);
                    uid = val?.ToString() ?? "";
                }
            }

            var exportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var infoObj = new
            {
                uid = uid,
                lang = "zh-cn",
                export_time = exportTime,
                export_app = "FufuLauncher",
                export_app_version = "v1.0",
                uigf_version = "v2.2",
                export_timestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
            };

            var uigfList = new List<Dictionary<string, object>>();

            long tempIdCounter = 1000000000000000000;

            foreach (var entry in _allGachaLogs)
            {
                var typeCode = (string)entry.TypeCode;
                var item = entry.Data;

                var dict = new Dictionary<string, object>();

                dict["uigf_gacha_type"] = typeCode;
                dict["gacha_type"] = GetPropValue(item, "GachaType") ?? typeCode;
                dict["item_id"] = GetPropValue(item, "ItemId") ?? "";
                dict["count"] = GetPropValue(item, "Count") ?? "1";
                dict["time"] = GetPropValue(item, "Time") ?? "";
                dict["name"] = GetPropValue(item, "Name") ?? "";
                dict["item_type"] = GetPropValue(item, "ItemType") ?? "";
                dict["rank_type"] = GetPropValue(item, "RankType") ?? "";
                dict["id"] = GetPropValue(item, "Id") ?? "";

                dict["uid"] = uid;
                dict["lang"] = "zh-cn";

                if (string.IsNullOrEmpty(dict["id"]?.ToString()))
                {
                    tempIdCounter++;
                    dict["id"] = tempIdCounter.ToString();
                }

                uigfList.Add(dict);
            }

            uigfList.Sort((a, b) =>
            {
                var idA = long.Parse(a["id"].ToString() ?? "0");
                var idB = long.Parse(b["id"].ToString() ?? "0");
                return idA.CompareTo(idB);
            });

            var finalObj = new
            {
                info = infoObj,
                list = uigfList
            };

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fileName = string.IsNullOrEmpty(uid) ? "uigf_export.json" : $"{uid}_uigf.json";
            var fullPath = Path.Combine(desktopPath, fileName);

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var jsonString = JsonSerializer.Serialize(finalObj, jsonOptions);
            await File.WriteAllTextAsync(fullPath, jsonString);

            StatusMessage = string.Format("Gacha_ExportSuccess_Format".GetLocalized(), fileName);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format("Gacha_ExportFailed_Format".GetLocalized(), ex.Message);
        }
    }

    private object? GetPropValue(object obj, string propName)
    {
        var prop = obj.GetType().GetProperty(propName);
        return prop?.GetValue(obj);
    }

    [RelayCommand]
    private async Task ToggleCaptureAsync()
    {
        if (IsCapturing)
        {
            try
            {
                if (_captureProcess != null && !_captureProcess.HasExited)
                {
                    _isManualStop = true;
                    _captureProcess.Kill();
                    StatusMessage = "Gacha_CaptureStoppedByUser".GetLocalized();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format("Gacha_StopFailed_Format".GetLocalized(), ex.Message);
            }

            return;
        }

        try
        {
            _isManualStop = false;

            var startInfo = new ProcessStartInfo
            {
                FileName = "CaptureApp.exe",
                Arguments = "-run",
                UseShellExecute = true
            };

            _captureProcess = Process.Start(startInfo);

            if (_captureProcess != null)
            {
                IsCapturing = true;
                StatusMessage = "Gacha_CapturingOpenRecordInGame".GetLocalized();

                await _captureProcess.WaitForExitAsync();

                IsCapturing = false;

                if (!_isManualStop)
                {
                    StatusMessage = "Gacha_CapturingReadingData".GetLocalized();
                    await CheckClipboardAndRunAsync();
                }
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            IsCapturing = false;
            StatusMessage = "Gacha_CaptureAppNotFound".GetLocalized();
        }
        catch (Exception ex)
        {
            IsCapturing = false;
            StatusMessage = string.Format("Common_Error_Format".GetLocalized(), ex.Message);
        }
    }

    private async Task CheckClipboardAndRunAsync()
    {
        _dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var package = Clipboard.GetContent();
                if (package.Contains(StandardDataFormats.Text))
                {
                    var clipboardText = await package.GetTextAsync();

                    if (!string.IsNullOrWhiteSpace(clipboardText) &&
                        clipboardText.Contains("authkey") &&
                        clipboardText.StartsWith("http"))
                    {
                        InputUrl = clipboardText;
                        StatusMessage = "Gacha_UrlObtainedAutoLoading".GetLocalized();
                        await StartAnalysisAsync();
                    }
                    else
                    {
                        StatusMessage = "Gacha_InvalidClipboardContent".GetLocalized();
                    }
                }
            }
            catch
            {
                StatusMessage = "Gacha_ReadClipboardFailed".GetLocalized();
            }
        });
    }

    private async Task LoadConfigAndAutoRunAsync()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                var config = JsonSerializer.Deserialize<GachaConfig>(json);

                if (config != null && !string.IsNullOrWhiteSpace(config.Url))
                {
                    _dispatcherQueue.TryEnqueue(async () =>
                    {
                        InputUrl = config.Url;
                        StatusMessage = "Gacha_HistoryLoadedAutoFetching".GetLocalized();
                        await Task.Delay(500);
                        await StartAnalysisAsync();
                    });
                }
            }
            else
            {
                StatusMessage = "Gacha_NoHistoryFoundTryCapture".GetLocalized();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format("Gacha_ReadConfigFailed_Format".GetLocalized(), ex.Message);
        }
    }

    private async Task SaveConfigAsync(string url)
    {
        try
        {
            var config = new GachaConfig { Url = url };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configPath, json);
            Debug.WriteLine($"配置已保存到: {_configPath}");
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format("Common_SaveFailed_Format".GetLocalized(), ex.Message);
        }
    }
}