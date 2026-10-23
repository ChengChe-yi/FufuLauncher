/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using Microsoft.Win32;
using FufuLauncher.Helpers;

namespace FufuLauncher.Views;

public static class AnnouncementLauncher
{
    private const string EdgeAppPathKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe";
    private const string EdgeExecutableRelativePath = @"Microsoft\Edge\Application\msedge.exe";

    public static bool Open(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            DebugLog.Info("AnnouncementLauncher", $"[Announcement] Ignored non-http(s) announcement link: {url}");
            return false;
        }

        return TryOpenWithEdge(uri) || OpenWithDefaultBrowser(uri);
    }

    private static bool TryOpenWithEdge(Uri uri)
    {
        var edgePath = ResolveEdgePath();
        if (edgePath is null)
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo(edgePath) { UseShellExecute = false };
            startInfo.ArgumentList.Add(uri.AbsoluteUri);
            return Process.Start(startInfo) is not null;
        }
        catch (Exception ex)
        {
            DebugLog.Info("AnnouncementLauncher", $"[Announcement] Failed to open announcement via Edge: {ex.Message}");
            return false;
        }
    }

    private static bool OpenWithDefaultBrowser(Uri uri)
    {
        try
        {
            return Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }) is not null;
        }
        catch (Exception ex)
        {
            DebugLog.Info("AnnouncementLauncher", $"[Announcement] Failed to open announcement link: {ex.Message}");
            return false;
        }
    }

    private static string? ResolveEdgePath()
    {
        return FindEdgeInRegistry() ?? FindEdgeInInstallRoots();
    }

    private static string? FindEdgeInRegistry()
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (var view in CandidateRegistryViews)
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var appPathKey = baseKey.OpenSubKey(EdgeAppPathKey);

                if (appPathKey?.GetValue(null) is string path && File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    private static string? FindEdgeInInstallRoots()
    {
        string[] roots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        foreach (var root in roots)
        {
            var path = Path.Combine(root, EdgeExecutableRelativePath);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static RegistryView[] CandidateRegistryViews => Environment.Is64BitOperatingSystem
        ? new[] { RegistryView.Registry64, RegistryView.Registry32 }
        : new[] { RegistryView.Registry32 };
}