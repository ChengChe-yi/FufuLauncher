/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace FufuLauncher.Helpers;

public static class MonitorHelper
{
    public sealed record GameMonitor(
        int Index,
        bool IsPrimary,
        int X,
        int Y,
        int Width,
        int Height,
        int WorkX,
        int WorkY,
        int WorkWidth,
        int WorkHeight);

    private const uint MonitorInfoPrimary = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref Rect rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx info);

    public static IReadOnlyList<GameMonitor> GetAll()
    {
        var monitors = new List<MonitorInfoEx>();

        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref Rect rect, IntPtr data) =>
            {
                var info = new MonitorInfoEx { cbSize = Marshal.SizeOf<MonitorInfoEx>() };
                if (GetMonitorInfo(hMonitor, ref info))
                {
                    monitors.Add(info);
                }

                return true;
            }, IntPtr.Zero);
        }
        catch
        {
        }

        var result = new List<GameMonitor>(monitors.Count);
        int index = 0;

        foreach (var info in monitors)
        {
            if ((info.dwFlags & MonitorInfoPrimary) != 0)
            {
                result.Add(CreateMonitor(++index, true, info));
            }
        }

        foreach (var info in monitors)
        {
            if ((info.dwFlags & MonitorInfoPrimary) == 0)
            {
                result.Add(CreateMonitor(++index, false, info));
            }
        }

        return result;
    }

    public static void IdentifyAll()
    {
        foreach (var monitor in GetAll())
        {
            var window = new Window();
            window.ExtendsContentIntoTitleBar = true;

            var grid = new Grid
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.8 },
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var textBlock = new TextBlock
            {
                Text = monitor.Index.ToString(),
                FontSize = 140,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            };

            grid.Children.Add(textBlock);
            window.Content = grid;

            IntPtr hWnd = WindowNative.GetWindowHandle(window);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                var presenter = appWindow.Presenter as OverlappedPresenter;
                if (presenter != null)
                {
                    presenter.SetBorderAndTitleBar(false, false);
                    presenter.IsAlwaysOnTop = true;
                }

                var size = new Windows.Graphics.SizeInt32(250, 250);
                appWindow.Resize(size);

                var centeredX = monitor.WorkX + (monitor.WorkWidth - size.Width) / 2;
                var centeredY = monitor.WorkY + (monitor.WorkHeight - size.Height) / 2;
                appWindow.Move(new Windows.Graphics.PointInt32(centeredX, centeredY));
            }

            window.Activate();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, args) =>
            {
                window.Close();
                ((DispatcherTimer)s).Stop();
            };
            timer.Start();
        }
    }

    private static GameMonitor CreateMonitor(int index, bool isPrimary, MonitorInfoEx info)
    {
        return new GameMonitor(
            index,
            isPrimary,
            info.rcMonitor.Left,
            info.rcMonitor.Top,
            info.rcMonitor.Right - info.rcMonitor.Left,
            info.rcMonitor.Bottom - info.rcMonitor.Top,
            info.rcWork.Left,
            info.rcWork.Top,
            info.rcWork.Right - info.rcWork.Left,
            info.rcWork.Bottom - info.rcWork.Top);
    }
}