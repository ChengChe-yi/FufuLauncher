/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Services.UID;
using FufuLauncher.Views;
using Windows.Media.Core;
using Windows.Media.Playback;
using FufuLauncher.Activation;

namespace FufuLauncher;

public partial class App
{
    #region Background Tasks

    private const int BanCheckExitCode = 4;

    private const uint MB_YESNO = 0x00000004;
    private const int IDYES = 6;


    private async Task<IReadOnlyList<string>> LoadUidLookupAsync()
    {
        try
        {
            var uidService = GetService<IUidLookupService>();
            return await uidService.LoadAndWriteUidsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UidLookup] 写入失败: {ex.Message}");
            return Array.Empty<string>();
        }
    }


    private async Task<bool> EnforceBanListAsync(IReadOnlyList<string>? knownUids = null)
    {
        BanCheckResult result;
        try
        {
            result = await GetService<Services.UID.BanCheckService>().CheckAsync(knownUids);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BanCheck] 检查失败，按放行处理 - {ex.Message}");
            return false;
        }

        if (!result.ShouldTerminate) return false;

        Debug.WriteLine($"[BanCheck] !!! TERMINATING - {result.Outcome} (UID: {result.Uid ?? "n/a"})");

        var message = string.Format(
            "BanCheck_KilledMessage".GetLocalized(),
            Environment.NewLine,
            result.Uid ?? "-",
            result.Reason,
            result.AppealUrl);

        // MB_YESNO lets the blocked user jump straight to the appeal channel
        // before the process exits, instead of hitting a dead end.
        var choice = MessageBox(
            IntPtr.Zero,
            message,
            "BanCheck_KilledTitle".GetLocalized(),
            MB_YESNO | MB_ICONERROR);

        if (choice == IDYES)
        {
            OpenAppealUrl(result.AppealUrl);
        }

        await RequestShutdownAsync();
        return true;
    }


    private async Task RequestShutdownAsync()
    {
        try
        {
            // Close the shell if it was already created; before that point there is
            // nothing to close and OnLaunched returns without building one.
            var window = MainWindow;
            if (window != null)
            {
                await _mainDispatcherQueue.EnqueueAsync(() =>
                {
                    try
                    {
                        window.Close();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[BanCheck] 关闭主窗口失败 - {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BanCheck] 调度关闭失败 - {ex.Message}");
        }

        // Give queued settings writes a brief window to complete.
        await Task.Delay(150);

        Environment.Exit(BanCheckExitCode);
    }


    private static void OpenAppealUrl(string? url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                Debug.WriteLine($"[BanCheck] 拒绝打开非 http(s) 申诉链接: {url}");
                return;
            }

            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BanCheck] 打开申诉链接失败 - {ex.Message}");
        }
    }

    private async Task PlayStartupSoundDelayedAsync()
    {
        try
        {
            await Task.Delay(800);
            await PlayStartupSoundAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"启动语音播放失败: {ex.Message}");
        }
    }

    private async Task CheckForAnnouncementAsync()
    {
        try
        {
            await Task.Delay(1500);

            var announcementService = GetService<IAnnouncementService>();
            var announcementUrl = await announcementService.CheckForNewAnnouncementAsync();

            if (!string.IsNullOrEmpty(announcementUrl))
            {
                await _mainDispatcherQueue.EnqueueAsync(() =>
                {
                    var announcementWindow = new AnnouncementWindowL(announcementUrl);
                    announcementWindow.Activate();
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Announcement] 公告检查失败: {ex.Message}");
        }
    }

    private async Task RunStartupUpdateCheckAsync()
    {
        try
        {
            Debug.WriteLine("[Background] 后台任务开始，等待主窗口渲染...");
            await Task.Delay(500);

            Debug.WriteLine("[Background] 准备调度到UI线程...");

            await _mainDispatcherQueue.EnqueueAsync(async () =>
            {
                Debug.WriteLine("[Background] 已在UI线程，执行更新检查...");
                await CheckAndShowUpdateWindowAsync();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Background] 后台更新检查失败: {ex.Message}");
            Debug.WriteLine($"[Background] 异常类型: {ex.GetType().FullName}");
            Debug.WriteLine($"[Background] 堆栈: {ex.StackTrace}");
        }
    }

    private async Task CheckStoragePathsAsync()
    {
        try
        {
            if (!AppPaths.HasMissingPaths)
            {
                return;
            }

            var lines = new List<string>();

            if (AppPaths.MissingDataDir != null)
            {
                lines.Add(string.Format(
                    "StoragePath_MissingDir".GetLocalized(),
                    "AgreementPage_UserDataDir".GetLocalized(),
                    AppPaths.MissingDataDir));
            }

            if (AppPaths.MissingCacheDir != null)
            {
                lines.Add(string.Format(
                    "StoragePath_MissingDir".GetLocalized(),
                    "AgreementPage_CacheDir".GetLocalized(),
                    AppPaths.MissingCacheDir));
            }

            lines.Add(string.Format(
                "StoragePath_MissingHint".GetLocalized(),
                "NavSettings".GetLocalized(),
                "SectionStoragePaths".GetLocalized()));

            var notification = new Messages.NotificationMessage(
                "StoragePath_MissingTitle".GetLocalized(),
                string.Join(Environment.NewLine, lines),
                Messages.NotificationType.Warning,
                0);

            Debug.WriteLine($"[AppPaths] 存储路径缺失: {string.Join(" | ", lines)}");

            await Task.Delay(1200);

            await _mainDispatcherQueue.EnqueueAsync(() =>
            {
                WeakReferenceMessenger.Default.Send(notification);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppPaths] 启动存储路径检查失败: {ex.Message}");
        }
    }

    internal async Task PlayStartupSoundAsync()
    {
        try
        {
            var localSettingsService = GetService<ILocalSettingsService>();

            var soundEnabled = await localSettingsService.ReadSettingAsync("IsStartupSoundEnabled");
            bool isSoundEnabled = soundEnabled != null && Convert.ToBoolean(soundEnabled);

            if (!isSoundEnabled) return;

            var soundPath = await localSettingsService.ReadSettingAsync("StartupSoundPath");
            if (soundPath == null || string.IsNullOrEmpty(soundPath.ToString())) return;

            string path = soundPath.ToString();
            if (!File.Exists(path))
            {
                Debug.WriteLine($"启动语音文件不存在: {path}");
                return;
            }

            await _mainDispatcherQueue.EnqueueAsync(() =>
            {
                try
                {
                    var mediaPlayer = new MediaPlayer();
                    MediaPlayerHelper.DisableSystemMediaControls(mediaPlayer);
                    mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(path));
                    mediaPlayer.Volume = 0.7;

                    int disposed = 0;

                    void DisposeOnce()
                    {
                        if (Interlocked.Exchange(ref disposed, 1) == 0)
                        {
                            try
                            {
                                mediaPlayer.Dispose();
                            }
                            catch
                            {
                            }
                        }
                    }

                    mediaPlayer.MediaEnded += (s, e) => DisposeOnce();
                    mediaPlayer.MediaFailed += (s, e) => DisposeOnce();
                    mediaPlayer.Play();

                    var timer = _mainDispatcherQueue.CreateTimer();
                    timer.Interval = TimeSpan.FromSeconds(30);
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        DisposeOnce();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"启动语音播放异常: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"启动语音处理失败: {ex.Message}");
        }
    }

    private async Task CheckAndShowUpdateWindowAsync()
    {
        try
        {
            var updateService = GetService<IUpdateService>();
            var result = await updateService.CheckUpdateAsync();

            var devBuildService = GetService<IDevBuildDetectionService>();
            WeakReferenceMessenger.Default.Send(
                new Messages.DevBuildDetectionCompletedMessage(devBuildService.IsDevBuild));

            if (result.IsDevBuild && MainWindow is MainWindow mainWindow)
            {
                mainWindow.ShowDevBuildBadge();

                WeakReferenceMessenger.Default.Send(new Messages.BackgroundRefreshMessage());
            }

            if (result.ShouldShowUpdate)
            {
                Debug.WriteLine($"准备显示更新窗口，版本: {result.ServerVersion}，预览版: {result.IsPreview}");
                Debug.WriteLine($"[App] 动态更新公告URL: {result.UpdateInfoUrl}");

                MainWindow.Activate();

                var updateWindow = new UpdateNotificationWindow(result.UpdateInfoUrl, result.IsPreview);
                updateWindow.Title = result.IsPreview
                    ? $"预览版更新公告 - v{result.ServerVersion}"
                    : $"版本更新公告 - v{result.ServerVersion}";
                updateWindow.Activate();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"更新检查失败: {ex.Message}");
            Debug.WriteLine($"[App] 异常详情: {ex.StackTrace}");
        }
    }

    #endregion
}