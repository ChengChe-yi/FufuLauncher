/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Services;
using Microsoft.UI.Windowing;

namespace FufuLauncher;

public sealed partial class MainWindow
{
    #region Memory Management

    private void FlushMemory()
    {
        try
        {
            if (ContentFrame.BackStackDepth > 0)
            {
                ContentFrame.BackStack.Clear();
            }

            var isMinimized = AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped &&
                              ((OverlappedPresenter)AppWindow.Presenter).State == OverlappedPresenterState.Minimized;

            var isHidden = !Visible;

            MemoryOptimizationService.FlushMemory(isMinimized || isHidden);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"内存清理异常: {ex.Message}");
        }
    }

    private void OnMemoryOptimizationTick(object sender, object e)
    {
        _memoryOptimizationTimer.Stop();
        PerformMemoryOptimization();
    }

    private void PerformMemoryOptimization()
    {
        if (_isSuspended) return;
        _isSuspended = true;

        try
        {
            if (_globalBackgroundPlayer != null && _globalBackgroundPlayer.PlaybackSession != null)
            {
                try
                {
                    if (_globalBackgroundPlayer.PlaybackSession.CanPause)
                    {
                        _globalBackgroundPlayer.Pause();
                    }
                    _suspendedVideoSource = _globalBackgroundPlayer.Source;
                    _globalBackgroundPlayer.Source = null;
                }
                catch (System.Runtime.InteropServices.COMException)
                {

                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"挂起媒体播放时发生异常: {ex.Message}");
        }

        _slideshowTimer?.Stop();

        _networkMonitorService.Stop();
        _messageDismissTimer.Stop();

        _announcementCheckTimer.Stop();
        FlushMemory();

        Debug.WriteLine("应用挂起");
    }

    private void RestoreFromSuspension()
    {
        _memoryOptimizationTimer.Stop();

        if (!_isSuspended) return;
        _isSuspended = false;

        try
        {
            if (_isVideoBackground && _globalBackgroundPlayer != null)
            {
                if (_suspendedVideoSource != null)
                {
                    _globalBackgroundPlayer.Source = _suspendedVideoSource;
                    _suspendedVideoSource = null;
                }
                _globalBackgroundPlayer.Play();
            }
        }
        catch (System.Runtime.InteropServices.COMException) {}
        catch (Exception ex)
        {
            Debug.WriteLine($"恢复媒体播放时发生异常: {ex.Message}");
        }

        _slideshowTimer?.Start();

        if (!_networkMonitorService.IsEnabled)
        {
            _networkMonitorService.Start();
        }

        if (!_announcementCheckTimer.IsEnabled)
        {
            _announcementCheckTimer.Start();
        }

        Debug.WriteLine("应用已唤醒");
    }

    #endregion
}
