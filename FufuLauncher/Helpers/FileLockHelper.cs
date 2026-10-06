/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;

namespace FufuLauncher.Helpers;

public static class FileLockHelper
{
    public static bool IsFileLocked(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[文件锁] 检测失败 {filePath}: {ex.Message}");
            return false;
        }
    }

    public static string? FindLockedFile(params string?[] filePaths)
    {
        foreach (var path in filePaths)
        {
            if (!string.IsNullOrEmpty(path) && IsFileLocked(path)) return path;
        }

        return null;
    }

    public static string? FindLockedFileInDirectory(string? directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath)) return null;

        try
        {
            foreach (var file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                if (IsFileLocked(file)) return file;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[文件锁] 目录检测失败 {directoryPath}: {ex.Message}");
        }

        return null;
    }

    public static string GetLockedFileTitle() => "Plugin_FileLocked_Title".GetLocalized();

    public static string GetLockedFileMessage(string lockedFilePath) =>
        string.Format("Plugin_FileLocked_Content".GetLocalized(), Path.GetFileName(lockedFilePath));
}