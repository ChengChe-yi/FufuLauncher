using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using FufuLauncher.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FufuLauncher.Services.Backpack;

public sealed class BackpackRuntimeService : IDisposable
{
    public static BackpackRuntimeService? Current { get; private set; }

    private const string GamePathKey = "GameInstallationPath";
    private readonly ILocalSettingsService _localSettingsService;
    private readonly PipeListenerService _pipe = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly BackpackDbService _db = new();
    private readonly DispatcherTimer _gameMonitor = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherQueue _dispatcher;
    private readonly object _initializationLock = new();
    private Task? _initializationTask;
    private string? _resolvedGameExe;
    private int _launchedPid;
    private bool _disposed;

    public BackpackViewModel ViewModel { get; }
    public event Action? DataReceived;

    public BackpackRuntimeService(ILocalSettingsService localSettingsService)
    {
        Current = this;
        _localSettingsService = localSettingsService;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        ViewModel = new BackpackViewModel(
            _dispatcher,
            new MaterialMetaService(), new FoodMetaService(), new WeaponMetaService(),
            new ArtifactMetaService(), new GadgetMetaService(), new AssetMetaService(), _db);

        _pipe.PacketReceived += ViewModel.OnPacketReceived;
        ViewModel.DataReceived += OnDataReceived;
        _gameMonitor.Tick += OnGameMonitorTick;
        WeakReferenceMessenger.Default.Register<GamePathChangedMessage>(this, (_, message) =>
            _dispatcher.TryEnqueue(() => _ = ResolveGameInstallationAsync(message.GamePath)));
    }

    public Task InitializeAsync()
    {
        lock (_initializationLock)
            return _initializationTask ??= InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            await GfxLoader.WarmupAsync();
            var configuredPath = await _localSettingsService.ReadSettingAsync(GamePathKey) as string;
            await ResolveGameInstallationAsync(configuredPath);
            _ = _pipe.RunAsync(_cts.Token);
            _gameMonitor.Start();
            RefreshGameState();
        }
        finally
        {
            ViewModel.IsInitializing = false;
            ViewModel.RefreshBrowse();
        }
    }

    private async Task ResolveGameInstallationAsync(string? configuredPath)
    {
        var directory = configuredPath?.Trim().Trim('"') ?? string.Empty;
        string? executable = null;

        if (Directory.Exists(directory))
        {
            foreach (var exeName in await GameExeManager.GetExeNamesAsync())
            {
                var candidate = Path.Combine(directory, exeName);
                if (File.Exists(candidate))
                {
                    executable = candidate;
                    break;
                }
            }
        }

        _resolvedGameExe = executable;
        ViewModel.UpdateGameInstallation(directory, executable is not null);
    }

    public async Task LaunchAndSyncAsync()
    {
        await InitializeAsync();
        if (string.IsNullOrWhiteSpace(_resolvedGameExe) || !File.Exists(_resolvedGameExe))
            throw new InvalidOperationException("请先在游戏设置中配置有效的游戏安装目录。");

        ViewModel.IsLaunching = true;
        ViewModel.StatusText = BackpackLocalization.Get("StatusLaunching");
        try
        {
            _launchedPid = await GameLaunchService.LaunchAsync(_resolvedGameExe);
            if (_launchedPid <= 0)
                throw new InvalidOperationException(BackpackLocalization.Get("ErrHelperStartFailed"));

            ViewModel.IsGameRunning = true;
            ViewModel.StatusText = BackpackLocalization.Get("StatusLaunched");
        }
        catch
        {
            ViewModel.IsLaunching = false;
            throw;
        }
    }

    public void KillLaunchedGame()
    {
        var pid = Interlocked.Exchange(ref _launchedPid, 0);
        if (pid <= 0)
        {
            RefreshGameState();
            return;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            if (!process.HasExited) process.Kill();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Backpack] 结束同步游戏进程失败: {ex.Message}");
        }
        finally
        {
            ViewModel.IsGameRunning = false;
            ViewModel.IsLaunching = false;
        }
    }

    private void OnDataReceived()
    {
        DataReceived?.Invoke();
        KillLaunchedGame();
    }

    private void OnGameMonitorTick(object? sender, object e) => RefreshGameState();

    private void RefreshGameState()
    {
        if (_launchedPid <= 0)
        {
            ViewModel.IsGameRunning = false;
            return;
        }

        try
        {
            using var process = Process.GetProcessById(_launchedPid);
            ViewModel.IsGameRunning = !process.HasExited;
            if (process.HasExited) _launchedPid = 0;
        }
        catch
        {
            _launchedPid = 0;
            ViewModel.IsGameRunning = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _gameMonitor.Stop();
        ViewModel.DataReceived -= OnDataReceived;
        _pipe.PacketReceived -= ViewModel.OnPacketReceived;
        _cts.Cancel();
        KillLaunchedGame();
        _cts.Dispose();
        _db.Dispose();
        Current = null;
    }
}
