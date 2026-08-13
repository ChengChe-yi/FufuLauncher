/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using FufuLauncher.Constants;
using FufuLauncher.Helpers;
using FufuLauncher.Models.GameServer;
using FufuLauncher.Services.GameServer;

namespace FufuLauncher.Views
{
    public sealed partial class AdvancedServerSwitchPage : Page
    {
        private string _gameDir = string.Empty;
        private Window? _parentWindow;
        private string _targetServer = string.Empty;

        private CancellationTokenSource? _cts;
        private GameServerScheme? _currentScheme;

        private readonly GameServerDownloadMonitor _downloadMonitor = new();
        private readonly RemainingChunksTracker _remainingChunksTracker = new();
        private DownloadSpeedChartController? _chartController;

        public AdvancedServerSwitchPage()
        {
            InitializeComponent();
            _chartController = new DownloadSpeedChartController(SpeedGraphChart, _downloadMonitor);
            SpeedGraphChart.NoDataText = "SpeedGraph_NoData".GetLocalized();
            RemainingChunksList.ItemsSource = _remainingChunksTracker.Chunks;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is BlankPage.SwitchPageParams param)
            {
                _gameDir = param.GameDir;
                _parentWindow = param.ParentWindow;
                _targetServer = param.TargetServer ?? string.Empty;
            }

            InitializeServerSelector();
        }

        private void InitializeServerSelector()
        {
            GameServerConfigurationService configurationService = App.GetService<GameServerConfigurationService>();
            _currentScheme = string.IsNullOrEmpty(_gameDir) ? null : configurationService.TryDetectCurrentScheme(_gameDir);

            CurrentServerText.Text = _currentScheme is null
                ? "AdvancedServerSwitch_UnknownCurrent".GetLocalized()
                : string.Format("AdvancedServerSwitch_CurrentServer".GetLocalized(), _currentScheme.DisplayName);
            
            TargetServerCombo.ItemsSource = GameServerScheme.Selectable;

            TargetServerCombo.SelectedItem = ResolvePreferredTarget();
        }

        private GameServerScheme? ResolvePreferredTarget()
        {
            if (!string.IsNullOrEmpty(_targetServer))
            {
                GameServerScheme preset = GameServerScheme.FromPreset(_targetServer);
                if (preset.IsNotCompatOnly)
                {
                    return preset;
                }
            }
            
            return GameServerScheme.Selectable.FirstOrDefault(scheme =>
                _currentScheme is null || !scheme.Equals(_currentScheme)) ?? GameServerScheme.Selectable.FirstOrDefault();
        }

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (TargetServerCombo.SelectedItem is not GameServerScheme target || _currentScheme is null || string.IsNullOrEmpty(_gameDir))
            {
                await ShowMessageAsync("AdvancedServerSwitch_SelectTarget".GetLocalized());
                return;
            }

            if (_cts is not null)
            {
                return;
            }

