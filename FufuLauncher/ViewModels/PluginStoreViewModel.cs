/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Models;
using FufuLauncher.Services;
using FufuLauncher.Services.PluginMirror;
using FufuLauncher.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.ViewModels;

public class PluginStoreViewModel : INotifyPropertyChanged
{
    private readonly PluginStoreService _storeService;
    private readonly LuaPluginInstaller _luaInstaller;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly string _pluginsDir;
    private DispatcherQueue? _dispatcher;

    private ObservableCollection<PluginStoreItem> _plugins = new();
    private ObservableCollection<PluginStoreCategory> _categories = new();
    private PluginStoreCategory? _selectedCategory;
    private string _searchText = string.Empty;
    private string _sortMode = "popular";
    private bool _isLoading;
    private bool _isEmpty;
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _totalPlugins;
    
    private bool _hasContent;

    private bool _isMirrorAccelerationEnabled = true;

    private CancellationTokenSource? _installCts;
    
    private readonly HashSet<string> _installingPluginIds = new(StringComparer.Ordinal);
    
    private static readonly string CurrentAppVersion =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0.0";

    public PluginStoreViewModel(PluginStoreService storeService, LuaPluginInstaller luaInstaller,
        ILocalSettingsService localSettingsService)
    {
        _pluginsDir = Path.Combine(AppContext.BaseDirectory, "Plugins");
        _storeService = storeService;
        _luaInstaller = luaInstaller;
        _localSettingsService = localSettingsService;

        _luaInstaller.ProgressChanged += OnInstallProgress;

        RefreshCommand = new RelayCommand(async () => await LoadPluginsAsync());
        SearchCommand = new RelayCommand(async () => await SearchAsync());
        SortCommand = new RelayCommand<string>(async (s) => await SortAsync(s!));
        SelectCategoryCommand = new RelayCommand<PluginStoreCategory>(async (cat) => await SelectCategoryAsync(cat!));
        InstallCommand = new RelayCommand<PluginStoreItem>(async (item) => await InstallPluginAsync(item!));
        UninstallCommand = new RelayCommand<PluginStoreItem>(async (item) => await UninstallPluginAsync(item!));
        NextPageCommand = new RelayCommand(async () => await GoToPageAsync(_currentPage + 1));
        PrevPageCommand = new RelayCommand(async () => await GoToPageAsync(_currentPage - 1));
        AddPrivatePluginCommand = new RelayCommand(async () => await AddPrivatePluginAsync());
        LuaTestCommand = new RelayCommand(async () => await ExecuteLuaTestAsync());
        CancelInstallCommand = new RelayCommand<PluginStoreItem>(item => CancelInstall(item!));
    }

    public ObservableCollection<PluginStoreItem> Plugins
    {
        get => _plugins;
        set { _plugins = value; OnPropertyChanged(); }
    }

    public ObservableCollection<PluginStoreCategory> Categories
    {
        get => _categories;
        set { _categories = value; OnPropertyChanged(); }
    }

