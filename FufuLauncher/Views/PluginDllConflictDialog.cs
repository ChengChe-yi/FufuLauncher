/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Helpers;
using FufuLauncher.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher.Views;

internal static class PluginDllConflictDialog
{
    internal static async Task<bool> ShowAsync(IReadOnlyList<PluginDllConflict> conflicts)
    {
        if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot) return false;

        var dialog = new ContentDialog
        {
            Title = "PluginDllConflict_Title".GetLocalized(),
            Content = BuildContent(conflicts),
            PrimaryButtonText = "PluginPage_OpenPluginFolderBtn".GetLocalized(),
            CloseButtonText = "CloseBtn".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            Resources = { ["ContentDialogMaxWidth"] = 760.0 }
        };

        var (shown, result) = await ShowWithRetryAsync(dialog);

        if (shown && result == ContentDialogResult.Primary)
        {
            OpenFolder(LightweightPluginService.PluginsDir);
        }

        return shown;
    }

    private static ScrollViewer BuildContent(IReadOnlyList<PluginDllConflict> conflicts)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 520 };

        panel.Children.Add(new TextBlock
        {
            Text = "PluginDllConflict_Message".GetLocalized(),
            TextWrapping = TextWrapping.Wrap
        });

        foreach (var conflict in conflicts)
        {
            panel.Children.Add(BuildConflictCard(conflict));
        }

        panel.Children.Add(new TextBlock
        {
            Text = "PluginDllConflict_Hint".GetLocalized(),
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap
        });

        return new ScrollViewer
        {
            Content = panel,
            MaxHeight = 460,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private static Border BuildConflictCard(PluginDllConflict conflict)
    {
        var panel = new StackPanel { Spacing = 10 };

        panel.Children.Add(new TextBlock
        {
            Text = string.Format("PluginDllConflict_DllNameFormat".GetLocalized(), conflict.DllName),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        for (var i = 0; i < conflict.Candidates.Count; i++)
        {
            panel.Children.Add(BuildCandidateRow(conflict.Candidates[i], i == 0));
        }

        return new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(6),
            Background = TryGetBrush("CardBackgroundFillColorDefaultBrush"),
            Child = panel
        };
    }

    private static Grid BuildCandidateRow(PluginDllCandidate candidate, bool isNewest)
    {
        var accent = TryGetBrush(isNewest ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush")
                     ?? new SolidColorBrush(isNewest ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Red);

        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = new TextBlock
        {
            Text = (isNewest ? "PluginDllConflict_NewestBadge" : "PluginDllConflict_OlderBadge").GetLocalized(),
            Foreground = accent,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(badge);

        var info = new StackPanel { Spacing = 2 };
        info.Children.Add(new TextBlock
        {
            Text = candidate.FilePath,
            Foreground = accent,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        });
        info.Children.Add(new TextBlock
        {
            Text = candidate.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
            FontSize = 12,
            Opacity = 0.7
        });
        Grid.SetColumn(info, 1);
        row.Children.Add(info);

        var locateButton = new Button
        {
            Content = "PluginDllConflict_LocateBtn".GetLocalized(),
            VerticalAlignment = VerticalAlignment.Center
        };
        locateButton.Click += (_, _) => RevealInExplorer(candidate.FilePath);
        Grid.SetColumn(locateButton, 2);
        row.Children.Add(locateButton);

        return row;
    }

    private static async Task<(bool Shown, ContentDialogResult Result)> ShowWithRetryAsync(ContentDialog dialog)
    {
        try
        {
            return (true, await dialog.ShowAsync());
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            await Task.Delay(300);

            try
            {
                return (true, await dialog.ShowAsync());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginDllConflictDialog] 对话框显示失败: {ex.Message}");
                return (false, ContentDialogResult.None);
            }
        }
    }

    private static void RevealInExplorer(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginDllConflictDialog] 定位文件失败: {ex.Message}");
        }
    }

    private static void OpenFolder(string folderPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo(folderPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginDllConflictDialog] 打开插件目录失败: {ex.Message}");
        }
    }

    private static Brush? TryGetBrush(string key)
    {
        try
        {
            return Application.Current.Resources.TryGetValue(key, out var brush) ? brush as Brush : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginDllConflictDialog] 读取主题画刷失败: {ex.Message}");
            return null;
        }
    }
}
