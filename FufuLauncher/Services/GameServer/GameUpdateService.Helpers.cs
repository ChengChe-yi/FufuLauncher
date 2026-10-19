/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using FufuLauncher.Constants;
using FufuLauncher.Helpers;
using FufuLauncher.Models.GameServer;

namespace FufuLauncher.Services.GameServer;

public sealed partial class GameUpdateService
{
    private static void EnsureDirectoryPermissions(string gameDir)
    {
        try
        {
            Directory.CreateDirectory(gameDir);
            string tempFilePath = Path.Combine(gameDir, $"{Guid.NewGuid():N}.tmp");
            string movedFilePath = Path.Combine(gameDir, $"{Guid.NewGuid():N}.tmp");

            using (SafeFileHandle handle = File.OpenHandle(tempFilePath, FileMode.CreateNew, FileAccess.ReadWrite,
                       FileShare.None, preallocationSize: 32 * 1024))
            {
                RandomAccess.Write(handle, "FUFU LAUNCHER DIRECTORY PERMISSION CHECK"u8, 0);
                RandomAccess.FlushToDisk(handle);
            }

            File.Move(tempFilePath, movedFilePath);
            File.Delete(movedFilePath);
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException(
                string.Format("GameServer_InsufficientPermission".GetLocalized(), ex.Message), ex);
        }
    }

    private static void EnsureFreeSpace(GameUpdateContext context, long installTotalBytes)
    {
        long existingChunks = GetExistingChunksSize(context);
        long required = installTotalBytes - existingChunks + TempFileMarginBytes;
        if (required <= 0)
        {
            return;
        }

        string? root = Path.GetPathRoot(context.GameDir);
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        try
        {
            var drive = new DriveInfo(root);
            if (drive.AvailableFreeSpace < required)
            {
                throw new InvalidOperationException(string.Format("GameUpdate_NoFreeSpace".GetLocalized(),
                    ToSizeString(required), ToSizeString(drive.AvailableFreeSpace)));
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
        }
    }

    private static long GetExistingChunksSize(GameUpdateContext context)
    {
        if (!Directory.Exists(context.ChunksDir))
        {
            return 0;
        }

        long size = 0;
        DateTime cutoffDate = DateTime.Now.AddDays(-5);

        try
        {
            foreach (string file in Directory.EnumerateFiles(context.ChunksDir, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime <= cutoffDate)
                    {
                        TryDeleteFile(file);
                        continue;
                    }

                    size += fileInfo.Length;
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch
        {
            return 0;
        }

        return size;
    }

    private static void WriteGameVersion(string gameDir, string tag)
    {
        string configPath = Path.Combine(gameDir, GameConstants.CONFIG_FILE_NAME);
        if (!File.Exists(configPath))
        {
            return;
        }

        var ini = new IniFile(configPath);
        ini.UpdateMultiple(new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["General"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_version"] = tag,
            },
        });
    }

    private static async Task WritePredownloadStatusAsync(GameUpdateContext context, PredownloadStatus status,
        CancellationToken token)
    {
        using FileStream stream = File.Create(context.PredownloadStatusFilePath);
        await JsonSerializer.SerializeAsync(stream, status, cancellationToken: token).ConfigureAwait(false);
    }

    private static void CleanupChunks(GameUpdateContext context)
    {
        try
        {
            if (Directory.Exists(context.ChunksDir))
            {
                Directory.Delete(context.ChunksDir, true);
            }
        }
        catch (Exception ex)
        {
            context.Log(string.Format("GameServer_ChunkCleanupFailed".GetLocalized(), ex.Message));
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static string ToSizeString(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
        {
            return $"{bytes / 1073741824.0:F1} GB";
        }

        if (bytes >= 1024L * 1024L)
        {
            return $"{bytes / 1048576.0:F1} MB";
        }

        return $"{bytes / 1024.0:F1} KB";
    }
}
