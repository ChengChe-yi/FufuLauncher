/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Runtime.InteropServices;

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
        {}

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
