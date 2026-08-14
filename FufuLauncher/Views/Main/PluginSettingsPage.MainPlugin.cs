/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Messages;
using FufuLauncher.Helpers;
using Windows.System;

namespace FufuLauncher.Views;

public sealed partial class PluginSettingsPage
{
    private FileSystemWatcher _mainPluginWatcher;
    private bool _hasShownMainPluginMissingWarning = false;

    private async void OnInjectionToggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            if (toggleSwitch.IsOn == MainVM.UseInjection) return;

            if (toggleSwitch.IsOn)
            {
                var osArch = RuntimeInformation.OSArchitecture;
                if (osArch == Architecture.Arm || 
                    osArch == Architecture.Arm64)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Plugin_ArchWarning_Title".GetLocalized(),
                        Content = "Plugin_ArchWarning_Content".GetLocalized(),
                        PrimaryButtonText = "Plugin_ArchWarning_Continue".GetLocalized(),
                        CloseButtonText = "CancelBtn".GetLocalized(),
                        XamlRoot = XamlRoot
                    };

                    var result = await dialog.ShowAsync();
                    
                    if (result != ContentDialogResult.Primary)
                    {
                        toggleSwitch.IsOn = false;
                        return; 
                    }
                }
            }
            
            MainVM.UseInjection = toggleSwitch.IsOn;
        }
    }

    private void StartMainPluginWatcher()
    {
        if (_mainPluginWatcher != null) return;

        string mainPluginDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "FuFuPlugin");
        if (!Directory.Exists(mainPluginDir))
        {
            Directory.CreateDirectory(mainPluginDir);
        }

        _mainPluginWatcher = new FileSystemWatcher(mainPluginDir)
        {
            Filter = "FufuLauncher.UnlockerIsland.*",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _mainPluginWatcher.Created += OnMainPluginFileChanged;
        _mainPluginWatcher.Deleted += OnMainPluginFileChanged;
        _mainPluginWatcher.Renamed += OnMainPluginFileChanged;
        _mainPluginWatcher.Changed += OnMainPluginFileChanged;
    }

    private void OnMainPluginFileChanged(object sender, FileSystemEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.RefreshPluginStates();

            if (!ViewModel.IsMainPluginDllMissing())
            {
                _hasShownMainPluginMissingWarning = false;
                return;
            }

            ShowMainPluginMissingWarningIfNeeded();
        });
    }

    private void ShowMainPluginMissingWarningIfNeeded()
    {
        if (_hasShownMainPluginMissingWarning || !ViewModel.IsMainPluginDllMissing()) return;

        _hasShownMainPluginMissingWarning = true;
        WeakReferenceMessenger.Default.Send(new NotificationMessage(
            "Plugin_MainMissing_Title".GetLocalized(),
            "Plugin_MainMissing_Content".GetLocalized(),
            NotificationType.Error,
            6000));
    }

    private async void OnDownloadPluginClick(object sender, RoutedEventArgs e)
    {
        string urlLatest = "http://kr2-proxy.gitwarp.top:9980/https://github.com/CodeCubist/FufuLauncher--Plugins/blob/main/FuFuPlugin.zip";
        await DownloadAndInstallPluginAsync(urlLatest);
    }


    private async Task DownloadAndInstallPluginAsync(string proxyUrl)
    {
        var fileName = proxyUrl.Split('/').Last();
        if (fileName.Contains("?")) fileName = fileName.Split('?')[0];
        if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) 
            fileName = "FuFuPlugin.zip";
        
        var rawGithubUrl = proxyUrl.Replace("http://kr2-proxy.gitwarp.top:9980/", "");
        if (rawGithubUrl.Contains("github.com") && rawGithubUrl.Contains("/blob/") && !rawGithubUrl.Contains("?raw=true"))
        {
            rawGithubUrl += "?raw=true";
        }
        
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);
        var extractPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(fileName) + "_Extract_" + Guid.NewGuid());
        var pluginsDir = Path.Combine(AppContext.BaseDirectory, "Plugins");

        if (!Directory.Exists(pluginsDir)) Directory.CreateDirectory(pluginsDir);
        
        var progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Height = 20, Margin = new Thickness(0, 10, 0, 0) };
        var statusText = new TextBlock { Text = "Plugin_Download_Connecting".GetLocalized(), HorizontalAlignment = HorizontalAlignment.Center };
        var stackPanel = new StackPanel();
        stackPanel.Children.Add(statusText);
        stackPanel.Children.Add(progressBar);

        var progressDialog = new ContentDialog
        {
            Title = "Plugin_Download_Title".GetLocalized(),
            Content = stackPanel,
            XamlRoot = XamlRoot
        };

        _ = progressDialog.ShowAsync();

        try
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);

            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                HttpResponseMessage response;
                bool usedFallback = false;
                
                try 
                {
                    response = await client.GetAsync(proxyUrl, HttpCompletionOption.ResponseHeadersRead);
                    if (!response.IsSuccessStatusCode) throw new Exception("主线路失败");
                }
                catch
                {
                    statusText.Text = "Plugin_Download_Fallback".GetLocalized();
                    usedFallback = true;
                    response = await client.GetAsync(rawGithubUrl, HttpCompletionOption.ResponseHeadersRead);
                    if (!response.IsSuccessStatusCode) throw new Exception($"下载失败 (HTTP {response.StatusCode})");
                }
                
                using (response)
                {
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var totalRead = 0L;
                    var buffer = new byte[8192];
                    
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        int read;
                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;
                            if (totalBytes != -1)
                            {
                                progressBar.Value = Math.Round((double)totalRead / totalBytes * 100, 0);
                                var lineName = usedFallback ? "Plugin_Download_BackupLine".GetLocalized() : "Plugin_Download_MainLine".GetLocalized();
                                statusText.Text = string.Format("Plugin_Download_Progress_Format".GetLocalized(), lineName, progressBar.Value);
                            }
                        }
                    }
                }
            }
            
            statusText.Text = "Plugin_Download_Extracting".GetLocalized();
            progressBar.IsIndeterminate = true;
            
            if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
            Directory.CreateDirectory(extractPath);

            await Task.Run(() => ZipFile.ExtractToDirectory(tempPath, extractPath));
            
            var targetFolderName = "FuFuPlugin"; 
            var finalDestDir = Path.Combine(pluginsDir, targetFolderName);
            
            var subDirs = Directory.GetDirectories(extractPath);
            string sourceDirToMove = (subDirs.Length == 1 && Directory.GetFiles(extractPath).Length == 0) ? subDirs[0] : extractPath;

            if (Directory.Exists(finalDestDir)) Directory.Delete(finalDestDir, true);
            
            await Task.Run(() => MoveDirectorySafe(sourceDirToMove, finalDestDir));
            
            progressDialog.Hide();
            ViewModel.LoadConfiguration();

            WeakReferenceMessenger.Default.Send(new NotificationMessage("Success".GetLocalized(), "Plugin_Download_Success".GetLocalized(), NotificationType.Success));
            
            ViewModel.RefreshPluginStates();
        }
        catch (Exception ex)
        {
            progressDialog.Hide();
            var failDialog = new ContentDialog
            {
                Title = "ErrorTitle".GetLocalized(),
                Content = string.Format("Plugin_Download_Fail_Content_Format".GetLocalized(), ex.Message),
                PrimaryButtonText = "Plugin_Download_Manual".GetLocalized(),
                CloseButtonText = "CloseBtn".GetLocalized(),
                XamlRoot = XamlRoot
            };
            if (await failDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(new Uri(rawGithubUrl));
            }
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
        }
    }
}
