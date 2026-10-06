/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text;
using System.Text.RegularExpressions;
using FufuLauncher.Helpers;

namespace FufuLauncher.Services;

public static class GachaCacheService
{
    private const int BufferSize = 64 * 1024;
    private const int MaxUrlLength = 16 * 1024;

    public static Task<GachaLink?> FindLatestLinkAsync(string gamePath, CancellationToken cancellationToken = default)
        => Task.Run(() => FindLatestLink(gamePath, cancellationToken), cancellationToken);

    private static GachaLink? FindLatestLink(string gamePath, CancellationToken cancellationToken)
    {
        gamePath = gamePath.Trim().Trim('"');
        var isDirectory = Directory.Exists(gamePath);
        var executable = Path.GetFileName(gamePath);
        if (!isDirectory && (!File.Exists(gamePath) ||
                             (!executable.Equals("YuanShen.exe", StringComparison.OrdinalIgnoreCase) &&
                              !executable.Equals("GenshinImpact.exe", StringComparison.OrdinalIgnoreCase))))
            throw new DirectoryNotFoundException();
        var gameDirectory = isDirectory ? gamePath : Path.GetDirectoryName(gamePath);
        if (string.IsNullOrEmpty(gameDirectory) || !Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException();

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        var files = new List<FileInfo>();
        foreach (var dataDirectory in new[] { "YuanShen_Data", "GenshinImpact_Data" })
        {
            if (!isDirectory && !dataDirectory.StartsWith(Path.GetFileNameWithoutExtension(executable),
                    StringComparison.OrdinalIgnoreCase))
                continue;
            var cacheRoot = Path.Combine(gameDirectory, dataDirectory, "webCaches");
            if (!Directory.Exists(cacheRoot)) continue;
            foreach (var cacheDirectory in Directory.EnumerateDirectories(cacheRoot, "Cache_Data", options))
            {
                foreach (var file in Directory.EnumerateFiles(cacheDirectory))
                {
                    var name = Path.GetFileName(file);
                    if (name.StartsWith("data_", StringComparison.Ordinal) ||
                        name.EndsWith("_0", StringComparison.Ordinal))
                        files.Add(new FileInfo(file));
                }
            }
        }

        if (files.Count == 0) throw new FileNotFoundException();

        GachaLink? latest = null;
        var latestTime = DateTimeOffset.MinValue;
        var latestFileTime = DateTime.MinValue;
        var readAnyFile = false;
        IOException? readError = null;
        var unreadFileTime = DateTime.MinValue;
        foreach (var file in files.OrderBy(f => f.LastWriteTimeUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var buffer = new byte[BufferSize];
                var tail = "";
                int count;
                while ((count = stream.Read(buffer)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var text = tail + Encoding.Latin1.GetString(buffer, 0, count);
                    tail = "";
                    foreach (Match match in GachaUrlHelper.UrlPattern.Matches(text))
                    {
                        if (match.Index + match.Length == text.Length)
                        {
                            if (match.Length <= MaxUrlLength) tail = match.Value;
                            continue;
                        }

                        Consider(match.Value, file.LastWriteTimeUtc);
                    }

                    // Preserve a split "https://" prefix as well as a URL split across buffers.
                    if (tail.Length == 0) tail = text[^Math.Min(7, text.Length)..];
                }

                Consider(tail, file.LastWriteTimeUtc);
                readAnyFile = true;
            }
            catch (UnauthorizedAccessException ex)
            {
                readError = new IOException("Cannot read game cache.", ex);
                unreadFileTime = file.LastWriteTimeUtc > unreadFileTime ? file.LastWriteTimeUtc : unreadFileTime;
            }
            catch (IOException ex)
            {
                readError = ex;
                unreadFileTime = file.LastWriteTimeUtc > unreadFileTime ? file.LastWriteTimeUtc : unreadFileTime;
            }
        }

        // An unreadable newer cache could contain another account; do not silently use an older link.
        if (readError != null && (!readAnyFile || unreadFileTime >= latestFileTime)) throw readError;
        return latest;

        void Consider(string text, DateTime modifiedAt)
        {
            if (text.Length > MaxUrlLength) return;
            var link = GachaUrlHelper.Parse(text);
            if (link == null) return;
            var time = link.CreatedAt ?? new DateTimeOffset(modifiedAt, TimeSpan.Zero);
            if (time > latestTime || (time == latestTime && modifiedAt >= latestFileTime))
            {
                latest = link;
                latestTime = time;
                latestFileTime = modifiedAt;
            }
        }
    }
}