            bool retryConversion;
            do
            {
                retryConversion = false;

                string? pluginDeleteError = TryDeletePluginsFolders();
                if (pluginDeleteError is not null)
                {
                    AppendStatus(string.Format("AdvancedServerSwitch_PluginCleanFailed".GetLocalized(), pluginDeleteError));
                }

                StartBtn.IsEnabled = false;
                TargetServerCombo.IsEnabled = false;
                ProgressPanel.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = true;
                ProgressDetailText.Text = string.Empty;
                CancelBtn.IsEnabled = true;
                _chartController!.Start();
                _remainingChunksTracker.Reset();
                RemainingCountText.Text = string.Format("AdvancedServerSwitch_RemainingChunks".GetLocalized(), 0);

                _cts = new CancellationTokenSource();
                CancellationToken token = _cts.Token;

                var converter = App.GetService<GameServerConverter>();
                var progress = new Progress<GameServerConversionProgress>(p => DispatcherQueue.TryEnqueue(() => UpdateProgress(p)));

                try
                {
                    await Task.Run(() => converter.ConvertAsync(_gameDir, _currentScheme, target, progress, AppendStatus, token, _downloadMonitor));

                    _chartController.Stop();
                    ProgressPanel.Visibility = Visibility.Collapsed;

                    var successDialog = new ContentDialog
                    {
                        Title = "AdvancedServerSwitch_Done".GetLocalized(),
                        Content = string.Format("AdvancedServerSwitch_DoneMsg".GetLocalized(), target.DisplayName),
                        CloseButtonText = "OkBtn".GetLocalized(),
                        XamlRoot = XamlRoot
                    };

                    await successDialog.ShowAsync();

                    _parentWindow?.Close();
                    return;
                }
                catch (OperationCanceledException)
                {
                    _chartController.SetPaused();
                    ProgressPanel.Visibility = Visibility.Collapsed;
                    AppendStatus("AdvancedServerSwitch_Cancelled".GetLocalized());
                }
                catch (Exception ex)
                {
                    _chartController.SetFailed();
                    ProgressPanel.Visibility = Visibility.Collapsed;

                    var errDialog = new ContentDialog
                    {
                        Title = "AdvancedServerSwitch_FailedTitle".GetLocalized(),
                        Content = string.Format("AdvancedServerSwitch_FailedMsg".GetLocalized(), ex.Message),
                        PrimaryButtonText = "AdvancedServerSwitch_RetryBtn".GetLocalized(),
                        CloseButtonText = "AdvancedServerSwitch_RepairBtn".GetLocalized(),
                        XamlRoot = XamlRoot
                    };

                    var result = await errDialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        await TryManualPluginCleanupAsync();
                        retryConversion = true;
                        continue;
                    }

                    await RunRepairAsync(converter);
                }
                finally
                {
                    _cts?.Dispose();
                    _cts = null;
                    StartBtn.IsEnabled = true;
                    TargetServerCombo.IsEnabled = true;
                    CancelBtn.IsEnabled = false;
                }
            } while (retryConversion);
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            CancelBtn.IsEnabled = false;
            _cts?.Cancel();
        }

        private void UpdateProgress(GameServerConversionProgress p)
        {
            _chartController?.UpdateProgress(p);
            _remainingChunksTracker.Update(p);
            RemainingCountText.Text = string.Format("AdvancedServerSwitch_RemainingChunks".GetLocalized(), _remainingChunksTracker.Chunks.Count);

            StatusText.Text = p.ChunkName is null
                ? p.Stage
                : string.Format("AdvancedServerSwitch_Progress".GetLocalized(), p.DoneChunks, p.TotalChunks, p.ChunkName);

            if (p.TotalChunks > 0)
            {
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Maximum = p.TotalChunks;
                ProgressBar.Value = p.DoneChunks;
                ProgressDetailText.Text = p.TotalBytes > 0
                    ? $"{p.DoneBytes / 1048576.0:F1} MB / {p.TotalBytes / 1048576.0:F1} MB ({p.Percent:F1}%)"
                    : $"{p.Percent:F1}%";
            }
            else
            {
                ProgressBar.IsIndeterminate = true;
                ProgressDetailText.Text = string.Empty;
            }
        }

        private void AppendStatus(string message)
        {
            DispatcherQueue.TryEnqueue(() => StatusText.Text = message);
        }

        private string? TryDeletePluginsFolders()
        {
            string? lastError = null;
            foreach (string dataDir in new[] { GameConstants.CN_DATA_DIR, GameConstants.OS_DATA_DIR })
            {
                string pluginsDir = Path.Combine(_gameDir, dataDir, "Plugins");
                try
                {
                    if (Directory.Exists(pluginsDir))
                    {
                        Directory.Delete(pluginsDir, true);
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }
            }

            return lastError;
        }

        private async Task TryManualPluginCleanupAsync()
        {
            string targetPluginDir = Directory.Exists(Path.Combine(_gameDir, GameConstants.OS_DATA_DIR))
                ? Path.Combine(_gameDir, GameConstants.OS_DATA_DIR, "Plugins")
                : Path.Combine(_gameDir, GameConstants.CN_DATA_DIR, "Plugins");

            if (!Directory.Exists(targetPluginDir))
            {
                Directory.CreateDirectory(targetPluginDir);
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = targetPluginDir,
                UseShellExecute = true,
                Verb = "open"
            });

            var monitorTextBlock = new TextBlock
            {
                Text = "AdvancedServerSwitch_ManualCleanMsg".GetLocalized(),
                TextWrapping = TextWrapping.Wrap
            };

            var manualDeleteDialog = new ContentDialog
            {
                Title = "AdvancedServerSwitch_ManualCleanTitle".GetLocalized(),
                Content = monitorTextBlock,
                PrimaryButtonText = "AdvancedServerSwitch_ManualCleanConfirm".GetLocalized(),
                CloseButtonText = "CancelBtn".GetLocalized(),
                XamlRoot = XamlRoot
            };

            using var monitorCts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!monitorCts.Token.IsCancellationRequested)
                    {
                        bool isClean = true;
                        string cnPlugins = Path.Combine(_gameDir, GameConstants.CN_DATA_DIR, "Plugins");
                        string osPlugins = Path.Combine(_gameDir, GameConstants.OS_DATA_DIR, "Plugins");
                        if (Directory.Exists(cnPlugins) && Directory.GetFileSystemEntries(cnPlugins).Length > 0) isClean = false;
                        if (Directory.Exists(osPlugins) && Directory.GetFileSystemEntries(osPlugins).Length > 0) isClean = false;

                        if (isClean)
                        {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                monitorTextBlock.Text = "AdvancedServerSwitch_ManualCleanDone".GetLocalized();
                            });
                            break;
                        }

                        await Task.Delay(500, monitorCts.Token);
                    }
                }
                catch
                {
                    // ignored
                }
            }, monitorCts.Token);

            await manualDeleteDialog.ShowAsync();
            monitorCts.Cancel();
        }

        private async Task RunRepairAsync(GameServerConverter converter)
        {
            ProgressPanel.Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = true;
            ProgressDetailText.Text = string.Empty;
            CancelBtn.IsEnabled = true;
            AppendStatus("AdvancedServerSwitch_Repairing".GetLocalized());
            _chartController!.Start();
            _remainingChunksTracker.Reset();
            RemainingCountText.Text = string.Format("AdvancedServerSwitch_RemainingChunks".GetLocalized(), 0);

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;
            var progress = new Progress<GameServerConversionProgress>(p => DispatcherQueue.TryEnqueue(() => UpdateProgress(p)));

            try
            {
                await Task.Run(() => converter.VerifyAndRepairAsync(_gameDir, _currentScheme!, progress, AppendStatus, token, _downloadMonitor));

                _chartController.Stop();
                ProgressPanel.Visibility = Visibility.Collapsed;

                var repairSuccessDialog = new ContentDialog
                {
                    Title = "AdvancedServerSwitch_RepairDone".GetLocalized(),
                    Content = "AdvancedServerSwitch_RepairDoneMsg".GetLocalized(),
                    CloseButtonText = "OkBtn".GetLocalized(),
                    XamlRoot = XamlRoot
                };
                await repairSuccessDialog.ShowAsync();
            }
            catch (OperationCanceledException)
            {
                _chartController.SetPaused();
                ProgressPanel.Visibility = Visibility.Collapsed;
                AppendStatus("AdvancedServerSwitch_Cancelled".GetLocalized());
            }
            catch (Exception repairEx)
            {
                _chartController.SetFailed();
                ProgressPanel.Visibility = Visibility.Collapsed;
                await ShowMessageAsync(
                    string.Format("AdvancedServerSwitch_RepairFailedMsg".GetLocalized(), repairEx.Message),
                    "AdvancedServerSwitch_RepairFailed".GetLocalized());
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                CancelBtn.IsEnabled = false;
            }
        }

        private async Task ShowMessageAsync(string message, string? title = null)
        {
            var dialog = new ContentDialog
            {
                Title = title ?? "ErrorTitle".GetLocalized(),
                Content = message,
                CloseButtonText = "OkBtn".GetLocalized(),
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
