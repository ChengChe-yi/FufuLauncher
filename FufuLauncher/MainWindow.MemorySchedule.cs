using System.Diagnostics;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;

namespace FufuLauncher;

public sealed partial class MainWindow
{
    private static readonly TimeSpan NormalMemoryReleaseInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan GameMemoryTelemetryDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan GameMemoryTelemetryInterval = TimeSpan.FromSeconds(10);

    private const double MinGameMemoryReleaseMinutes = 3;
    private const double MaxGameMemoryReleaseMinutes = 20;
    private const double NormalGameMemoryReleaseMinutes = 10;

    private readonly SystemResourceMonitor _systemResourceMonitor = new();
    private readonly Stopwatch _gameMemoryTelemetryStopwatch = new();
    private readonly List<(double Cpu, int Memory)> _gameMemorySamples = new();

    private DispatcherTimer? _gameMemoryTelemetryTimer;
    private DispatcherTimer? _gameMemoryReleaseTimer;
    private bool _isGameMemoryStrategyActive;
    private bool _isMemoryReleaseEnabled = true;
    private bool _isGameRunning;

    private void ApplyGameMemoryStrategy(bool isRunning)
    {
        _isGameRunning = isRunning;

        if (!_isMemoryReleaseEnabled || _isExit)
            return;

        if (isRunning)
            StartGameMemoryStrategy();
        else
            StopGameMemoryStrategy();
    }

    private async Task LoadMemoryReleaseSettingAsync()
    {
        try
        {
            var enabledObj = await _localSettingsService.ReadSettingAsync(MemoryOptimizationService.IsEnabledSettingKey);
            var isEnabled = enabledObj == null || Convert.ToBoolean(enabledObj);
            dispatcherQueue.TryEnqueue(() => ApplyMemoryReleaseEnabled(isEnabled));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"读取内存释放开关失败: {ex.Message}");
        }
    }

    private void ApplyMemoryReleaseEnabled(bool isEnabled)
    {
        if (_isExit)
            return;

        _isMemoryReleaseEnabled = isEnabled;
        if (!isEnabled)
        {
            StopGameMemoryStrategy(restoreNormal: false);
            _periodicMemoryTimer?.Stop();
            Debug.WriteLine("[MemorySchedule] 内存释放已全局关闭");
            return;
        }

        if (_isGameRunning)
            StartGameMemoryStrategy();
        else if (_periodicMemoryTimer != null && !_periodicMemoryTimer.IsEnabled)
            _periodicMemoryTimer.Start();

        Debug.WriteLine("[MemorySchedule] 内存释放已开启");
    }

    private void StartGameMemoryStrategy()
    {
        if (!_isMemoryReleaseEnabled || !_isGameRunning || _isGameMemoryStrategyActive)
            return;

        _isGameMemoryStrategyActive = true;
        _periodicMemoryTimer?.Stop();
        _gameMemoryTelemetryTimer?.Stop();
        _gameMemoryReleaseTimer?.Stop();
        _gameMemoryReleaseTimer = null;
        _gameMemorySamples.Clear();
        _systemResourceMonitor.ResetCpuBaseline();
        _ = _systemResourceMonitor.SampleCpuUsagePercent();
        _gameMemoryTelemetryStopwatch.Restart();

        _gameMemoryTelemetryTimer = new DispatcherTimer { Interval = GameMemoryTelemetryInterval };
        _gameMemoryTelemetryTimer.Tick += OnGameMemoryTelemetryTick;
        _gameMemoryTelemetryTimer.Start();
        Debug.WriteLine("[MemorySchedule] 游戏启动，开始 5 分钟资源遥测");
    }

    private void OnGameMemoryTelemetryTick(object? sender, object e)
    {
        if (!_isMemoryReleaseEnabled || !_isGameMemoryStrategyActive)
            return;

        var cpu = _systemResourceMonitor.SampleCpuUsagePercent();
        var memory = _systemResourceMonitor.GetMemoryUsagePercent();
        if (cpu.HasValue)
            _gameMemorySamples.Add((cpu.Value, memory));

        if (_gameMemoryTelemetryStopwatch.Elapsed < GameMemoryTelemetryDuration)
            return;

        _gameMemoryTelemetryTimer?.Stop();
        _gameMemoryTelemetryTimer = null;
        StartGameMemoryReleaseTimer();
    }

    private void StartGameMemoryReleaseTimer()
    {
        if (!_isMemoryReleaseEnabled || !_isGameMemoryStrategyActive)
            return;

        var (averageCpu, averageMemory) = GetAverageGameMemorySamples();
        var minutes = Math.Clamp(
            NormalGameMemoryReleaseMinutes + averageCpu / 10.0 - averageMemory / 10.0,
            MinGameMemoryReleaseMinutes,
            MaxGameMemoryReleaseMinutes);

        _gameMemoryTelemetryStopwatch.Stop();
        _gameMemoryReleaseTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(minutes) };
        _gameMemoryReleaseTimer.Tick += OnGameMemoryReleaseTick;
        _gameMemoryReleaseTimer.Start();
        Debug.WriteLine(
            $"[MemorySchedule] 游戏遥测完成，平均 CPU {averageCpu:F1}%，平均内存 {averageMemory:F1}%，" +
            $"释放间隔 {minutes:F1} 分钟");
    }

    private (double Cpu, double Memory) GetAverageGameMemorySamples()
    {
        if (_gameMemorySamples.Count == 0)
            return (0, 0);

        var cpu = 0.0;
        var memory = 0.0;
        foreach (var sample in _gameMemorySamples)
        {
            cpu += sample.Cpu;
            memory += sample.Memory;
        }

        return (cpu / _gameMemorySamples.Count, memory / _gameMemorySamples.Count);
    }

    private void OnGameMemoryReleaseTick(object? sender, object e)
    {
        if (!_isMemoryReleaseEnabled || !_isGameMemoryStrategyActive)
            return;

        Debug.WriteLine("[MemorySchedule] 执行游戏期间内存释放");
        FlushMemory(force: true);
    }

    private void StopGameMemoryStrategy(bool restoreNormal = true)
    {
        _isGameMemoryStrategyActive = false;
        _gameMemoryTelemetryTimer?.Stop();
        _gameMemoryTelemetryTimer = null;
        _gameMemoryReleaseTimer?.Stop();
        _gameMemoryReleaseTimer = null;
        _gameMemoryTelemetryStopwatch.Stop();
        _gameMemorySamples.Clear();
        _systemResourceMonitor.ResetCpuBaseline();

        if (restoreNormal && !_isExit && _isMemoryReleaseEnabled &&
            _periodicMemoryTimer != null && !_periodicMemoryTimer.IsEnabled)
            _periodicMemoryTimer.Start();

        Debug.WriteLine(restoreNormal
            ? "[MemorySchedule] 游戏结束，恢复常规内存释放"
            : "[MemorySchedule] 游戏内存调度已停止");
    }
}
