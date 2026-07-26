/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Reflection;
using FufuLauncher.Models;
using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace FufuLauncher.Views;

public sealed partial class PluginStorePage : Page
{
    public PluginStoreViewModel ViewModel { get; }

    private static readonly string CurrentAppVersion =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0.0";

    public PluginStorePage()
    {
        ViewModel = App.GetService<PluginStoreViewModel>();
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (FindName("EntranceStoryboard") is Storyboard sb)
            sb.Begin();

        if (ViewModel.Plugins.Count == 0)
        {
            await ViewModel.InitializeAsync();
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (ViewModel.Plugins.Count == 0)
        {
            await ViewModel.InitializeAsync();
        }
    }

    private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ViewModel.LoadPluginsAsync();
        }
    }
    
    private async void OnUploadPluginClick(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(new Uri("https://fu1.fun/dev-add"));
    }

    private async void OnAddPrivatePluginClick(object sender, RoutedEventArgs e)
    {
        ViewModel.AddPrivatePluginCommand.Execute(null);
    }

    private void OnLuaTestClick(object sender, RoutedEventArgs e)
    {
        ViewModel.LuaTestCommand.Execute(null);
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private async void OnSortClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string sortMode)
        {
            SortLabel.Text = item.Text;
            ViewModel.SortMode = sortMode;
            await ViewModel.LoadPluginsAsync();
        }
    }

    private async void OnCategoryClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PluginStoreCategory category)
        {
            ViewModel.SelectedCategory = category;
            await ViewModel.LoadPluginsAsync();
        }
    }

    private async void OnPrevPageClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CanGoPrev)
            await ViewModel.GoToPageAsync(ViewModel.CurrentPage - 1);
    }

    private async void OnNextPageClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CanGoNext)
            await ViewModel.GoToPageAsync(ViewModel.CurrentPage + 1);
    }

    private void OnInstallButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PluginStoreItem item)
        {
            ViewModel.InstallCommand.Execute(item);
        }
    }

    private void OnCancelInstallClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PluginStoreItem item)
        {
            ViewModel.CancelInstallCommand.Execute(item);
        }
    }

    private async void OnInstalledButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PluginStoreItem item)
        {
            await ShowPluginDetailDialogAsync(item);
        }
    }

    private async void OnPluginItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PluginStoreItem item)
        {
            await ShowPluginDetailDialogAsync(item);
        }
    }

    private async Task ShowPluginDetailDialogAsync(PluginStoreItem item)
    {
        var infoPanel = new StackPanel { Spacing = 16, Padding = new Thickness(0, 12, 0, 12) };
        
        infoPanel.Children.Add(new TextBlock
        {
            Text = item.Description,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Opacity = 0.9,
            LineHeight = 22,
            Margin = new Thickness(0, 0, 0, 8)
        });
        
        infoPanel.Children.Add(CreateInfoRow("版本", item.VersionDisplay));
        infoPanel.Children.Add(CreateInfoRow("开发者", item.Developer));
        infoPanel.Children.Add(CreateInfoRow("大小", item.SizeDisplay));
        infoPanel.Children.Add(CreateInfoRow("下载量", item.DownloadsDisplay));
        
        if (item.HasUpdateType)
        {
            infoPanel.Children.Add(CreateInfoRow("更新类型", item.UpdateTypeDisplay));
        }
        
        if (!string.IsNullOrWhiteSpace(item.MinAppVersion))
        {
            var versionSatisfied = IsVersionSatisfied(CurrentAppVersion, item.MinAppVersion);
            var versionRow = CreateInfoRow("最低启动器版本", $"v{item.MinAppVersion}");
            if (!versionSatisfied)
            {
                var valueBlock = versionRow.Children[1] as TextBlock;
                if (valueBlock != null)
                {
                    valueBlock.Text = $"v{item.MinAppVersion} ⚠ 当前版本 {CurrentAppVersion} 过低";
                    valueBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
                }
            }
            infoPanel.Children.Add(versionRow);
        }
        
        if (item.IsPrivate)
        {
            var privateRow = CreateInfoRow("可见性", "私密插件");
            infoPanel.Children.Add(privateRow);
        }
        
        if (item.HasDependencies)
        {
            infoPanel.Children.Add(new TextBlock
            {
                Text = "依赖",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 16, 0, -8)
            });

            var depsPanel = new StackPanel { Spacing = 8 };
            foreach (var dep in item.Dependencies.Where(d => !d.IsEmpty))
            {
                var depGrid = new Grid();
                depGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var depText = new TextBlock
                {
                    Text = dep.ToString(),
                    FontSize = 13,
                    Opacity = 0.75,
                    TextWrapping = TextWrapping.Wrap
                };
                depGrid.Children.Add(depText);
                depsPanel.Children.Add(depGrid);
            }
            infoPanel.Children.Add(depsPanel);
        }
        
        if (!string.IsNullOrEmpty(item.LongDescription))
        {
            infoPanel.Children.Add(new TextBlock
            {
                Text = "详细介绍",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 16, 0, -8)
            });

            infoPanel.Children.Add(new TextBlock
            {
                Text = item.LongDescription,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Opacity = 0.75,
                LineHeight = 20,
                Margin = new Thickness(0, 8, 0, 0),
                MaxHeight = 250
            });
        }

        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 500,
            Content = infoPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var isInstalledOrUpdate = item.State == StorePluginState.Installed || item.State == StorePluginState.UpdateAvailable;
        var isUpdate = item.State == StorePluginState.UpdateAvailable;

        var dialog = new ContentDialog
        {
            Title = item.Name,
            Content = scrollViewer,
            PrimaryButtonText = isUpdate ? "立即更新" : (isInstalledOrUpdate ? "卸载" : "安装插件"),
            SecondaryButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            if (isUpdate)
            {
                ViewModel.InstallCommand.Execute(item);
            }
            else if (isInstalledOrUpdate)
            {
                ViewModel.UninstallCommand.Execute(item);
            }
            else
            {
                ViewModel.InstallCommand.Execute(item);
            }
        }
    }

    private static Grid CreateInfoRow(string label, string value)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 13,
            Opacity = 0.55,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelBlock, 0);

        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(valueBlock, 1);

        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);

        return grid;
    }

    private static bool IsVersionSatisfied(string currentVersion, string minVersion)
    {
        try
        {
            var cur = new Version(currentVersion);
            var min = new Version(minVersion);
            return cur >= min;
        }
        catch
        {
            return true;
        }
    }
}
