using System.Runtime.InteropServices;

namespace FufuLauncher.Services;

public sealed class SystemResourceMonitor
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;

        public ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

    private ulong _lastIdleTime;
    private ulong _lastSystemTime;
    private bool _hasCpuBaseline;

    public double? SampleCpuUsagePercent()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return null;

        var idleTime = idle.ToUInt64();
        var systemTime = kernel.ToUInt64() + user.ToUInt64();
        if (!_hasCpuBaseline)
        {
            _lastIdleTime = idleTime;
            _lastSystemTime = systemTime;
            _hasCpuBaseline = true;
            return null;
        }

        var systemDelta = systemTime - _lastSystemTime;
        var idleDelta = idleTime - _lastIdleTime;
        _lastIdleTime = idleTime;
        _lastSystemTime = systemTime;
        if (systemDelta == 0)
            return null;

        return Math.Clamp((systemDelta - idleDelta) * 100.0 / systemDelta, 0.0, 100.0);
    }

    public int GetMemoryUsagePercent()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status))
            return 0;

        return (int)Math.Min(status.MemoryLoad, 100u);
    }

    public void ResetCpuBaseline()
    {
        _lastIdleTime = 0;
        _lastSystemTime = 0;
        _hasCpuBaseline = false;
    }
}