/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json;
using FufuLauncher.Constants;
using FufuLauncher.Helpers;
using FufuLauncher.Models.GameServer;
using FufuLauncher.Services.GameServer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class BlankPage
{
    #region 下载、预下载与版本信息

    private void PreDownloadGame_Click(object sender, RoutedEventArgs e)
    {
        string? gameDir = GetGameDirectory();
        if (gameDir is null)
        {
            _ = ShowError("Err_GamePathNotFound".GetLocalized());
            return;
        }

        var newWindow = new GameUpdateWindow(gameDir, GameUpdateOperationKind.Predownload);
        newWindow.Activate();
    }

    private void UpdateGame_Click(object sender, RoutedEventArgs e)
    {
        string? gameDir = GetGameDirectory();
        if (gameDir is null)
        {
            _ = ShowError("Err_GamePathNotFound".GetLocalized());
            return;
        }

        var newWindow = new GameUpdateWindow(gameDir, GameUpdateOperationKind.Update);
        newWindow.Activate();
    }

    private async Task RefreshUpdateStateAsync()
    {
        string? gameDir = GetGameDirectory();
        if (gameDir is null)
        {
            return;
        }

        bool finished = GameUpdateService.IsPredownloadFinished(gameDir, out _);
        DispatcherQueue.TryEnqueue(() =>
        {
            PredownloadFinishedBadge.Visibility = finished ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void DownloadGame_Click(object sender, RoutedEventArgs e)
    {
        string targetPath = _currentConfig?.GamePath;

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Genshin Game");
        }

        if (!Directory.Exists(targetPath))
        {
            try
            {
                Directory.CreateDirectory(targetPath);
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Title_PathError".GetLocalized(),
                    Content = string.Format("Err_CannotCreateGameDir_Format".GetLocalized(), targetPath, ex.Message),
                    CloseButtonText = "OkBtn".GetLocalized(),
                    XamlRoot = XamlRoot
                };
                _ = dialog.ShowAsync();
                return;
            }
        }

        var downloadWindow = new DownloadWindow(targetPath);
        downloadWindow.Activate();
    }

    private async Task GetGameBranchesInfoAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = ApiEndpoints.GameBranchesUrl;

            var response = await client.GetStringAsync(url);
            var json = JsonDocument.Parse(response);

            var root = json.RootElement;
            if (root.GetProperty("retcode").GetInt32() == 0)
            {
                var gameBranch = root.GetProperty("data").GetProperty("game_branches")[0];

                var mainInfo = gameBranch.GetProperty("main");
                var latestVersion = mainInfo.GetProperty("tag").GetString();

                var versionText = latestVersion ?? "FetchFailedShort".GetLocalized();
                DispatcherQueue.TryEnqueue(() => LatestVersionText.Text = versionText);

                if (gameBranch.TryGetProperty("pre_download", out var preDownload) &&
                    preDownload.ValueKind != JsonValueKind.Null)
                {
                    var preVersion = preDownload.GetProperty("tag").GetString() ?? "UnknownGeneric".GetLocalized();
                    DispatcherQueue.TryEnqueue(() => PreDownloadText.Text = string.Format("Msg_HasVersion_Format".GetLocalized(), preVersion));
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() => PreDownloadText.Text = "NotAvailableGeneric".GetLocalized());
                }
            }
        }
        catch
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LatestVersionText.Text = "FetchFailedShort".GetLocalized();
                PreDownloadText.Text = "FetchFailedShort".GetLocalized();
            });
        }
    }

    #endregion
}
