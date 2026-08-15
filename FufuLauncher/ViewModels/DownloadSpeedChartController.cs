/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using FufuLauncher.Controls;
using FufuLauncher.Models.GameServer;

namespace FufuLauncher.Views;

public sealed class DownloadSpeedChartController
{
    private static readonly TimeSpan ChartTickInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan SpeedSampleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StalledThreshold = TimeSpan.FromSeconds(5);
    
    private const double SpeedSmoothingFactor = 0.3;

    private readonly SpeedGraph _chart;
    private readonly GameServerDownloadMonitor _monitor;
    private readonly DispatcherTimer _timer;

    private long _lastSampleBytes;
    private long _lastSampleTicks;
    private double _smoothedSpeed;
    private long _lastBytesTicks;
    private double _lastPercent;

    public DownloadSpeedChartController(SpeedGraph chart, GameServerDownloadMonitor monitor)
    {
        _chart = chart;
        _monitor = monitor;

        _timer = new DispatcherTimer { Interval = ChartTickInterval };
        _timer.Tick += OnTimerTick;
    }
    
    public void Start()
    {
        _monitor.Reset();
        _lastSampleBytes = 0;
        _lastSampleTicks = 0;
        _smoothedSpeed = 0;
        _lastBytesTicks = Environment.TickCount64;
        _lastPercent = 0;

        _chart.ResetGraph();
        _chart.NormalGraph();
        _chart.SetSpeed(0, 0);

        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }
    
    public void Stop()
    {
        _timer.Stop();
    }
    
    public void SetPaused()
    {
        _timer.Stop();
        _chart.PauseGraph();
    }
    
    public void SetFailed()
    {
        _timer.Stop();
        _chart.ErrorGraph();
    }
    
    public void UpdateProgress(GameServerConversionProgress progress)
    {
        if (progress.TotalChunks > 0)
        {
            UpdateProgress(progress.Percent);
        }
    }

    public void UpdateProgress(double percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        _lastPercent = Math.Max(_lastPercent, percent);
    }

    private void OnTimerTick(object? sender, object e)
    {
        long totalBytes = _monitor.TotalBytesTransferred;
        long nowTicks = Environment.TickCount64;

        if (totalBytes != _lastSampleBytes)
        {
            _lastBytesTicks = nowTicks;
        }
        
        if (nowTicks - _lastBytesTicks >= StalledThreshold.TotalMilliseconds)
        {
            _smoothedSpeed = 0;
            _lastSampleBytes = totalBytes;
            _lastSampleTicks = nowTicks;
            _chart.SetSpeed(_lastPercent, 0);
            return;
        }

        if (_lastSampleTicks == 0)
        {
            _lastSampleTicks = nowTicks;
            _chart.SetSpeed(_lastPercent, 0);
            return;
        }
        
        if (nowTicks - _lastSampleTicks >= SpeedSampleInterval.TotalMilliseconds)
        {
            double elapsedSeconds = (nowTicks - _lastSampleTicks) / 1000.0;
            long deltaBytes = totalBytes - _lastSampleBytes;
            double rawSpeed = deltaBytes > 0 ? deltaBytes / elapsedSeconds : 0.0;
            _smoothedSpeed = _smoothedSpeed <= 0 ? rawSpeed
                : rawSpeed * SpeedSmoothingFactor + _smoothedSpeed * (1.0 - SpeedSmoothingFactor);
            _lastSampleBytes = totalBytes;
            _lastSampleTicks = nowTicks;
        }

        _chart.SetSpeed(_lastPercent, (ulong)_smoothedSpeed);
    }
}
