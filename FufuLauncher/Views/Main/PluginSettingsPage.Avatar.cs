/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Messages;
using FufuLauncher.Services;
using FufuLauncher.Helpers;

namespace FufuLauncher.Views;

public sealed partial class PluginSettingsPage
{
    private int _currentEditSize = 512;

    private async void OnImportAvatarClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && int.TryParse(fe.Tag?.ToString(), out int size))
            {
                _currentEditSize = size;
            }

            var path = await FilePickerService.PickOpenFileAsync(
                null,
                new[] { ("图片文件", new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp" }) },
                Windows.Storage.Pickers.PickerLocationId.PicturesLibrary,
                msg => WeakReferenceMessenger.Default.Send(new NotificationMessage("Avatar_ImportFail_Title".GetLocalized(), msg, NotificationType.Error)));
            if (!string.IsNullOrEmpty(path))
            {
                string avatarDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "Avatar");
                if (!Directory.Exists(avatarDir)) Directory.CreateDirectory(avatarDir);

                string originalPath = ViewModel.GetAvatarOriginalPath(_currentEditSize);
                File.Copy(path, originalPath, true);

                await LoadImageToCropperAsync(originalPath);
            }
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage("Avatar_ImportFail_Title".GetLocalized(), ex.Message, NotificationType.Error));
        }
    }

    private async void OnEditCurrentAvatarClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && int.TryParse(fe.Tag?.ToString(), out int size))
        {
            _currentEditSize = size;
            string originalPath = ViewModel.GetAvatarOriginalPath(size);
            string normalPath = ViewModel.GetAvatarPath(size);
            string targetPath = File.Exists(originalPath) ? originalPath : normalPath;

            if (File.Exists(targetPath))
            {
                await LoadImageToCropperAsync(targetPath);
            }
            else
            {
                WeakReferenceMessenger.Default.Send(new NotificationMessage("Avatar_EditNotFound_Title".GetLocalized(), "Avatar_EditNotFound_Content".GetLocalized(), NotificationType.Warning));
            }
        }
    }

    private void OnClearAvatarClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && int.TryParse(fe.Tag?.ToString(), out int size))
            {
                string normalPath = ViewModel.GetAvatarPath(size);
                string originalPath = ViewModel.GetAvatarOriginalPath(size);

                if (File.Exists(normalPath)) File.Delete(normalPath);
                if (File.Exists(originalPath)) File.Delete(originalPath);

                ViewModel.UpdateAvatarPreview();
                WeakReferenceMessenger.Default.Send(new NotificationMessage("Success".GetLocalized(), string.Format("Avatar_Clear_Success_Format".GetLocalized(), size), NotificationType.Success));
            }
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage("Avatar_Clear_Fail_Title".GetLocalized(), ex.Message, NotificationType.Error));
        }
    }
}
