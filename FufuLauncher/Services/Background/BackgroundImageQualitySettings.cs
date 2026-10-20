using System.Diagnostics;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Models;

namespace FufuLauncher.Services.Background;

public static class BackgroundImageQualitySettings
{
    public const string QualitySettingKey = "BackgroundImageQuality";

    private static readonly SemaphoreSlim LoadLock = new(1, 1);
    private static readonly SemaphoreSlim SaveLock = new(1, 1);
    private static BackgroundImageQuality? _cachedQuality;

    public static async Task<BackgroundImageQuality> GetEffectiveQualityAsync()
    {
        if (_cachedQuality.HasValue)
            return _cachedQuality.Value;

        await LoadLock.WaitAsync();
        try
        {
            if (_cachedQuality.HasValue)
                return _cachedQuality.Value;

            try
            {
                var settings = App.GetService<ILocalSettingsService>();
                var savedObj = await settings.ReadSettingAsync(QualitySettingKey);
                if (savedObj != null && int.TryParse(savedObj.ToString(), out var saved) &&
                    Enum.IsDefined(typeof(BackgroundImageQuality), saved))
                {
                    _cachedQuality = (BackgroundImageQuality)saved;
                    return _cachedQuality.Value;
                }

                var totalMemoryGb = await Task.Run(SystemEnvironmentHelper.GetTotalMemoryGB);
                _cachedQuality = totalMemoryGb > 0 && totalMemoryGb < 6
                    ? BackgroundImageQuality.UltraLow
                    : BackgroundImageQuality.High;
                Debug.WriteLine($"[BackgroundImageQuality] 自动选择: {_cachedQuality.Value}，总内存: {totalMemoryGb} GB");
                return _cachedQuality.Value;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BackgroundImageQuality] 读取设置失败，使用高画质: {ex.Message}");
                _cachedQuality = BackgroundImageQuality.High;
                return _cachedQuality.Value;
            }
        }
        finally
        {
            LoadLock.Release();
        }
    }

    public static async Task SetUserQualityAsync(BackgroundImageQuality quality)
    {
        _cachedQuality = quality;
        await SaveLock.WaitAsync();
        try
        {
            var settings = App.GetService<ILocalSettingsService>();
            await settings.SaveSettingAsync(QualitySettingKey, (int)quality);
        }
        finally
        {
            SaveLock.Release();
        }
    }

    public static int GetMaxDecodeEdge(BackgroundImageQuality quality) => quality switch
    {
        BackgroundImageQuality.UltraLow => 640,
        BackgroundImageQuality.Low => 960,
        BackgroundImageQuality.Medium => 1280,
        BackgroundImageQuality.High => 1920,
        _ => 0
    };
}