/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using FufuLauncher.Models.MiHoYo.Identity;
using FufuLauncher.Services.Device;
using FufuLauncher.Services.MiHoYo.Fingerprint;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FufuLauncher.Views;

public sealed partial class DeviceInfoWindow : Window
{
    private readonly DeviceFpService _deviceFpService;
    private readonly MobileDeviceService _deviceService;
    private readonly List<DeviceExtFieldItem> _allFields = new();
    private readonly ObservableCollection<DeviceExtFieldItem> _visibleFields = new();

    public DeviceInfoWindow()
    {
        InitializeComponent();

        _deviceFpService = App.GetService<DeviceFpService>();
        _deviceService = App.GetService<MobileDeviceService>();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop();

        var windowSize = new SizeInt32(1040, 780);
        AppWindow.Resize(windowSize);
        ConfigureWindow(windowSize);

        ExtFieldsList.ItemsSource = _visibleFields;

        _ = InitializeAsync();
    }

    private void ConfigureWindow(SizeInt32 windowSize)
    {
        try
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "WindowIcon.ico");
            if (File.Exists(iconPath))
            {
                AppWindow.SetIcon(iconPath);
            }

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            }

            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var work = displayArea.WorkArea;
                AppWindow.Move(new PointInt32(
                    work.X + (work.Width - windowSize.Width) / 2,
                    work.Y + (work.Height - windowSize.Height) / 2));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DeviceInfoWindow] 窗口初始化失败: {ex.Message}");
        }
    }

    private async Task InitializeAsync()
    {
        // 设备身份为 App 级：与账号无关，展示机型供对照。
        var profile = _deviceService.Device;
        ScopeText.Text = $"{profile.Brand} {profile.Model} / Android {profile.OsVersion}";
        HeaderHintText.Text = "mihoyo_device.json";

        await LoadIdentityAsync();
    }

    private async void OnReloadClick(object sender, RoutedEventArgs e) => await LoadIdentityAsync();

    private async Task LoadIdentityAsync()
    {
        try
        {
            var identity = await _deviceFpService.GetIdentityAsync();
            ApplyFingerprint(identity);

            SetEditingEnabled(true);
            SetStatus(string.IsNullOrEmpty(identity.DeviceFp)
                ? "DeviceInfo_NoFingerprint".GetLocalized()
                : "");
        }
        catch (Exception ex)
        {
            SetEditingEnabled(false);
            SetStatus(ex.Message);
        }
    }

    private void ApplyFingerprint(MiHoYoDeviceIdentity? identity)
    {
        DeviceIdBox.Text = identity?.DeviceId ?? "";
        BbsDeviceIdBox.Text = identity?.BbsDeviceId ?? "";
        DeviceFpBox.Text = identity?.DeviceFp ?? "";
        SeedIdBox.Text = identity?.SeedId ?? "";
        SeedTimeBox.Text = identity?.SeedTime ?? "";

        LoadExtFields();
    }


    private void LoadExtFields()
    {
        _allFields.Clear();

        try
        {
            string json = _deviceFpService.BuildExtFieldsJson();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    _allFields.Add(new DeviceExtFieldItem
                    {
                        Key = property.Name,
                        Value = ElementToText(property.Value),
                        Kind = property.Value.ValueKind
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[DeviceInfoWindow] ext_fields 生成失败: {ex.Message}");
            SetStatus("DeviceInfo_InvalidJson".GetLocalized());
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = ExtFilterBox.Text?.Trim() ?? "";

        _visibleFields.Clear();
        foreach (var item in _allFields)
        {
            if (query.Length == 0
                || item.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Value.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                _visibleFields.Add(item);
            }
        }

        ExtCountText.Text = $"{_visibleFields.Count} / {_allFields.Count}";
    }

    private void OnFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnRandomizeClick(object sender, RoutedEventArgs e)
    {
        // 仅重新生成「身份原始值」；device_fp 由服务端签发，留空待注册回填。
        var created = MiHoYoDeviceIdentity.CreateNew();
        DeviceIdBox.Text = created.DeviceId;
        BbsDeviceIdBox.Text = created.BbsDeviceId;
        DeviceFpBox.Text = "";
        SeedIdBox.Text = created.SeedId;
        SeedTimeBox.Text = created.SeedTime;
        SetStatus("DeviceInfo_Randomized".GetLocalized());
    }

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        LoadExtFields();
        SetStatus("DeviceInfo_DefaultsRestored".GetLocalized());
    }

    private async void OnResetToDefaultsClick(object sender, RoutedEventArgs e)
    {
        SetEditingEnabled(false);
        SetStatus("DeviceInfo_Resetting".GetLocalized());

        try
        {
            // 丢弃落盘身份并重新生成 + 注册。
            var identity = await _deviceFpService.ResetAndRegisterAsync();
            ApplyFingerprint(identity);
            SetStatus("DeviceInfo_ResetDone".GetLocalized());
        }
        catch (Exception ex)
        {
            string failed = "DeviceInfo_ResetFailed".GetLocalized();
            SetStatus($"{failed}: {ex.Message}");
        }
        finally
        {
            SetEditingEnabled(true);
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // device_id 必须为 16 位 hex。
        string deviceId = DeviceIdBox.Text.Trim();
        if (!MiHoYoDeviceIdentity.IsValidDeviceId(deviceId))
        {
            SetStatus("DeviceInfo_DeviceIdRequired".GetLocalized());
            return;
        }

        // seed_id / seed_time 也必须合法：否则下次读取会被判为不可用而重新生成，
        // 用户这次保存的内容会被静默覆盖。
        string seedId = SeedIdBox.Text.Trim();
        if (!MiHoYoDeviceIdentity.IsValidSeedId(seedId))
        {
            SetStatus("DeviceInfo_SeedIdInvalid".GetLocalized());
            return;
        }

        string seedTime = SeedTimeBox.Text.Trim();
        if (!MiHoYoDeviceIdentity.IsValidSeedTime(seedTime))
        {
            SetStatus("DeviceInfo_SeedTimeInvalid".GetLocalized());
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var identity = new MiHoYoDeviceIdentity
            {
                DeviceId = deviceId,
                DeviceFp = DeviceFpBox.Text.Trim(),
                SeedId = seedId,
                SeedTime = seedTime,
            };

            await _deviceFpService.SaveIdentityAsync(identity);
            ApplyFingerprint(identity);
            SetStatus("DeviceInfo_Saved".GetLocalized());
        }
        catch (Exception ex)
        {
            string failed = "DeviceInfo_SaveFailed".GetLocalized();
            SetStatus($"{failed}: {ex.Message}");
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private static string ElementToText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.Null => "",
        JsonValueKind.Undefined => "",
        _ => element.GetRawText()
    };

    private void SetEditingEnabled(bool enabled)
    {
        SaveButton.IsEnabled = enabled;
        RestoreButton.IsEnabled = enabled;
        ReloadButton.IsEnabled = enabled;
        ResetButton.IsEnabled = enabled;
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
    }
}