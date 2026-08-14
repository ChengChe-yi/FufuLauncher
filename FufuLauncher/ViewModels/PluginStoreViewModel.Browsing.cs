/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Helpers;
using FufuLauncher.Models;

namespace FufuLauncher.ViewModels;

public partial class PluginStoreViewModel
{
    #region Store Browsing & Loading

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var cats = await _storeService.GetCategoriesAsync();

            if (cats.Count > 0)
            {
                Categories.Clear();

                Categories.Add(new PluginStoreCategory
                {
                    Key = "",
                    DisplayName = "PluginStoreAll".GetLocalized(),
                    Icon = "\uE71D"
                });

                foreach (var cat in cats)
                {
                    Categories.Add(cat);
                }

                SelectedCategory = Categories.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginStoreVM] Error loading categories: {ex.Message}");
            if (Categories.Count == 0)
            {
                Categories.Clear();
                Categories.Add(new PluginStoreCategory { Key = "", DisplayName = "PluginStoreAll".GetLocalized(), Icon = "\uE71D" });
                Categories.Add(new PluginStoreCategory { Key = "utility", DisplayName = "PluginStoreCategoryUtility".GetLocalized(), Icon = "\uE90F" });
                Categories.Add(new PluginStoreCategory { Key = "gameplay", DisplayName = "PluginStoreCategoryGameplay".GetLocalized(), Icon = "\uE7FC" });
                Categories.Add(new PluginStoreCategory { Key = "visuals", DisplayName = "PluginStoreCategoryVisuals".GetLocalized(), Icon = "\uE790" });
                SelectedCategory = Categories.FirstOrDefault();
            }
        }
    }

    public async Task LoadPluginsAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "PluginStoreLoading".GetLocalized();

            var category = SelectedCategory?.Key;
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();

            var response = await _storeService.GetPluginListAsync(
                category: string.IsNullOrEmpty(category) ? null : category,
                search: search,
                sort: SortMode,
                page: CurrentPage,
                pageSize: 20);
            
            var savedInstallingStates = new Dictionary<string, (double percent, string status, long downloaded, long total, long speed)>(StringComparer.Ordinal);
            if (_installingPluginIds.Count > 0)
            {
                foreach (var plugin in Plugins)
                {
                    if (_installingPluginIds.Contains(plugin.Id))
                    {
                        savedInstallingStates[plugin.Id] = (plugin.InstallProgressPercent, plugin.InstallStatusText,
                            plugin.DownloadedBytes, plugin.TotalDownloadBytes, plugin.DownloadSpeedBytesPerSecond);
                    }
                }
            }

            Plugins.Clear();
            if (response.Plugins != null)
            {
                foreach (var plugin in response.Plugins)
                {
                    if (_installingPluginIds.Contains(plugin.Id))
                    {
                        plugin.State = StorePluginState.Installing;
                        plugin.IsInstallInProgress = true;
                        if (savedInstallingStates.TryGetValue(plugin.Id, out var saved))
                        {
                            plugin.InstallProgressPercent = saved.percent;
                            plugin.InstallProgress = (int)Math.Round(saved.percent);
                            plugin.InstallStatusText = saved.status;
                            plugin.DownloadedBytes = saved.downloaded;
                            plugin.TotalDownloadBytes = saved.total;
                            plugin.DownloadSpeedBytesPerSecond = saved.speed;
                        }
                        else
                        {
                            plugin.InstallProgress = 0;
                            plugin.InstallProgressPercent = 0;
                            plugin.InstallStatusText = "PluginStoreDownloadingLua".GetLocalized();
                        }
                    }
                    else
                    {
                        UpdateLocalState(plugin);
                    }
                    Plugins.Add(plugin);
                }
            }

            TotalPlugins = response.Total;
            TotalPages = response.Total > 0
                ? (int)Math.Ceiling((double)response.Total / 20)
                : 1;

            _hasContent = Plugins.Count > 0;
            IsEmpty = Plugins.Count == 0;
            if (IsEmpty)
            {
                if (!string.IsNullOrWhiteSpace(SearchText) || (SelectedCategory != null && !string.IsNullOrEmpty(SelectedCategory.Key)))
                    StatusMessage = "PluginStoreNoMatch".GetLocalized();
                else
                    StatusMessage = "PluginStoreNoAvailable".GetLocalized();
            }
            else
            {
                StatusMessage = string.Format("PluginStoreTotalPlugins".GetLocalized(), TotalPlugins);
            }
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"[PluginStoreVM] {ex.Message}");
            HasError = true;
            ErrorMessage = ex.Message;
            StatusMessage = "PluginStoreConnectionFailed".GetLocalized();
            _hasContent = false;
            IsEmpty = Plugins.Count == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginStoreVM] Error loading plugins: {ex}");
            HasError = true;
            ErrorMessage = "PluginStoreLoadFailed".GetLocalized();
            StatusMessage = "PluginStoreError".GetLocalized();
            _hasContent = false;
            IsEmpty = Plugins.Count == 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await LoadPluginsAsync();
    }

    private async Task SortAsync(string sortMode)
    {
        SortMode = sortMode;
        CurrentPage = 1;
        await LoadPluginsAsync();
    }

    private async Task SelectCategoryAsync(PluginStoreCategory category)
    {
        SelectedCategory = category;
        CurrentPage = 1;
        await LoadPluginsAsync();
    }

    public async Task GoToPageAsync(int page)
    {
        if (page < 1 || page > TotalPages) return;
        CurrentPage = page;
        await LoadPluginsAsync();
    }

    #endregion
}
