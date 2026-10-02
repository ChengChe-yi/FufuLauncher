/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using FufuLauncher.Models.MiHoYo.Fingerprint;
using FufuLauncher.Services;
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
    private static readonly JsonSerializerOptions ExtJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AccountManager _accountManager;
    private readonly DeviceFpService _deviceFpService;
    private readonly List<DeviceExtFieldItem> _allFields = new();
    private readonly ObservableCollection<DeviceExtFieldItem> _visibleFields = new();
    private string? _selectedAccountId;

    public DeviceInfoWindow()
    {
        InitializeComponent();

        _accountManager = App.GetService<AccountManager>();
        _deviceFpService = App.GetService<DeviceFpService>();

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
        List<DeviceAccountChoice> choices;
        try
        {
            choices = _accountManager.GetAllAccounts()
                .Select(a => new DeviceAccountChoice(
                    a.Id,
                    string.IsNullOrWhiteSpace(a.Nickname) ? a.Stuid : $"{a.Nickname}（{a.Stuid}）"))
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DeviceInfoWindow] 读取账号列表失败: {ex.Message}");
            choices = new List<DeviceAccountChoice>();
        }

        AccountCombo.ItemsSource = choices;

        if (choices.Count == 0)
        {
            SetEditingEnabled(false);
            SetStatus("DeviceInfo_NoAccount".GetLocalized());
            return;
        }

        var activeId = _accountManager.ActiveAccountId;
        int index = choices.FindIndex(c => c.Id == activeId);
        AccountCombo.SelectedIndex = index >= 0 ? index : 0;
    }

    private async void OnAccountSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadFingerprintAsync();
    }

    private async Task LoadFingerprintAsync()
    {
        if (AccountCombo.SelectedItem is not DeviceAccountChoice choice)
        {
            return;
        }

        _selectedAccountId = choice.Id;
        HeaderHintText.Text = choice.Id;

        try
        {
            var fingerprint = await _accountManager.LoadFingerprintAsync(choice.Id);

            ApplyFingerprint(fingerprint);

            SetEditingEnabled(true);
            SetStatus(fingerprint is null ? "DeviceInfo_NoFingerprint".GetLocalized() : "");
        }
        catch (Exception ex)
        {
            SetEditingEnabled(false);
            SetStatus(ex.Message);
        }
    }

    private void ApplyFingerprint(DeviceFpRequest? fingerprint)
    {
        DeviceIdBox.Text = fingerprint?.DeviceId ?? "";
        BbsDeviceIdBox.Text = fingerprint?.BbsDeviceId ?? "";
        DeviceFpBox.Text = fingerprint?.DeviceFp ?? "";
        SeedIdBox.Text = fingerprint?.SeedId ?? "";
        SeedTimeBox.Text = fingerprint?.SeedTime ?? "";
        PlatformBox.Text = fingerprint?.Platform ?? "";
        AppNameBox.Text = fingerprint?.AppName ?? "";

        LoadExtFields(fingerprint?.ExtFields);
    }

    private void LoadExtFields(string? json)
    {
        _allFields.Clear();

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
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
            catch (JsonException)
            {
                SetStatus("DeviceInfo_InvalidJson".GetLocalized());
            }
        }

        if (_allFields.Count == 0)
        {
            _allFields.AddRange(CreateDefaultExtFields());
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
        string deviceId = RandomHex(16);
        DeviceIdBox.Text = deviceId;
        BbsDeviceIdBox.Text = DeriveBbsDeviceId(deviceId);
        SeedIdBox.Text = Guid.NewGuid().ToString();
        SeedTimeBox.Text = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        SetStatus("DeviceInfo_Randomized".GetLocalized());
    }

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        _allFields.Clear();
        _allFields.AddRange(CreateDefaultExtFields());
        ApplyFilter();
        SetStatus("DeviceInfo_DefaultsRestored".GetLocalized());
    }

    private async void OnReloadClick(object sender, RoutedEventArgs e)
    {
        await LoadFingerprintAsync();
    }

    private async void OnResetToDefaultsClick(object sender, RoutedEventArgs e)
    {
        if (_selectedAccountId is null)
        {
            return;
        }

        SetEditingEnabled(false);
        SetStatus("DeviceInfo_Resetting".GetLocalized());

        try
        {
            await _accountManager.ClearFingerprintAsync(_selectedAccountId);
            var fingerprint = await _deviceFpService.GetFingerprintRequestAsync(_selectedAccountId);

            if (fingerprint is null)
            {
                await LoadFingerprintAsync();
                SetStatus("DeviceInfo_ResetFailed".GetLocalized());
                return;
            }

            ApplyFingerprint(fingerprint);
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
        if (_selectedAccountId is null)
        {
            return;
        }

        string deviceId = DeviceIdBox.Text.Trim();
        if (deviceId.Length == 0)
        {
            SetStatus("DeviceInfo_DeviceIdRequired".GetLocalized());
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var fingerprint = new DeviceFpRequest
            {
                DeviceId = deviceId,
                BbsDeviceId = string.IsNullOrWhiteSpace(BbsDeviceIdBox.Text) ? null : BbsDeviceIdBox.Text.Trim(),
                DeviceFp = DeviceFpBox.Text.Trim(),
                SeedId = SeedIdBox.Text.Trim(),
                SeedTime = SeedTimeBox.Text.Trim(),
                Platform = PlatformBox.Text.Trim(),
                AppName = AppNameBox.Text.Trim(),
                ExtFields = SerializeExtFields()
            };

            await _accountManager.SaveFingerprintAsync(_selectedAccountId, fingerprint);
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

    private string SerializeExtFields()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }))
        {
            writer.WriteStartObject();
            foreach (var item in _allFields)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    continue;
                }

                string value = item.Value?.Trim() ?? "";
                switch (item.Kind)
                {
                    case JsonValueKind.Number when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number):
                        writer.WriteNumber(item.Key, number);
                        break;
                    case JsonValueKind.Number when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double real):
                        writer.WriteNumber(item.Key, real);
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        writer.WriteBoolean(item.Key, value.Equals("true", StringComparison.OrdinalIgnoreCase));
                        break;
                    case JsonValueKind.Null:
                        writer.WriteNull(item.Key);
                        break;
                    default:
                        writer.WriteString(item.Key, value);
                        break;
                }
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static List<DeviceExtFieldItem> CreateDefaultExtFields()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long firstInstall = now - Random.Shared.NextInt64(30, 120) * 86_400_000L;
        long lastUpdate = Math.Min(firstInstall + Random.Shared.NextInt64(0, 30) * 86_400_000L, now);

        var defaults = new ExtFields
        {
            AppInstallTimeDiff = firstInstall,
            AppUpdateTimeDiff = lastUpdate
        };

        var items = new List<DeviceExtFieldItem>();
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(defaults, ExtJsonOptions));
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            items.Add(new DeviceExtFieldItem
            {
                Key = property.Name,
                Value = ElementToText(property.Value),
                Kind = property.Value.ValueKind
            });
        }

        return items;
    }

    private static string ElementToText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.Null => "",
        JsonValueKind.Undefined => "",
        _ => element.GetRawText()
    };

    private static string RandomHex(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes((length + 1) / 2);
        return Convert.ToHexString(bytes).ToLowerInvariant()[..length];
    }

    private static string DeriveBbsDeviceId(string deviceId)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(deviceId));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        Array.Reverse(hash, 0, 4);
        Array.Reverse(hash, 4, 2);
        Array.Reverse(hash, 6, 2);
        return new Guid(hash).ToString();
    }

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
