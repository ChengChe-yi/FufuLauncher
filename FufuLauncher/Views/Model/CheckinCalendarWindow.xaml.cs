/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MihoyoBBS;

namespace FufuLauncher.Views
{
    public sealed partial class CheckinCalendarWindow : Window
    {
        public ObservableCollection<CalendarRewardItem> Rewards { get; } = new();

        public CheckinCalendarWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            SystemBackdrop = new MicaBackdrop();
            AppWindow.Resize(new Windows.Graphics.SizeInt32(680, 720));

            _ = LoadCalendarDataAsync();
        }

        private async Task LoadCalendarDataAsync()
        {
            
            var accountManager = App.GetService<AccountManager>();
            var activeId = accountManager.ActiveAccountId;
            if (activeId == null) return;
            var cookies = await accountManager.LoadCookiesAsync(activeId);
            if (cookies == null || cookies.Count == 0) return;

            
            var checkinService = App.GetService<IHoyoverseCheckinService>();
            var entry = accountManager.GetActiveAccountEntry();
            if (entry == null) return;

        
            var calendarData = await checkinService.GetCalendarDataAsync(cookies, entry.ServerType);  
            if (calendarData != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    TitleText.Text = $"{calendarData.Month}月 签到奖励日历";
                    Rewards.Clear();
                    foreach (var item in calendarData.Awards)
                        Rewards.Add(item);
                    CalendarGridView.ItemsSource = Rewards;
                });
            }
        }
        
        private async void ResignButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var accountManager = App.GetService<AccountManager>();
                var activeId = accountManager.ActiveAccountId;
                if (activeId == null) return;

                var cookies = await accountManager.LoadCookiesAsync(activeId);
                var entry = accountManager.GetActiveAccountEntry();
                if (cookies == null || entry == null) return;

                var checkinService = App.GetService<IHoyoverseCheckinService>();
                bool isOs = entry.ServerType == "os";

                var uids = await checkinService.GetBoundUidsAsync(cookies, entry.ServerType);
                if (uids.Count == 0)
                {
                    await ShowMessageAsync("Checkin_NoBoundAccount".GetLocalized());
                    return;
                }

                string uid = uids[0];
                ResignButton.IsEnabled = false;
                try
                {
                    var resignInfo = await checkinService.GetResignInfoAsync(uid, cookies, entry.ServerType);
                    if (resignInfo == null)
                    {
                        string lastError = isOs ? HoyolabCheckinService.LastApiError : MihoyoBBS.GameCheckin.LastApiError;
                        await ShowMessageAsync(string.IsNullOrEmpty(lastError)
                            ? "Checkin_ResignQueryFailed".GetLocalized()
                            : lastError);
                        return;
                    }

                    string confirmKey = isOs ? "Checkin_ResignConfirmFormat" : "Checkin_ResignConfirmFormatCn";
                    int cost = isOs ? resignInfo.QualityCount : resignInfo.CoinCost;

                    var confirmDialog = new ContentDialog
                    {
                        Title = "Checkin_ResignTitle".GetLocalized(),
                        Content = string.Format(confirmKey.GetLocalized(), resignInfo.RemainingMonthly, cost),
                        PrimaryButtonText = "OkBtn".GetLocalized(),
                        CloseButtonText = "CancelBtn".GetLocalized(),
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = Content.XamlRoot
                    };

                    if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
                        return;

                    var (success, message) = await checkinService.ExecuteResignAsync(uid, cookies, entry.ServerType);
                    await ShowMessageAsync(message);
                    if (success)
                    {
                        await LoadCalendarDataAsync();
                    }
                }
                finally
                {
                    ResignButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CheckinCalendar] 补签异常: {ex.Message}");
            }
        }

        private async Task ShowMessageAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Content = message,
                CloseButtonText = "OkBtn".GetLocalized(),
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
