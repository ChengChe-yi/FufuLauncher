/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.ViewModels;

public partial class PluginSettingsViewModel
{
    #region 头像预览

    public string GetAvatarPath(int size) => Path.Combine(AppContext.BaseDirectory, "Plugins", "Avatar", $"avatar{size}.png");
    public string GetAvatarOriginalPath(int size) => Path.Combine(AppContext.BaseDirectory, "Plugins", "Avatar", $"avatar{size}_original.png");
    public string AvatarPath => Path.Combine(AppContext.BaseDirectory, "Plugins", "Avatar", "avatar.png");
    public string AvatarOriginalPath => Path.Combine(AppContext.BaseDirectory, "Plugins", "Avatar", "avatar_original.png");
    public void UpdateAvatarPreview()
    {
        if (SelectedPluginIndex != 2) return;
        
        OnPropertyChanged(nameof(AvatarSettingsVisibility));
        OnPropertyChanged(nameof(MainSettingsVisibility));
        
        Avatar512Source = LoadImageSource(512, out bool has512);
        HasAvatar512 = has512;
        
        Avatar256Source = LoadImageSource(256, out bool has256);
        HasAvatar256 = has256;
        
        Avatar128Source = LoadImageSource(128, out bool has128);
        HasAvatar128 = has128;
    }
    
    private Microsoft.UI.Xaml.Media.Imaging.BitmapImage LoadImageSource(int size, out bool hasAvatar)
    {
        var path = GetAvatarPath(size);
        if (File.Exists(path))
        {
            try
            {
                var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                bmp.CreateOptions = Microsoft.UI.Xaml.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                bmp.UriSource = new Uri(path);
                hasAvatar = true;
                return bmp;
            }
            catch { }
        }
        hasAvatar = false;
        return null;
    }
    #endregion
}