    public PluginStoreCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            SyncCategorySelection();
            OnPropertyChanged();
        }
    }
    
    private void SyncCategorySelection()
    {
        foreach (var category in Categories)
        {
            category.IsSelected = ReferenceEquals(category, _selectedCategory);
        }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); }
    }

    public string SortMode
    {
        get => _sortMode;
        set { _sortMode = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); OnPageStateChanged(); }
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        set { _isEmpty = value; OnPropertyChanged(); OnPageStateChanged(); }
    }

    public bool HasError
    {
        get => _hasError;
        set { _hasError = value; OnPropertyChanged(); OnPageStateChanged(); }
    }
    
    public bool ShowSkeleton => IsLoading && !_hasContent;
    public bool IsRefreshing => IsLoading && _hasContent;
    public bool ShowError => HasError && !IsLoading;
    public bool ShowEmpty => IsEmpty && !IsLoading && !HasError;
    public bool ShowGrid => !HasError && !IsEmpty && (_hasContent || !IsLoading);
    public bool ShowPagination => ShowGrid && TotalPages > 1;

    private void OnPageStateChanged()
    {
        OnPropertyChanged(nameof(ShowSkeleton));
        OnPropertyChanged(nameof(IsRefreshing));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(ShowPagination));
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public int CurrentPage
    {
        get => _currentPage;
        set { _currentPage = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGoPrev)); OnPropertyChanged(nameof(CanGoNext)); OnPropertyChanged(nameof(PageInfo)); }
    }

    public int TotalPages
    {
        get => _totalPages;
        set { _totalPages = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGoNext)); OnPropertyChanged(nameof(PageInfo)); OnPropertyChanged(nameof(ShowPagination)); }
    }

    public int TotalPlugins
    {
        get => _totalPlugins;
        set { _totalPlugins = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageInfo)); }
    }

    public bool CanGoPrev => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;
    public string PageInfo => TotalPages > 0 ? $"{CurrentPage} / {TotalPages}" : "";
    
    public bool IsMirrorAccelerationEnabled
    {
        get => _isMirrorAccelerationEnabled;
        set
        {
            if (_isMirrorAccelerationEnabled == value) return;
            _isMirrorAccelerationEnabled = value;
            OnPropertyChanged();
            _ = _localSettingsService.SaveSettingAsync(PluginMirrorDownloadService.SettingKey, value);
        }
    }

    private async Task LoadMirrorAccelerationSettingAsync()
    {
        try
        {
            var json = await _localSettingsService.ReadSettingAsync(PluginMirrorDownloadService.SettingKey);
            _isMirrorAccelerationEnabled = json == null || Convert.ToBoolean(json);
            OnPropertyChanged(nameof(IsMirrorAccelerationEnabled));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginStoreVM] Failed to load mirror acceleration setting: {ex.Message}");
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand SortCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }
    public ICommand AddPrivatePluginCommand { get; }
    public ICommand LuaTestCommand { get; }
    public ICommand CancelInstallCommand { get; }

    public async Task InitializeAsync()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        await LoadMirrorAccelerationSettingAsync();
        await LoadCategoriesAsync();
        await LoadPluginsAsync();
    }

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
    
    private void CancelInstall(PluginStoreItem item)
    {
        if (item == null || !item.IsInstallInProgress) return;

        Debug.WriteLine($"[PluginStoreVM] User cancelled install for plugin: {item.Id}");
        
        _installCts?.Cancel();

        item.InstallStatusText = "PluginStoreCancelling".GetLocalized();
        item.DownloadSpeedBytesPerSecond = 0;
    }

    private async Task InstallPluginAsync(PluginStoreItem item)
    {
        if (item == null || item.IsInstallInProgress) return;

        try
        {
            _installCts?.Cancel();
            _installCts = new CancellationTokenSource();

            _installingPluginIds.Add(item.Id);

            item.State = StorePluginState.Installing;
            item.IsInstallInProgress = true;
            item.InstallProgress = 0;
            item.InstallStatusText = "PluginStoreVerifying".GetLocalized();
            
            if (!string.IsNullOrWhiteSpace(item.MinAppVersion))
            {
                if (!IsVersionSatisfied(CurrentAppVersion, item.MinAppVersion))
                {
                    _installingPluginIds.Remove(item.Id);
                    await ShowMinVersionWarningAsync(item);
                    item.State = StorePluginState.Available;
                    item.InstallProgress = 0;
                    item.InstallStatusText = "PluginStoreVersionTooLow".GetLocalized();
                    return;
                }
            }
            
            if (item.IsPrivate && string.IsNullOrWhiteSpace(item.AccessToken))
            {
                var accessKey = await ShowPrivateAccessDialogAsync(item);
                if (string.IsNullOrWhiteSpace(accessKey))
                {
                    _installingPluginIds.Remove(item.Id);
                    item.State = StorePluginState.Available;
                    item.InstallProgress = 0;
                    item.InstallStatusText = string.Empty;
                    return;
                }

                try
                {
                    var accessResult = await _storeService.GetPrivateAccessAsync(item.Id, accessKey);
                    item.AccessToken = accessResult.AccessToken;
                    
                    if (accessResult.Plugin != null)
                    {
                        item.Version = accessResult.Plugin.Version;
                        item.FileHash = accessResult.Plugin.FileHash;
                        item.LuaHash = accessResult.Plugin.LuaHash;
                        item.LuaInstallUrl = accessResult.Plugin.LuaInstallUrl;
                        item.LuaUninstallUrl = accessResult.Plugin.LuaUninstallUrl;
                        item.DownloadUrl = accessResult.Plugin.DownloadUrl;
                        item.SizeBytes = accessResult.Plugin.SizeBytes;
                        item.DllFileName = accessResult.Plugin.DllFileName;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PluginStoreVM] Private access failed: {ex.Message}");
                    _installingPluginIds.Remove(item.Id);
                    item.State = StorePluginState.Available;
                    item.InstallProgress = 0;
                    item.InstallStatusText = "PluginStorePrivateAccessDenied".GetLocalized();
                    StatusMessage = ex.Message;
                    return;
                }
            }

            await DoInstallAsync(item);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginStoreVM] Install error: {ex}");
            _installingPluginIds.Remove(item.Id);
            item.State = StorePluginState.Available;
            item.InstallProgress = 0;
            item.InstallStatusText = "PluginStoreInstallFailedShort".GetLocalized();
            StatusMessage = string.Format("PluginStoreInstallFailed".GetLocalized(), ex.Message);
            
            CleanupPluginDir(item.Id);
        }
        finally
        {
            _installingPluginIds.Remove(item.Id);
            item.IsInstallInProgress = false;
            _installCts?.Dispose();
            _installCts = null;
        }
    }
    
    private async Task DoInstallAsync(PluginStoreItem item)
    {
        var maxCaptchaRetries = 3;
        var attempt = 0;

        while (attempt < maxCaptchaRetries)
        {
            try
            {
                item.InstallStatusText = attempt > 0
                    ? "PluginStoreRetrying".GetLocalized()
                    : "PluginStoreDownloadingLua".GetLocalized();

                await _luaInstaller.ExecuteInstallScriptAsync(
                    item.LuaInstallUrl,
                    item.LuaHash,
                    item.FileHash,
                    _installCts?.Token ?? CancellationToken.None,
                    item.DllFileName,
                    item.Id,
                    item.DlToken,
                    item.AccessToken);

                var pluginDir = Path.Combine(_pluginsDir, item.Id);
                _luaInstaller.EnsureConfigFileEntry(pluginDir, item.DllFileName);
                
                if (!IsPluginInstalledOnDisk(item.Id, out _))
                {
                    Debug.WriteLine($"[PluginStoreVM] Install verification failed: plugin '{item.Id}' not found on disk after install script");
                    item.State = StorePluginState.Available;
                    item.InstallProgress = 0;
                    item.InstallStatusText = "PluginStoreInstallFailedShort".GetLocalized();
                    StatusMessage = string.Format("PluginStoreInstallFailed".GetLocalized(), "PluginStoreInstallVerifyFailed".GetLocalized());
                    CleanupPluginDir(item.Id);
                    return;
                }

                if (_dispatcher != null)
                {
                    var capturedItem = item;
                    _dispatcher.TryEnqueue(async () =>
                    {
                        capturedItem.InstallProgress = 100;
                        capturedItem.InstallProgressPercent = 100.0;
                        capturedItem.InstallStatusText = "PluginStoreInstallComplete".GetLocalized();
                        capturedItem.DownloadSpeedBytesPerSecond = 0;
                        
                        await Task.Delay(600);
                        
                        capturedItem.State = StorePluginState.Installed;
                    });
                }
                else
                {
                    item.InstallProgress = 100;
                    item.InstallProgressPercent = 100.0;
                    item.InstallStatusText = "PluginStoreInstallComplete".GetLocalized();
                    item.State = StorePluginState.Installed;
                }
                StatusMessage = string.Format("PluginStoreInstallSuccess".GetLocalized(), item.Name);
                return;
            }
            catch (CaptchaRequiredException captchaEx)
            {
                Debug.WriteLine($"[PluginStoreVM] Captcha required: {captchaEx.VerifyUrl}");
                item.InstallStatusText = "PluginStoreCaptchaRequired".GetLocalized();
                
                var dlToken = await ShowGeetestCaptchaAsync(captchaEx.VerifyUrl);

                if (string.IsNullOrWhiteSpace(dlToken))
                {
                    throw new OperationCanceledException("PluginStoreCaptchaCancelled".GetLocalized());
                }

                item.DlToken = dlToken;
                attempt++;
                Debug.WriteLine($"[PluginStoreVM] Got dl_token, retrying download (attempt {attempt})...");
            }
            catch (PrivatePluginAccessException privEx)
            {
                Debug.WriteLine($"[PluginStoreVM] Private access required: {privEx.Message}");
                item.InstallStatusText = "PluginStorePrivateAccessRequired".GetLocalized();

                var accessKey = await ShowPrivateAccessDialogAsync(item);
                if (string.IsNullOrWhiteSpace(accessKey))
                    throw new OperationCanceledException("PluginStorePrivateAccessCancelled".GetLocalized());

                var accessResult = await _storeService.GetPrivateAccessAsync(item.Id, accessKey);
                item.AccessToken = accessResult.AccessToken;
                if (accessResult.Plugin != null)
                {
                    item.FileHash = accessResult.Plugin.FileHash;
                    item.LuaHash = accessResult.Plugin.LuaHash;
                }
                attempt++;
            }
            catch (HashMismatchException ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Hash mismatch: {ex.Message}");
                item.State = StorePluginState.Available;
                item.InstallProgress = 0;
                item.InstallStatusText = "PluginStoreHashFailed".GetLocalized();
                StatusMessage = string.Format("PluginStoreInstallFailed".GetLocalized(), ex.Message);
                CleanupPluginDir(item.Id);
                return;
            }
            catch (SecurityViolationException ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Security violation: {ex.Message}");
                item.State = StorePluginState.Available;
                item.InstallProgress = 0;
                item.InstallStatusText = "PluginStoreSecurityBlockedShort".GetLocalized();
                StatusMessage = string.Format("PluginStoreSecurityBlocked".GetLocalized(), ex.Message);
                CleanupPluginDir(item.Id);
                return;
            }
            catch (OperationCanceledException)
            {
                item.State = StorePluginState.Available;
                item.InstallProgress = 0;
                item.InstallStatusText = "PluginStoreCancelled".GetLocalized();
                CleanupPluginDir(item.Id);
                return;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("download") || ex.Message.Contains("Download"))
            {
                Debug.WriteLine($"[PluginStoreVM] Download error (may need captcha): {ex.Message}");
                attempt++;
                if (attempt >= maxCaptchaRetries) throw;
            }
        }

        throw new InvalidOperationException("PluginStoreCaptchaRetryExhausted".GetLocalized());
    }
    
    private static async Task<string?> ShowGeetestCaptchaAsync(string verifyUrl)
    {
        var tcs = new TaskCompletionSource<string?>();

        // Guard: ensure MainWindow and its DispatcherQueue are available
        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot show captcha: MainWindow or DispatcherQueue is null");
            return null;
        }

        var enqueued = dispatcherQueue.TryEnqueue(async () =>
        {
            Window? captchaWindow = null;
            CancellationTokenSource? pollCts = null;
            try
            {
                captchaWindow = new Window();
                captchaWindow.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
                captchaWindow.Title = "人机验证";

                var rootGrid = new Grid();
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                
                var titleBar = new Grid { Height = 32 };
                titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var titleText = new TextBlock
                {
                    Text = "下载验证",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16, 0, 0, 0)
                };
                Grid.SetColumn(titleText, 1);
                titleBar.Children.Add(titleText);

                Grid.SetRow(titleBar, 0);
                rootGrid.Children.Add(titleBar);

                var webView = new WebView2
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                Grid.SetRow(webView, 1);
                rootGrid.Children.Add(webView);

                captchaWindow.Content = rootGrid;
                
                // Configure AppWindow with null guard
                if (captchaWindow.AppWindow is { } appWindow)
                {
                    appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                    appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                    appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 720));

                    // Center on main window (if available)
                    if (App.MainWindow?.AppWindow is { } mainAppWindow)
                    {
                        var mainPos = mainAppWindow.Position;
                        var mainSize = mainAppWindow.Size;
                        appWindow.Move(new Windows.Graphics.PointInt32(
                            mainPos.X + (mainSize.Width - 1280) / 2,
                            mainPos.Y + (mainSize.Height - 720) / 2));
                    }
                }

                captchaWindow.SetTitleBar(titleBar);

                await webView.EnsureCoreWebView2Async();
                
                // Guard: CoreWebView2 must be non-null after initialization
                if (webView.CoreWebView2 is not { } coreWebView)
                {
                    Debug.WriteLine("[PluginStoreVM] CoreWebView2 is null after EnsureCoreWebView2Async");
                    tcs.TrySetResult(null);
                    captchaWindow.Close();
                    return;
                }

                coreWebView.Settings.AreDefaultContextMenusEnabled = false;
                coreWebView.Settings.IsStatusBarEnabled = false;

                pollCts = new CancellationTokenSource();
                var pollToken = pollCts.Token;
                
                coreWebView.NavigationCompleted += async (s, e) =>
                {
                    if (!e.IsSuccess) return;
                    Debug.WriteLine($"[PluginStoreVM] Gate page loaded, starting poll for dl_token...");

                    try
                    {
                        for (var i = 0; i < 120 && !pollToken.IsCancellationRequested; i++)
                        {
                            await Task.Delay(500, pollToken);

                            string raw;
                            try { raw = await webView.CoreWebView2.ExecuteScriptAsync("document.body.textContent"); }
                            catch { continue; }

                            if (string.IsNullOrWhiteSpace(raw)) continue;
                            
                            var unescaped = raw.Trim('"').Replace("\\\"", "\"").Replace("\\\\", "\\");

                            if (!unescaped.StartsWith("{")) continue;

                            try
                            {
                                using var doc = JsonDocument.Parse(unescaped);
                                var root = doc.RootElement;
                                if (root.TryGetProperty("retcode", out var rc) && rc.GetInt32() == 0 &&
                                    root.TryGetProperty("data", out var data) &&
                                    data.TryGetProperty("dl_token", out var dlToken))
                                {
                                    var token = dlToken.GetString();
                                    if (!string.IsNullOrWhiteSpace(token))
                                    {
                                        Debug.WriteLine($"[PluginStoreVM] Got dl_token: {token[..12]}...");
                                        pollCts.Cancel();
                                        tcs.TrySetResult(token);
                                        captchaWindow.DispatcherQueue.TryEnqueue(() => captchaWindow.Close());
                                        return;
                                    }
                                }
                            }
                            catch (JsonException) { }
                        }
                    }
                    catch (TaskCanceledException) { }
                };

                captchaWindow.Closed += (_, _) =>
                {
                    pollCts?.Cancel();
                    tcs.TrySetResult(null);
                };

                Debug.WriteLine($"[PluginStoreVM] Navigating to captcha gate: {verifyUrl}");
                coreWebView.Navigate(verifyUrl);
                captchaWindow.Activate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Error in captcha window: {ex}");
                tcs.TrySetResult(null);
                pollCts?.Cancel();
                // Best-effort close the window if it was created
                if (captchaWindow is not null)
                {
                    try { captchaWindow.DispatcherQueue.TryEnqueue(() => captchaWindow.Close()); }
                    catch { /* ignore cleanup failures */ }
                }
            }
        });

        if (!enqueued)
        {
            Debug.WriteLine("[PluginStoreVM] Failed to enqueue captcha window to DispatcherQueue");
            return null;
        }

        return await tcs.Task;
    }
    
    private static async Task<string?> ShowPrivateAccessDialogAsync(PluginStoreItem item)
    {
        var tcs = new TaskCompletionSource<string?>();

        // Guard: ensure MainWindow and its DispatcherQueue are available
        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot show private access dialog: MainWindow or DispatcherQueue is null");
            return null;
        }

        var enqueued = dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var inputBox = new TextBox
                {
                    PlaceholderText = "请输入访问密钥",
                    Width = 300
                };

                var stackPanel = new StackPanel { Spacing = 12 };
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"插件 \"{item.Name}\"ID{item.Id}为私密插件",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                stackPanel.Children.Add(inputBox);

                // Guard: XamlRoot requires a valid Content
                if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot)
                {
                    Debug.WriteLine("[PluginStoreVM] Cannot show private access dialog: XamlRoot is null");
                    tcs.TrySetResult(null);
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = "私密插件访问",
                    Content = stackPanel,
                    PrimaryButtonText = "确认",
                    SecondaryButtonText = "取消",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputBox.Text))
                {
                    tcs.TrySetResult(inputBox.Text.Trim());
                }
                else
                {
                    tcs.TrySetResult(null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Error in private access dialog: {ex}");
                tcs.TrySetResult(null);
            }
        });

        if (!enqueued)
        {
            Debug.WriteLine("[PluginStoreVM] Failed to enqueue private access dialog to DispatcherQueue");
            return null;
        }

        return await tcs.Task;
    }
    
    private static async Task ShowMinVersionWarningAsync(PluginStoreItem item)
    {
        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot show version warning: MainWindow or DispatcherQueue is null");
            return;
        }

        dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot)
                {
                    Debug.WriteLine("[PluginStoreVM] Cannot show version warning: XamlRoot is null");
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = "版本过低",
                    Content = $"插件 \"{item.Name}\" 要求启动器版本≥ {item.MinAppVersion}，当前版本为 {CurrentAppVersion}\n\n请先更新启动器后再安装此插件",
                    CloseButtonText = "知道了",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = xamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Error showing version warning: {ex}");
            }
        });
    }

    private async Task AddPrivatePluginAsync()
    {
        string? pluginId = null;
        string? accessKey = null;

        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot add private plugin: MainWindow or DispatcherQueue is null");
            return;
        }

        var tcs = new TaskCompletionSource();
        dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot)
                {
                    Debug.WriteLine("[PluginStoreVM] Cannot add private plugin: XamlRoot is null");
                    tcs.TrySetResult();
                    return;
                }

                var idBox = new TextBox { PlaceholderText = "插件ID", Width = 300 };
                var keyBox = new TextBox { PlaceholderText = "访问密钥", Width = 300 };

                var panel = new StackPanel { Spacing = 12 };
                panel.Children.Add(new TextBlock { Text = "输入私密插件的 ID 和访问密钥：" });
                panel.Children.Add(new TextBlock { Text = "插件ID", FontSize = 12, Opacity = 0.7 });
                panel.Children.Add(idBox);
                panel.Children.Add(new TextBlock { Text = "访问密钥", FontSize = 12, Opacity = 0.7 });
                panel.Children.Add(keyBox);

                var dialog = new ContentDialog
                {
                    Title = "添加私密插件",
                    Content = panel,
                    PrimaryButtonText = "添加",
                    SecondaryButtonText = "取消",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    pluginId = idBox.Text.Trim();
                    accessKey = keyBox.Text.Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Error in private plugin dialog: {ex}");
            }
            finally
            {
                tcs.TrySetResult();
            }
        });

        await tcs.Task;

        if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(accessKey))
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "正在验证私密插件访问...";

            var accessResult = await _storeService.GetPrivateAccessAsync(pluginId, accessKey);
            if (accessResult.Plugin != null)
            {
                accessResult.Plugin.AccessToken = accessResult.AccessToken;
                UpdateLocalState(accessResult.Plugin);
                
                Plugins.Insert(0, accessResult.Plugin);
                TotalPlugins++;
                // 与 IsEmpty 一起维护，否则下次刷新会在已有内容上盖一层骨架屏。
                _hasContent = true;
                IsEmpty = false;
                StatusMessage = string.Format("已添加私密插件: {0}", accessResult.Plugin.Name);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginStoreVM] AddPrivatePlugin error: {ex.Message}");
            StatusMessage = string.Format("私密插件添加失败: {0}", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UninstallPluginAsync(PluginStoreItem item)
    {
        if (item == null) return;

        try
        {
            _installingPluginIds.Add(item.Id);

            item.IsInstallInProgress = true;
            item.State = StorePluginState.Installing;
            item.InstallStatusText = "PluginStoreUninstalling".GetLocalized();
            
            if (!string.IsNullOrEmpty(item.LuaUninstallUrl))
            {
                var maxCaptchaRetries = 3;
                var attempt = 0;
                var luaSuccess = false;

                while (attempt < maxCaptchaRetries)
                {
                    try
                    {
                        item.InstallStatusText = attempt > 0
                            ? "PluginStoreRetrying".GetLocalized()
                            : "PluginStoreUninstalling".GetLocalized();

                        var uninstallUrl = AppendTokenToUrl(item.LuaUninstallUrl, item.AccessToken);
                        await _luaInstaller.ExecuteInstallScriptAsync(
                            uninstallUrl,
                            expectedLuaHash: null,
                            expectedFileHash: null,
                            cancellationToken: CancellationToken.None,
                            dllFileName: null,
                            pluginId: item.Id,
                            dlToken: item.DlToken,
                            accessToken: item.AccessToken);

                        luaSuccess = true;
                        break;
                    }
                    catch (CaptchaRequiredException captchaEx)
                    {
                        Debug.WriteLine($"[PluginStoreVM] Uninstall captcha required: {captchaEx.VerifyUrl}");
                        item.InstallStatusText = "PluginStoreCaptchaRequired".GetLocalized();

                        var dlToken = await ShowGeetestCaptchaAsync(captchaEx.VerifyUrl);

                        if (string.IsNullOrWhiteSpace(dlToken))
                        {
                            Debug.WriteLine("[PluginStoreVM] Uninstall captcha cancelled, falling back to directory delete");
                            break;
                        }

                        item.DlToken = dlToken;
                        attempt++;
                        Debug.WriteLine($"[PluginStoreVM] Uninstall: got dl_token, retrying (attempt {attempt})...");
                    }
                    catch (PrivatePluginAccessException privEx)
                    {
                        Debug.WriteLine($"[PluginStoreVM] Uninstall private access required: {privEx.Message}");
                        item.InstallStatusText = "PluginStorePrivateAccessRequired".GetLocalized();

                        var accessKey = await ShowPrivateAccessDialogAsync(item);
                        if (string.IsNullOrWhiteSpace(accessKey))
                        {
                            Debug.WriteLine("[PluginStoreVM] Uninstall private access cancelled, falling back to directory delete");
                            break;
                        }

                        var accessResult = await _storeService.GetPrivateAccessAsync(item.Id, accessKey);
                        item.AccessToken = accessResult.AccessToken;
                        attempt++;
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("download") || ex.Message.Contains("Download"))
                    {
                        Debug.WriteLine($"[PluginStoreVM] Uninstall download error (may need captcha): {ex.Message}");
                        attempt++;
                        if (attempt >= maxCaptchaRetries)
                        {
                            Debug.WriteLine("[PluginStoreVM] Uninstall captcha retries exhausted, falling back to directory delete");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PluginStoreVM] Lua uninstall error, falling back to directory delete: {ex.Message}");
                        break;
                    }
                }

                if (luaSuccess)
                {
                    Debug.WriteLine("[PluginStoreVM] Lua uninstall script completed successfully");
                }
            }
            
            var pluginDir = Path.Combine(_pluginsDir, item.Id);
            if (Directory.Exists(pluginDir))
            {
                Directory.Delete(pluginDir, true);
                Debug.WriteLine($"[PluginStoreVM] Deleted plugin directory: {pluginDir}");
            }

            item.State = StorePluginState.Available;
            item.InstallProgress = 0;
            item.InstallStatusText = "PluginStoreUninstallComplete".GetLocalized();
            StatusMessage = string.Format("PluginStoreUninstallSuccess".GetLocalized(), item.Name);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginStoreVM] Uninstall error: {ex}");
            item.State = StorePluginState.Installed;
            item.InstallStatusText = "PluginStoreUninstallFailed".GetLocalized();
        }
        finally
        {
            _installingPluginIds.Remove(item.Id);
            item.IsInstallInProgress = false;
        }
    }
    
    private void CleanupPluginDir(string pluginId)
    {
        try
        {
            var pluginDir = Path.Combine(_pluginsDir, pluginId);
            if (Directory.Exists(pluginDir))
            {
                Directory.Delete(pluginDir, true);
                Debug.WriteLine($"[PluginStoreVM] Cleaned up partial install: {pluginDir}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginStoreVM] Failed to clean up plugin dir: {ex.Message}");
        }
    }
    
    private bool IsPluginInstalledOnDisk(string pluginId, out string? localVersion)
    {
        localVersion = null;

        if (string.IsNullOrWhiteSpace(pluginId) || !Directory.Exists(_pluginsDir)) return false;

        var pluginDir = Path.Combine(_pluginsDir, pluginId);
        if (!Directory.Exists(pluginDir)) return false;

        var configPath = Path.Combine(pluginDir, "config.ini");
        if (!File.Exists(configPath)) return false;

        try
        {
            var lines = File.ReadAllLines(configPath);
            string? dllFileName = null;
            var inGeneral = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    inGeneral = trimmed.Equals("[General]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inGeneral) continue;

                var parts = trimmed.Split('=', 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                if (key.Equals("Version", StringComparison.OrdinalIgnoreCase))
                {
                    localVersion = value;
                }
                else if (key.Equals("File", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(value))
                {
                    dllFileName = value;
                }
            }
            
            if (!string.IsNullOrEmpty(dllFileName))
            {
                var dllPath = Path.Combine(pluginDir, dllFileName);
                if (!File.Exists(dllPath)) return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateLocalState(PluginStoreItem storeItem)
    {
        if (!IsPluginInstalledOnDisk(storeItem.Id, out var localVersion)) return;

        if (!string.IsNullOrEmpty(localVersion))
        {
            storeItem.State = localVersion != storeItem.Version
                ? StorePluginState.UpdateAvailable
                : StorePluginState.Installed;
        }
        else
        {
            storeItem.State = StorePluginState.Installed;
        }
    }
    
    private static bool IsVersionSatisfied(string currentVersion, string minVersion)
    {
        if (!AppVersionHelper.TryParseVersion(currentVersion, out var cur) ||
            !AppVersionHelper.TryParseVersion(minVersion, out var min))
        {
            return true;
        }

        return cur >= min;
    }

    private static string AppendTokenToUrl(string url, string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return url;
        var uriBuilder = new UriBuilder(url);
        var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
        query["access_token"] = accessToken;
        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }

    private void OnInstallProgress(DownloadProgressInfo info)
    {
        _dispatcher?.TryEnqueue(() =>
        {
            foreach (var id in _installingPluginIds)
            {
                var installing = Plugins.FirstOrDefault(p => p.Id == id);
                if (installing != null && installing.State == StorePluginState.Installing)
                {
                    installing.InstallProgress = (int)Math.Round(info.Percent);
                    installing.InstallProgressPercent = info.Percent;
                    installing.InstallStatusText = info.StatusText;
                    installing.DownloadedBytes = info.BytesDownloaded;
                    installing.TotalDownloadBytes = info.TotalBytes;
                    installing.DownloadSpeedBytesPerSecond = info.SpeedBytesPerSecond;
                }
            }
        });
    }

    public async Task ExecuteLuaTestAsync()
    {
        string? luaCode = null;
        
        var dialogCompleted = new TaskCompletionSource<string?>();

        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot execute Lua test: MainWindow or DispatcherQueue is null");
            return;
        }

        dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot)
                {
                    Debug.WriteLine("[PluginStoreVM] Cannot execute Lua test: XamlRoot is null");
                    dialogCompleted.TrySetResult(null);
                    return;
                }

                var inputBox = new TextBox
                {
                    PlaceholderText = "PluginStoreLuaTestInputHint".GetLocalized(),
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.NoWrap,
                    Height = 300,
                    Width = 560,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas, Cascadia Code, monospace"),
                    FontSize = 13,
                    IsSpellCheckEnabled = false
                };
                ScrollViewer.SetHorizontalScrollBarVisibility(inputBox, ScrollBarVisibility.Auto);
                ScrollViewer.SetVerticalScrollBarVisibility(inputBox, ScrollBarVisibility.Auto);

                var infoText = new TextBlock
                {
                    Text = "PluginStoreLuaTestDescription".GetLocalized(),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                    Opacity = 0.8,
                    Margin = new Thickness(0, 0, 0, 12)
                };

                var panel = new StackPanel { Spacing = 8 };
                panel.Children.Add(infoText);
                panel.Children.Add(inputBox);

                var dialog = new ContentDialog
                {
                    Title = "PluginStoreLuaTestTitle".GetLocalized(),
                    Content = panel,
                    PrimaryButtonText = "PluginStoreLuaTestRun".GetLocalized(),
                    SecondaryButtonText = "PluginStoreLuaTestClose".GetLocalized(),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputBox.Text))
                {
                    dialogCompleted.TrySetResult(inputBox.Text.Trim());
                }
                else
                {
                    dialogCompleted.TrySetResult(null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Lua test dialog error: {ex.Message}");
                dialogCompleted.TrySetResult(null);
            }
        });

        luaCode = await dialogCompleted.Task;
        if (string.IsNullOrWhiteSpace(luaCode))
            return;
        
        var securityResult = PluginVerifier.ValidateLuaSecurity(luaCode);
        if (!securityResult.IsValid)
        {
            var proceed = await ShowLuaTestSecurityWarningAsync(securityResult.Reason ?? "Unknown security issue");
            if (!proceed)
            {
                StatusMessage = string.Format("Lua 测试已取消（安全阻止: {0}）", securityResult.Reason);
                return;
            }
        }
        
        StatusMessage = "PluginStoreLuaTestExecuting".GetLocalized();
        bool success = false;
        string? errorMessage = null;

        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await _luaInstaller.ExecuteUserScriptAsync(luaCode, cts.Token);
            success = true;
        }
        catch (SecurityViolationException ex)
        {
            errorMessage = string.Format("安全违规: {0}", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            errorMessage = string.Format("Lua 执行错误: {0}", ex.Message);
        }
        catch (OperationCanceledException)
        {
            errorMessage = "脚本执行超时（5分钟）";
        }
        catch (Exception ex)
        {
            errorMessage = string.Format("未预期的错误: {0}", ex.Message);
        }
        
        var logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
        var logFileName = $"lua_test_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        var logFilePath = Path.Combine(logDir, logFileName);

        try
        {
            _luaInstaller.SaveLogsToFile(logFilePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginStoreVM] Failed to save log file: {ex.Message}");
            errorMessage = (errorMessage != null)
                ? errorMessage + $"\n日志保存失败: {ex.Message}"
                : $"日志保存失败: {ex.Message}";
        }
        
        await ShowLuaTestResultDialogAsync(success, logFilePath, errorMessage);

        StatusMessage = success
            ? "Lua 脚本测试完成"
            : string.Format("Lua 脚本测试失败: {0}", errorMessage ?? "未知错误");
    }

    private static async Task<bool> ShowLuaTestSecurityWarningAsync(string reason)
    {
        var tcs = new TaskCompletionSource<bool>();

        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot show security warning: MainWindow or DispatcherQueue is null");
            return false;
        }

        var enqueued = dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot)
                {
                    Debug.WriteLine("[PluginStoreVM] Cannot show security warning: XamlRoot is null");
                    tcs.TrySetResult(false);
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = "PluginStoreLuaTestSecurityWarning".GetLocalized(),
                    Content = string.Format("PluginStoreLuaTestSecurityBlocked".GetLocalized(), reason),
                    PrimaryButtonText = "强制执行（不推荐）",
                    SecondaryButtonText = "取消",
                    DefaultButton = ContentDialogButton.Secondary,
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                tcs.TrySetResult(result == ContentDialogResult.Primary);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Error showing security warning: {ex}");
                tcs.TrySetResult(false);
            }
        });

        if (!enqueued)
        {
            Debug.WriteLine("[PluginStoreVM] Failed to enqueue security warning to DispatcherQueue");
            return false;
        }

        return await tcs.Task;
    }

    private static async Task ShowLuaTestResultDialogAsync(bool success, string logPath, string? errorMessage)
    {
        var tcs = new TaskCompletionSource<bool>();

        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot show Lua test result: MainWindow or DispatcherQueue is null");
            return;
        }

        var enqueued = dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot)
                {
                    Debug.WriteLine("[PluginStoreVM] Cannot show Lua test result: XamlRoot is null");
                    tcs.TrySetResult(true);
                    return;
                }

                var messagePanel = new StackPanel { Spacing = 12 };

                var statusIcon = success ? "\uE73E" : "\uE783"; // Checkmark or Error
                var statusColor = success
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.OrangeRed);

                var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                statusRow.Children.Add(new FontIcon
                {
                    Glyph = statusIcon,
                    FontSize = 20,
                    Foreground = statusColor
                });
                statusRow.Children.Add(new TextBlock
                {
                    Text = success ? "PluginStoreLuaTestSuccess".GetLocalized() : "PluginStoreLuaTestFailed".GetLocalized(),
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                });
                messagePanel.Children.Add(statusRow);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    messagePanel.Children.Add(new TextBlock
                    {
                        Text = errorMessage,
                        Foreground = statusColor,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13
                    });
                }

                messagePanel.Children.Add(new TextBlock
                {
                    Text = string.Format("PluginStoreLuaTestLogSaved".GetLocalized(), logPath),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                    Opacity = 0.8,
                    Margin = new Thickness(0, 8, 0, 0)
                });

                var dialog = new ContentDialog
                {
                    Title = success ? "PluginStoreLuaTestSuccess".GetLocalized() : "PluginStoreLuaTestFailed".GetLocalized(),
                    Content = messagePanel,
                    PrimaryButtonText = "PluginStoreLuaTestOpenLog".GetLocalized(),
                    SecondaryButtonText = "PluginStoreLuaTestClose".GetLocalized(),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    try
                    {
                        // Open the log file with the system default text editor
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = logPath,
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PluginStoreVM] Failed to open log file: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Error showing Lua test result: {ex}");
            }
            finally
            {
                tcs.TrySetResult(true);
            }
        });

        if (!enqueued)
        {
            Debug.WriteLine("[PluginStoreVM] Failed to enqueue Lua test result to DispatcherQueue");
            return;
        }

        await tcs.Task;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
