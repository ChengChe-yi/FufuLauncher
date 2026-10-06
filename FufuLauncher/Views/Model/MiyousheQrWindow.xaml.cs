/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Drawing;
using FufuLauncher.Helpers;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class MiyousheQrWindow : Window
{
    private readonly AccountManager _accounts = App.GetService<AccountManager>();
    private readonly MiyousheQrLoginService _login = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly FrameworkElement? _launcherRoot;
    private ScreenScanRegionIndicator? _indicator;
    private Color _accentColor;
    private CancellationTokenSource? _scanCts;
    private MiyousheLoginQr? _pending;
    private Rectangle? _region;
    private string? _accountId;
    private bool _closed;
    private bool _busy;
    private bool _confirming;
    private bool _restoreMainWindow;
    private bool _scanSucceeded;
    private bool _stopAfterLogin;
    private int _confirmedCount;
    private bool _uncertainAuthorization;
    private bool _scanFailureWasConfirmation;
    private bool _scanHadFailure;
    private bool IsScanning => _scanCts != null;

    public MiyousheQrWindow()
    {
        InitializeComponent();
        _launcherRoot = App.MainWindow?.Content as FrameworkElement;
        if (_launcherRoot != null) _launcherRoot.ActualThemeChanged += LauncherThemeChanged;
        ApplyLauncherTheme();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        WindowManagerHelper.ResizeWithDpi(AppWindow, this, 580, 620);
        WindowManagerHelper.CenterWindowOnScreen(AppWindow, 580, 620);
        string icon = Path.Combine(AppContext.BaseDirectory, "Assets", "WindowIcon.ico");
        if (File.Exists(icon)) AppWindow.SetIcon(icon);
        RefreshAccount();
        Closed += (_, _) =>
        {
            _closed = true;
            if (_launcherRoot != null) _launcherRoot.ActualThemeChanged -= LauncherThemeChanged;
            _pending = null;
            _lifetime.Cancel();
            RestoreMainWindow();
            _login.Dispose();
        };
        Activated += (_, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated || _closed) return;
            if ((_pending != null || IsScanning) && _accountId != _accounts.ActiveAccountId)
            {
                _scanCts?.Cancel();
                ResetTarget();
                ShowStatus("MiyousheQr_AccountChanged", InfoBarSeverity.Warning);
            }

            if (!_busy) RefreshAccount();
        };
    }

    private void LauncherThemeChanged(FrameworkElement sender, object args) => ApplyLauncherTheme();

    private void ApplyLauncherTheme()
    {
        if (_closed) return;
        // Use the launcher's shared accent resources, including custom colors applied by ThemeHelper.
        foreach (string key in new[]
                 {
                     "SystemAccentColor", "SystemAccentColorLight1", "SystemAccentColorLight2",
                     "SystemAccentColorLight3", "SystemAccentColorDark1", "SystemAccentColorDark2",
                     "SystemAccentColorDark3",
                     "AccentFillColorDefaultBrush"
                 })
            if (Application.Current.Resources.TryGetValue(key, out var resource))
                ScanRootGrid.Resources[key] = resource;
        var accent =
            ((Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"])
            .Color;
        _accentColor = Color.FromArgb(255, accent.R, accent.G, accent.B);
        _indicator?.SetAccentColor(_accentColor);
        var theme = _launcherRoot?.ActualTheme ?? ElementTheme.Default;
        if (theme == ElementTheme.Default)
            theme = Application.Current.RequestedTheme == ApplicationTheme.Dark
                ? ElementTheme.Dark
                : ElementTheme.Light;
        ScanRootGrid.RequestedTheme = theme;
        // Match MainWindow.UpdateBackgroundOverlayTheme, without a separate tinted backdrop.
        var color = theme == ElementTheme.Dark
            ? Windows.UI.Color.FromArgb(255, 32, 32, 32)
            : Windows.UI.Color.FromArgb(255, 243, 243, 243);
        ScanRootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
        AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor =
            theme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        AppWindow.TitleBar.ButtonInactiveForegroundColor =
            theme == ElementTheme.Dark ? Microsoft.UI.Colors.LightGray : Microsoft.UI.Colors.Gray;
    }

    private bool RefreshAccount()
    {
        var account = _accounts.GetActiveAccountEntry();
        AccountText.Text = account == null
            ? "MiyousheQr_NoAccount".GetLocalized()
            : $"{account.Nickname} · {account.Stuid}";
        bool available = account != null && account.ServerType == "cn";
        if (!available)
            ShowStatus(account == null ? "MiyousheQr_NoAccount" : "MiyousheQr_CnOnly", InfoBarSeverity.Warning);
        return available;
    }

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || IsScanning || !RefreshAccount()) return;
        bool automatic = AutoConfirmCheckBox.IsChecked == true;
        bool attemptedConfirmation = false;
        ResetTarget();
        _accountId = _accounts.ActiveAccountId;
        SetBusy(true);
        try
        {
            using var selection = await SelectRegionAsync(false, _lifetime.Token);
            if (selection == null)
            {
                ShowStatus("MiyousheQr_Cancelled", InfoBarSeverity.Informational);
                return;
            }

            _region = selection.Bounds;
            ShowStatus("MiyousheQr_Recognizing", InfoBarSeverity.Informational);
            var decoded = await Task.Run(() => ScreenQrDecoder.DecodeRegions(selection.Image), _lifetime.Token);
            if (decoded.Length == 0) throw new MiyousheQrException("MiyousheQr_NotFound");
            var selected = await ChooseCodeAsync(selection.Image, selection.Bounds, decoded, _lifetime.Token);
            if (selected == null)
            {
                ShowStatus("MiyousheQr_Cancelled", InfoBarSeverity.Informational);
                return;
            }

            if (!MiyousheLoginQr.TryParse(selected.Text, out var qr))
                throw new MiyousheQrException("MiyousheQr_Unsupported");
            await PrepareCodeAsync(qr!, _lifetime.Token);
            if (automatic)
            {
                attemptedConfirmation = true;
                await AuthorizePendingAsync(_lifetime.Token);
            }
        }
        catch (OperationCanceledException)
        {
            if (!_closed)
                ShowStatus(attemptedConfirmation ? "MiyousheQr_ResultUnknown" : "MiyousheQr_HttpError",
                    InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            ShowError(ex, attemptedConfirmation);
        }
        finally
        {
            if (!_closed) SetBusy(false);
        }
    }

    private async void Continuous_Click(object sender, RoutedEventArgs e)
    {
        if (_scanCts != null)
        {
            _scanCts.Cancel();
            ResetTarget();
            UpdateControls();
            return;
        }

        if (_busy || !RefreshAccount()) return;
        bool automatic = AutoConfirmCheckBox.IsChecked == true;
        ResetTarget();
        _stopAfterLogin = StopAfterLoginCheckBox.IsChecked == true;
        _accountId = _accounts.ActiveAccountId;
        _scanSucceeded = false;
        _confirmedCount = 0;
        _uncertainAuthorization = false;
        _scanFailureWasConfirmation = false;
        _scanHadFailure = false;
        using var scanCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _scanCts = scanCts;
        SetBusy(true);
        try
        {
            // Always select afresh when starting continuous mode, so the user chooses the watched area explicitly.
            using var selection = await SelectRegionAsync(true, scanCts.Token);
            if (selection == null)
            {
                ShowStatus("MiyousheQr_Cancelled", InfoBarSeverity.Informational);
                return;
            }

            _region = selection.Bounds;
            EnsureCurrentAccount();
            await using var indicator =
                await ScreenScanRegionIndicator.ShowAsync(_region.Value, scanCts.Token, _accentColor);
            _indicator = indicator;
            SetBusy(false);
            ShowScanningStatus();
            await ContinuousQrScanService.ScanAsync(
                async token =>
                {
                    using var frame = await Task.Run(() => ScreenRegionCaptureService.CaptureRegion(_region.Value),
                        token);
                    var decoded = await Task.Run(() => ScreenQrDecoder.DecodeRegions(frame), token);
                    var selected = await ChooseCodeAsync(frame, _region.Value, decoded, token);
                    if (decoded.Length > 1 && selected == null) scanCts.Cancel();
                    return selected == null ? [] : [selected.Text];
                },
                async (qr, token) =>
                {
                    SetBusy(true);
                    _scanFailureWasConfirmation = false;
                    try
                    {
                        await PrepareCodeAsync(qr, token);
                        if (automatic)
                        {
                            _scanFailureWasConfirmation = true;
                            await AuthorizePendingAsync(token);
                        }
                    }
                    catch (MiyousheQrException ex) when
                        (ex.ResourceKey is "MiyousheQr_Expired" or "MiyousheQr_Rejected")
                    {
                        ShowError(ex, false);
                        if (ex.Code == -100) throw;
                    }
                    finally
                    {
                        if (!_closed) SetBusy(false);
                    }
                },
                () => _busy || _pending != null,
                () => _accountId == _accounts.ActiveAccountId,
                key => ShowStatus(key, InfoBarSeverity.Warning), scanCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!_closed && !_scanHadFailure)
            {
                string key = _accountId != _accounts.ActiveAccountId ? "MiyousheQr_AccountChanged"
                    : _scanSucceeded ? "MiyousheQr_Success"
                    : _confirming || _uncertainAuthorization ? "MiyousheQr_ResultUnknown"
                    : scanCts.IsCancellationRequested ? "MiyousheQr_Stopped" : "MiyousheQr_HttpError";
                ShowStatus(key, key == "MiyousheQr_Success" ? InfoBarSeverity.Success : InfoBarSeverity.Informational);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex, _scanFailureWasConfirmation);
        }
        finally
        {
            _indicator = null;
            _scanCts = null;
            ResetTarget();
            RestoreMainWindow();
            if (!_closed)
            {
                if (_confirming) UpdateControls();
                else SetBusy(false);
            }
        }
    }

    private async Task<ScreenQrDetection?> ChooseCodeAsync(Bitmap frame, Rectangle bounds,
        ScreenQrDetection[] codes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        EnsureCurrentAccount();
        if (codes.Length == 0) return null;
        if (codes.Length == 1) return codes[0];
        ShowStatus("MiyousheQr_Multiple", InfoBarSeverity.Informational);
        var selected = await ScreenQrChoiceService.ChooseAsync(frame, bounds, codes, token);
        token.ThrowIfCancellationRequested();
        EnsureCurrentAccount();
        if (!_closed) Activate();
        return selected;
    }

    private async Task<ScreenRegionSelection?> SelectRegionAsync(bool keepMainHidden, CancellationToken token)
    {
        HideMainWindow();
        try
        {
            AppWindow.Hide();
            await Task.Delay(250, token);
            return await ScreenRegionCaptureService.CaptureAsync("MiyousheQr_SelectionHint".GetLocalized(), token,
                _accentColor);
        }
        finally
        {
            if (!keepMainHidden) RestoreMainWindow();
            if (!_closed)
            {
                AppWindow.Show();
                Activate();
            }
        }
    }

    private async Task PrepareCodeAsync(MiyousheLoginQr qr, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        EnsureCurrentAccount();
        var prepared = await _login.PrepareAsync(qr, token);
        token.ThrowIfCancellationRequested();
        EnsureCurrentAccount();
        if (_closed) return;
        _pending = prepared;
        TargetText.Text = qr.TargetKey.GetLocalized();
        TargetPanel.Visibility = Visibility.Visible;
        ShowStatus("MiyousheQr_Ready", InfoBarSeverity.Informational);
        UpdateControls();
    }

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _pending == null) return;
        var token = _scanCts?.Token ?? _lifetime.Token;
        SetBusy(true);
        try
        {
            await AuthorizePendingAsync(token);
        }
        catch (Exception ex)
        {
            _scanHadFailure = IsScanning;
            ShowError(ex, true);
            _scanCts?.Cancel();
        }
        finally
        {
            if (!_closed) SetBusy(false);
        }
    }

    private async Task AuthorizePendingAsync(CancellationToken token)
    {
        var pending = _pending;
        if (pending == null) return;
        _pending = null; // Never automatically retry a submitted ticket.
        _confirming = true;
        UpdateControls();
        ShowStatus("MiyousheQr_Confirming", InfoBarSeverity.Informational);
        try
        {
            token.ThrowIfCancellationRequested();
            EnsureCurrentAccount();
            var cookies = await _accounts.LoadCookiesAsync(_accountId!);
            if (cookies == null) throw new MiyousheQrException("MiyousheQr_CredentialsMissing");
            await _login.ConfirmAsync(pending, cookies, () => _accountId == _accounts.ActiveAccountId, token);
            _confirmedCount++;
            ShowStatus("MiyousheQr_Success", InfoBarSeverity.Success);
            ResetTarget();
            if (IsScanning && _stopAfterLogin)
            {
                _scanSucceeded = true;
                _scanCts?.Cancel();
            }
            else if (IsScanning) ShowScanningStatus();
        }
        catch (OperationCanceledException)
        {
            _uncertainAuthorization = true;
            if (!_closed) ShowStatus("MiyousheQr_ResultUnknown", InfoBarSeverity.Warning);
            _scanCts?.Cancel();
            throw;
        }
        catch (Exception ex)
        {
            if (ex is not MiyousheQrException) _uncertainAuthorization = true;
            throw;
        }
        finally
        {
            _confirming = false;
        }
    }

    private void HideMainWindow()
    {
        if (App.MainWindow?.AppWindow.IsVisible != true) return;
        _restoreMainWindow = true;
        App.MainWindow.AppWindow.Hide();
    }

    private void RestoreMainWindow()
    {
        if (!_restoreMainWindow) return;
        _restoreMainWindow = false;
        App.MainWindow?.AppWindow.Show();
    }

    private void EnsureCurrentAccount()
    {
        if (_accountId == null || _accountId != _accounts.ActiveAccountId)
            throw new MiyousheQrException("MiyousheQr_AccountChanged");
    }

    private void ResetTarget()
    {
        _pending = null;
        if (_closed) return;
        TargetPanel.Visibility = Visibility.Collapsed;
        ConfirmButton.IsEnabled = false;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        if (!_closed) UpdateControls();
    }

    private void UpdateControls()
    {
        bool scanning = IsScanning;
        Progress.IsActive = _busy || scanning;
        Progress.Visibility = _busy || scanning ? Visibility.Visible : Visibility.Collapsed;
        CaptureButton.IsEnabled = !_busy && !scanning;
        ContinuousButton.IsEnabled = scanning ? !_scanCts!.IsCancellationRequested : !_busy;
        ContinuousButton.Content = (scanning ? "MiyousheQr_Stop" : "MiyousheQr_Continuous").GetLocalized();
        AutoConfirmCheckBox.IsEnabled = !_busy && !scanning && _pending == null;
        StopAfterLoginCheckBox.IsEnabled = !_busy && !scanning && _pending == null;
        ConfirmButton.IsEnabled = !_busy && _pending != null;
    }

    private void ShowScanningStatus()
    {
        ShowStatus("MiyousheQr_Scanning", InfoBarSeverity.Informational);
        if (!_closed) Status.Message = string.Format("MiyousheQr_Scanning".GetLocalized(), _confirmedCount);
    }

    private void ShowStatus(string key, InfoBarSeverity severity)
    {
        if (_closed) return;
        Status.Message = key.GetLocalized();
        Status.Severity = severity;
        Status.IsOpen = true;
    }

    private void ShowError(Exception ex, bool confirming)
    {
        if (_closed) return;
        ResetTarget();
        string key = ex is MiyousheQrException qrError ? qrError.ResourceKey
            : confirming ? "MiyousheQr_ResultUnknown"
            : ex is HttpRequestException or OperationCanceledException ? "MiyousheQr_HttpError"
            : ex is System.Text.Json.JsonException ? "MiyousheQr_ResponseError" : "MiyousheQr_CaptureFailed";
        ShowStatus(key, InfoBarSeverity.Error);
        if (ex is MiyousheQrException { Code: int code }) Status.Message += $" ({code})";
        // Do not log exception messages: an HTTP/QR exception can contain a ticket or credential.
    }
}