/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;

namespace FufuLauncher.Helpers;

public static class SettingsLog
{
    public static bool Verbose
    {
        get;
        set;
    } = ReadVerboseFromEnvironment();

    private static bool ReadVerboseFromEnvironment()
    {
        try
        {
            var raw = Environment.GetEnvironmentVariable("FUFU_SETTINGS_VERBOSE");
            return string.Equals(raw, "1", StringComparison.Ordinal)
                   || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [Conditional("DEBUG")]
    public static void Write(string message)
    {
        if (Verbose)
            DebugLog.Info("SettingsLog", message);
    }
}