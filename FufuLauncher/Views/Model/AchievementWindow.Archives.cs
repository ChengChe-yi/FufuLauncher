/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.Data.Sqlite;
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class AchievementWindow
{
    private async void OnArchiveManageClick(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_profileRecordPath) || CurrentProfileName == "AchievementWindow_UnnamedProfile".GetLocalized())
        {
            var nameResult = await ShowInputAsync("保存当前存档", "检测到当前存档未命名，在切换或新建前，请先为当前进度取一个名字：");
            if (string.IsNullOrWhiteSpace(nameResult)) return;

            SaveData();

            _achievementRepo.ChangeDatabase(_workFilePath);
            SqliteConnection.ClearAllPools();

            string targetPath = Path.Combine(_archivesDir, $"{nameResult}.db");
            File.Copy(_workFilePath, targetPath, true);
            File.WriteAllText(_profileRecordPath, nameResult);
            CurrentProfileName = nameResult;
            await ShowDialogAsync("保存成功", $"当前进度已保存为：{nameResult}");
        }
        else
        {
            SaveData();

            _achievementRepo.ChangeDatabase(_workFilePath);
            SqliteConnection.ClearAllPools();

            string currentBackupPath = Path.Combine(_archivesDir, $"{CurrentProfileName}.db");
            File.Copy(_workFilePath, currentBackupPath, true);
        }

        var rootPanel = new StackPanel { Spacing = 12, MinWidth = 340 };

        var btnContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        btnContent.Children.Add(new FontIcon { Glyph = "\uE710", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"), FontSize = 12 });
        btnContent.Children.Add(new TextBlock { Text = "AchievementWindow_NewBlank".GetLocalized() });
        var createBtn = new Button { Content = btnContent, HorizontalAlignment = HorizontalAlignment.Stretch };

        var listHeader = new TextBlock { Text = "AchievementWindow_ExistingProfiles".GetLocalized(), Opacity = 0.7, FontSize = 12, Margin = new Thickness(0, 10, 0, 0) };

        var listContainer = new StackPanel { Spacing = 8 };
        var scrollViewer = new ScrollViewer { Content = listContainer, MaxHeight = 250, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        rootPanel.Children.Add(createBtn);
        rootPanel.Children.Add(listHeader);
        rootPanel.Children.Add(scrollViewer);

        var dialog = new ContentDialog
        {
            Title = "AchievementWindow_ArchiveManagement".GetLocalized(),
            Content = rootPanel,
            CloseButtonText = "CloseBtn".GetLocalized(),
            XamlRoot = Content.XamlRoot
        };

        void RefreshList()
        {
            listContainer.Children.Clear();

            var files = Directory.GetFiles(_archivesDir, "*.db")
                                 .Select(Path.GetFileNameWithoutExtension)
                                 .OrderBy(x => x)
                                 .ToList();

            if (files.Count == 0)
            {
                listContainer.Children.Add(new TextBlock
                {
                    Text = "AchievementWindow_NoBackup".GetLocalized(),
                    Opacity = 0.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                });
            }

            foreach (var file in files)
            {
                var itemGrid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    },
                    Margin = new Thickness(0, 0, 0, 4)
                };

                var switchBtn = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(10, 255, 255, 255)),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(10, 8, 10, 8)
                };

                bool isCurrent = file == CurrentProfileName;
                string displayText = isCurrent ? $"{file} (当前)" : file;

                var txtBlock = new TextBlock { Text = displayText, VerticalAlignment = VerticalAlignment.Center };
                if (isCurrent)
                {
                    txtBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 255, 100));
                    txtBlock.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                }

                switchBtn.Content = txtBlock;
                switchBtn.Click += async (_, _) =>
                {
                    if (isCurrent) return;
                    dialog.Hide();
                    await SwitchToArchive(file, false);
                };

                var deleteBtn = new Button
                {
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8, 5, 8, 5),
                    Margin = new Thickness(4, 0, 0, 0)
                };

                ToolTipService.SetToolTip(deleteBtn, "删除此存档");

                deleteBtn.Content = new FontIcon
                {
                    Glyph = "\uE74D",
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    FontSize = 14,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 80, 80))
                };

                if (isCurrent)
                {
                    deleteBtn.IsEnabled = false;
                    deleteBtn.Opacity = 0.3;
                }
                else
                {
                    var confirmPanel = new StackPanel { Spacing = 10, Padding = new Thickness(10) };
                    confirmPanel.Children.Add(new TextBlock { Text = "AchievementWindow_ConfirmDeleteMsg".GetLocalized(), FontSize = 12 });

                    var confirmDeleteBtn = new Button
                    {
                        Content = "确认删除",
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 50, 50)),
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        FontSize = 12
                    };

                    confirmPanel.Children.Add(confirmDeleteBtn);

                    var flyout = new Flyout { Content = confirmPanel };
                    deleteBtn.Flyout = flyout;

                    confirmDeleteBtn.Click += (_, _) =>
                    {
                        try
                        {
                            string pathToDelete = Path.Combine(_archivesDir, $"{file}.db");
                            if (File.Exists(pathToDelete))
                            {
                                File.Delete(pathToDelete);
                            }
                            flyout.Hide();
                            RefreshList();
                        }
                        catch (Exception)
                        {
                            flyout.Hide();
                        }
                    };
                }

                Grid.SetColumn(switchBtn, 0);
                Grid.SetColumn(deleteBtn, 1);
                itemGrid.Children.Add(switchBtn);
                itemGrid.Children.Add(deleteBtn);

                listContainer.Children.Add(itemGrid);
            }
        }

        RefreshList();

        createBtn.Click += async (_, _) =>
        {
            dialog.Hide();
            var newName = await ShowInputAsync("新建存档", "请输入新存档的名称：");
            if (string.IsNullOrWhiteSpace(newName)) return;

            if (File.Exists(Path.Combine(_archivesDir, $"{newName}.db")))
            {
                await ShowDialogAsync("ErrorTitle".GetLocalized(), "该存档名称已存在！");
                return;
            }

            await SwitchToArchive(newName, true);
        };

        await dialog.ShowAsync();
    }

    private async Task SwitchToArchive(string profileName, bool isNew)
    {
        try
        {
            ViewModel.IsLoading = true;
            ViewModel.StatusMessage = "AchievementWindow_SwitchingProfile".GetLocalized();

            _achievementRepo.ChangeDatabase(_workFilePath);

            // 释放连接池中的文件句柄，并清除迁移缓存，防止文件被占用
            SqliteConnection.ClearAllPools();
            _achievementRepo.InvalidateMigrationCache(_workFilePath);

            if (isNew)
            {
                if (File.Exists(_workFilePath)) File.Delete(_workFilePath);
                EnsureDatabaseExists(_workFilePath);
            }
            else
            {
                string sourceArchive = Path.Combine(_archivesDir, $"{profileName}.db");
                if (!File.Exists(sourceArchive))
                {
                    await ShowDialogAsync("ErrorTitle".GetLocalized(), "找不到目标存档文件！");
                    return;
                }
                File.Copy(sourceArchive, _workFilePath, true);
            }

            File.WriteAllText(_profileRecordPath, profileName);
            CurrentProfileName = profileName;

            LoadData();

            if (isNew)
            {
                string newBackupPath = Path.Combine(_archivesDir, $"{profileName}.db");
                File.Copy(_workFilePath, newBackupPath, true);
            }

            ViewModel.StatusMessage = $"已切换至：{profileName}";
        }
        catch (Exception ex)
        {
            await ShowDialogAsync("切换失败", ex.Message);
            LoadData();
        }
    }
}
