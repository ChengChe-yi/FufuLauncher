/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;

namespace FufuLauncher.Helpers;

internal static class PreviewTrace
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    [Conditional("DEBUG")]
    public static void Log(string stage, string? detail = null)
    {
        var suffix = string.IsNullOrEmpty(detail) ? string.Empty : " " + detail;
        Debug.WriteLine($"[PreviewTrace][{Clock.ElapsedMilliseconds,7}ms][T{Environment.CurrentManagedThreadId}] {stage}{suffix}");
    }
}
