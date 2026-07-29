using System.Diagnostics;

namespace FufuLauncher.Services.Backpack;

internal static class BackpackPerf
{
    [Conditional("DEBUG")]
    internal static void Segment(string operation, string segment, Stopwatch stopwatch)
    {
        Debug.WriteLine($"[Backpack.Perf] {operation} | {segment}: {stopwatch.ElapsedMilliseconds} ms");
        stopwatch.Restart();
    }

    [Conditional("DEBUG")]
    internal static void Total(string operation, Stopwatch stopwatch)
        => Debug.WriteLine($"[Backpack.Perf] {operation} | total: {stopwatch.ElapsedMilliseconds} ms");
}
