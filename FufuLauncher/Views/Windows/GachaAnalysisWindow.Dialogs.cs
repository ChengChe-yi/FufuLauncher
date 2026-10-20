/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using FufuLauncher.Helpers;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher.Views;

public sealed partial class GachaAnalysisWindow
{
    private async void OnAboutButtonClick(object sender, RoutedEventArgs e)
    {
        var contentPanel = new StackPanel { Spacing = 8 };

        contentPanel.Children.Add(new TextBlock
        {
            Text = "该项目使用UIGF v4.2/v4.1/v4.0/v3.0/v2.4/v2.3/v2.2标准格式处理祈愿数据",
            TextWrapping = TextWrapping.Wrap
        });

        contentPanel.Children.Add(new HyperlinkButton
        {
            Content = "UIGF-Org",
            NavigateUri = new Uri(ApiEndpoints.UigfHomeUrl)
        });

        var badgePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 4, 0, 0)
        };

        badgePanel.Children.Add(new FontIcon
        {
            Glyph = "\uE734",
            FontSize = 16,
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"]
        });

        badgePanel.Children.Add(new TextBlock
        {
            Text = "已获UIGF/UIAF标准项目合作",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        contentPanel.Children.Add(badgePanel);

        var dialog = new ContentDialog
        {
            Title = "关于",
            Content = contentPanel,
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async void OnDeleteGachaDataClick(object sender, RoutedEventArgs e)
    {
        var deleteUid = ViewModel.SelectedUid;
        if (string.IsNullOrEmpty(ViewModel.SelectedUid))
        {
            var noDataDialog = new ContentDialog
            {
                Title = "提示",
                Content = "当前没有选中任何账号，无法删除记录。",
                CloseButtonText = "知道了",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            await noDataDialog.ShowAsync();
            return;
        }

        ContentDialog deleteDialog = new()
        {
            Title = "警告",
            Content = $"确定要删除 UID: {ViewModel.SelectedUid} 的所有抽卡记录吗？\n此操作不可逆转！",
            PrimaryButtonText = "确认删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        ContentDialogResult result = await deleteDialog.ShowAsync();
        if (result == ContentDialogResult.Primary) await ViewModel.ClearGachaDataAsync(deleteUid);
    }

    private async void OnMiYouSheLoginClick(object sender, RoutedEventArgs e)
    {
        await PerformMiYouSheFetchAsync(incremental: false);
    }

    private async void OnMiYouSheFullUpdateClick(object sender, RoutedEventArgs e)
    {
        await PerformMiYouSheFetchAsync(incremental: false);
    }

    private async void OnMiYouSheIncrementalUpdateClick(object sender, RoutedEventArgs e)
    {
        await PerformMiYouSheFetchAsync(incremental: true);
    }

    private async Task PerformMiYouSheFetchAsync(bool incremental)
    {
        if (ViewModel.IsFetching || ViewModel.IsScraping) return;
        var accountManager = App.GetService<AccountManager>();
        var hasCnAccount = accountManager.GetAllAccounts().Any(a => a.ServerType == "cn");

        if (!hasCnAccount && accountManager.GetActiveAccountEntry()?.ServerType == "os")
        {
            var osDialog = new ContentDialog
            {
                Title = "国际服暂不支持",
                Content = "国际服（HoYoLAB）账号的抽卡记录获取功能正在适配中，敬请期待。\n\n国际服用户可通过「通过URL获取」方式导入抽卡记录。",
                CloseButtonText = "知道了",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            await osDialog.ShowAsync();
            return;
        }

        if (!hasCnAccount)
        {
            var dialog = new ContentDialog
            {
                Title = "未登录米游社",
                Content = "检测到尚未登录米游社账号，即将跳转到账户设置页面进行登录。",
                CloseButtonText = "知道了",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();

            if (App.MainWindow is MainWindow mainWindow)
                await mainWindow.NavigateToAccountPageAsync();
            return;
        }

        await ViewModel.FetchFromMiYouSheCommand.ExecuteAsync(incremental);
    }

    private async void OnGameCacheFetchClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsFetching || ViewModel.IsScraping) return;
        var dialog = new ContentDialog
        {
            Title = "GachaAnalysis_GetViaGame".GetLocalized(),
            Content = new TextBlock
            {
                Text = "GachaAnalysis_CacheInstructions".GetLocalized(),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 440
            },
            PrimaryButtonText = "GachaAnalysis_CacheStart".GetLocalized(),
            CloseButtonText = "CancelBtn".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.FetchFromGameCacheCommand.ExecuteAsync(null);
    }

    private async void OnUrlFetchClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsFetching || ViewModel.IsScraping) return;
        var urlBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 120,
            Text = ViewModel.GachaUrl ?? "",
            PlaceholderText = "在此处粘贴抽卡分析链接 (URL)..."
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "粘贴从米游社或其他工具获取的抽卡记录链接，链接应包含 authkey 参数。",
            FontSize = 12,
            Foreground =
                (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(urlBox);

        var dialog = new ContentDialog
        {
            Title = "通过 URL 获取抽卡记录",
            Content = panel,
            PrimaryButtonText = "开始获取",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.GachaUrl = urlBox.Text;
            ViewModel.FetchGachaDataCommand.Execute(null);
        }
    }
}