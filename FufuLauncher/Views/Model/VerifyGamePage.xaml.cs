/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using FufuLauncher.Helpers;
using FufuLauncher.Models.GameServer;
using FufuLauncher.Services.GameServer;

namespace FufuLauncher.Views
{
    public sealed partial class VerifyGamePage : Page
    {
        private string _gameDir = string.Empty;
        private Window? _parentWindow;
        private CancellationTokenSource? _cts;

        private readonly GameServerDownloadMonitor _downloadMonitor = new();
        private readonly RemainingChunksTracker _remainingChunksTracker = new();
        private DownloadSpeedChartController? _chartController;

        public VerifyGamePage()
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
            }
        }

        private async void StartVerifyBtn_Click(object sender, RoutedEventArgs e)
        {
            var configurationService = App.GetService<GameServerConfigurationService>();
            var currentScheme = configurationService.TryDetectCurrentScheme(_gameDir);
            if (currentScheme is null)
            {
                await ShowMessageAsync("AdvancedServerSwitch_UnknownCurrent".GetLocalized());
                return;
            }

            StartVerifyBtn.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = true;
            ProgressDetailText.Text = string.Empty;
            CancelBtn.IsEnabled = true;
            _chartController!.Start();
            _remainingChunksTracker.Reset();
            RemainingCountText.Text = string.Format("AdvancedServerSwitch_RemainingChunks".GetLocalized(), 0);

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;
            var progress = new Progress<GameServerConversionProgress>(p => DispatcherQueue.TryEnqueue(() => UpdateProgress(p)));

            try
            {
                await Task.Run(() => App.GetService<GameServerConverter>().VerifyAndRepairAsync(_gameDir, currentScheme, progress, UpdateStatus, token, _downloadMonitor));

                _chartController.Stop();
                ProgressPanel.Visibility = Visibility.Collapsed;

                var successDialog = new ContentDialog
                {
                    Title = "AdvancedServerSwitch_Done".GetLocalized(),
                    Content = "VerifyGame_Success".GetLocalized(),
                    CloseButtonText = "OkBtn".GetLocalized(),
                    XamlRoot = XamlRoot
                };
                await successDialog.ShowAsync();

                _parentWindow?.Close();
            }
            catch (OperationCanceledException)
            {
                _chartController.SetPaused();
                ProgressPanel.Visibility = Visibility.Collapsed;
                UpdateStatus("AdvancedServerSwitch_Cancelled".GetLocalized());
            }
            catch (Exception ex)
            {
                _chartController.SetFailed();
                ProgressPanel.Visibility = Visibility.Collapsed;
                await ShowMessageAsync(ex.Message, "VerifyGame_Failed".GetLocalized());
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                StartVerifyBtn.IsEnabled = true;
                CancelBtn.IsEnabled = false;
            }
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

        private void UpdateStatus(string message)
        {
            DispatcherQueue.TryEnqueue(() => StatusText.Text = message);
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
