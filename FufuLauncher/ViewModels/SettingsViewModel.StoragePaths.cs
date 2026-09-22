/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 存储路径

    [ObservableProperty] private string _dataPath = AppPaths.DataDir;
    [ObservableProperty] private string _cachePath = AppPaths.CacheDir;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPathError))]
    private string _pathError = string.Empty;

    public bool HasPathError => !string.IsNullOrEmpty(PathError);

    [RelayCommand]
    private async Task ChangeDataPathAsync()
    {
        await ChangeStoragePathAsync(isDataPath: true);
    }

    [RelayCommand]
    private async Task ChangeCachePathAsync()
    {
        await ChangeStoragePathAsync(isDataPath: false);
    }

    private async Task ChangeStoragePathAsync(bool isDataPath)
    {
        try
        {
            var folder = await _filePickerService.PickFolderAsync();
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            PathError = string.Empty;

            var newDataPath = isDataPath ? folder : DataPath;
            var newCachePath = isDataPath ? CachePath : folder;

            if (IsSamePath(newDataPath, DataPath) && IsSamePath(newCachePath, CachePath))
            {
                return;
            }

            var validationError = AppPaths.ValidateCustomPaths(newDataPath, newCachePath);
            if (validationError != null)
            {
                PathError = validationError;
                return;
            }

            AppPaths.SaveCustomPaths(newDataPath, newCachePath);

            DataPath = AppPaths.DataDir;
            CachePath = AppPaths.CacheDir;

            var dialog = new ContentDialog
            {
                Title = "StoragePath_RestartTitle".GetLocalized(),
                Content = "StoragePath_RestartMessage".GetLocalized(),
                PrimaryButtonText = "RestartNowBtn".GetLocalized(),
                CloseButtonText = "RestartLaterBtn".GetLocalized(),
                XamlRoot = App.MainWindow.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                RestartApp();
            }
        }
        catch (Exception ex)
        {
            PathError = string.Format("StoragePath_Error_SaveFailed".GetLocalized(), ex.Message);
            Debug.WriteLine($"更改存储路径失败: {ex.Message}");
        }
    }

    private static bool IsSamePath(string path1, string path2)
    {
        if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(path1).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(path2).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void RefreshStoragePaths()
    {
        DataPath = AppPaths.DataDir;
        CachePath = AppPaths.CacheDir;
        PathError = string.Empty;
    }

    #endregion
}
