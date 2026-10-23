/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using FufuLauncher.Helpers;
using FufuLauncher.Models;
using MoonSharp.Interpreter;

namespace FufuLauncher.Services;

public partial class LuaPluginInstaller
{
    #region Install API

    private void RegisterInstallHandlers(Script script, Table table, CancellationToken cancellationToken)
    {
        table["download"] = (Action<string, string>)((url, path) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "download");
            LogMessage($"Download: {url} -> {safePath}");

            try
            {
                _storeService.DownloadFileAsync(url, safePath,
                    new Progress<DownloadProgressInfo>(p =>
                    {
                        ReportProgress(5 + p.Percent * 70 / 100, p.StatusText, p.BytesDownloaded, p.TotalBytes,
                            p.SpeedBytesPerSecond);
                    }),
                    _expectedFileHash, _dlToken, _accessToken,
                    cancellationToken).GetAwaiter().GetResult();
                _downloadedFiles.Add(safePath);
            }
            catch (Exception ex)
            {
                LogMessage($"Download failed: {ex.Message}");
                throw;
            }
        });

        table["download_plugin"] = (Func<string, DynValue>)(pluginId =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = new Table(script);
            result["success"] = DynValue.NewBoolean(false);
            result["error"] = DynValue.NewString("");

            try
            {
                if (string.IsNullOrWhiteSpace(pluginId))
                {
                    result["error"] = DynValue.NewString("plugin_id is required");
                    return DynValue.NewTable(result);
                }

                if (pluginId.Contains("..") ||
                    pluginId.Contains('/') || pluginId.Contains('\\') ||
                    pluginId.Contains(':') || pluginId.Contains('*') ||
                    pluginId.Contains('?') || pluginId.Contains('"') ||
                    pluginId.Contains('<') || pluginId.Contains('>') || pluginId.Contains('|'))
                {
                    result["error"] = DynValue.NewString("plugin_id contains invalid characters");
                    return DynValue.NewTable(result);
                }

                var pluginDir = Path.Combine(_pluginsDir, pluginId);
                pluginDir = SanitizePath(pluginDir, "download_plugin");

                if (!Directory.Exists(pluginDir))
                    Directory.CreateDirectory(pluginDir);

                var downloadUrl = pluginId == _pluginId && !string.IsNullOrWhiteSpace(_downloadUrl)
                    ? _downloadUrl
                    : Constants.ApiEndpoints.GetPluginFileDownloadUrl(pluginId);

                var zipPath = Path.Combine(pluginDir, "package.zip");
                zipPath = SanitizePath(zipPath, "download_plugin zip");

                LogMessage($"One-click plugin download [{pluginId}]: {downloadUrl} -> {zipPath}");

                ReportProgress(5, string.Format("PluginStoreDownloading".GetLocalized(), 0));

                try
                {
                    _storeService.DownloadFileAsync(downloadUrl, zipPath,
                        new Progress<DownloadProgressInfo>(p =>
                        {
                            ReportProgress(5 + p.Percent * 70 / 100, p.StatusText, p.BytesDownloaded, p.TotalBytes,
                                p.SpeedBytesPerSecond);
                        }),
                        _expectedFileHash, _dlToken, _accessToken,
                        cancellationToken).GetAwaiter().GetResult();
                    _downloadedFiles.Add(zipPath);
                }
                catch (Exception ex)
                {
                    _installFailure = ex;
                    LogMessage($"Plugin download failed: {ex.Message}");

                    try
                    {
                        if (File.Exists(zipPath)) File.Delete(zipPath);
                    }
                    catch
                    {
                    }

                    result["error"] = DynValue.NewString(ex.Message);
                    return DynValue.NewTable(result);
                }

                LogMessage("Plugin archive downloaded, extracting...");
                ReportProgress(75, "PluginStoreExtracting".GetLocalized());

                try
                {
                    ExtractPluginArchive(zipPath, pluginDir, null, cancellationToken);
                    LogMessage("Extraction complete");
                }
                catch (Exception ex)
                {
                    _installFailure = ex;
                    LogMessage($"Extraction failed: {ex.Message}");
                    result["error"] = DynValue.NewString($"Extract failed: {ex.Message}");
                    return DynValue.NewTable(result);
                }

                try
                {
                    if (File.Exists(zipPath))
                        File.Delete(zipPath);
                    LogMessage("Cleaned up temporary archive file");
                }
                catch (Exception ex)
                {
                    LogMessage($"Failed to clean up archive file: {ex.Message}");
                }

                ReportProgress(90, "PluginStoreWritingConfig".GetLocalized());
                EnsureConfigFileEntry(pluginDir, null);

                ReportProgress(100, "PluginStoreInstallComplete".GetLocalized());
                LogMessage($"Plugin [{pluginId}] downloaded and installed");

                result["success"] = DynValue.NewBoolean(true);
                return DynValue.NewTable(result);
            }
            catch (Exception ex)
            {
                _installFailure = ex;
                LogMessage($"download_plugin error: {ex.Message}");
                result["error"] = DynValue.NewString(ex.Message);
                return DynValue.NewTable(result);
            }
        });

        table["extract"] = (Action<string, string>)((zipPath, destDir) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeZipPath = SanitizePath(zipPath, "extract source");
            var safeDestDir = SanitizePath(destDir, "extract destination");
            LogMessage($"Extract: {safeZipPath} -> {safeDestDir}");

            if (!File.Exists(safeZipPath))
            {
                var msg = $"Archive file not found: {safeZipPath}";
                LogMessage(msg);
                throw new FileNotFoundException(msg);
            }

            try
            {
                ExtractPluginArchive(safeZipPath, safeDestDir, null, cancellationToken);
                LogMessage("Extraction complete");
            }
            catch (Exception ex)
            {
                _installFailure = ex;
                LogMessage($"Extraction failed: {ex.Message}");
                throw;
            }
        });

        table["create_dir"] = (Action<string>)(path =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "create_dir");
            LogMessage($"Create directory: {safePath}");
            if (!Directory.Exists(safePath))
                Directory.CreateDirectory(safePath);
        });

        table["delete"] = (Action<string>)(path =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "delete");
            LogMessage($"Delete: {safePath}");

            if (File.Exists(safePath))
                File.Delete(safePath);
            else if (Directory.Exists(safePath))
                Directory.Delete(safePath, true);
        });

        table["get_plugins_dir"] = (Func<string>)(() =>
        {
            return _pluginsDir;
        });

        table["extract_files"] = (Action<string, DynValue, string>)((zipPath, patterns, destDir) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeZipPath = SanitizePath(zipPath, "extract_files source");
            var safeDestDir = SanitizePath(destDir, "extract_files destination");
            var patternList = TableToStringList(patterns, "extract_files");

            LogMessage($"Extract with patterns: {safeZipPath} -> {safeDestDir}");
            LogMessage($"  filter patterns: {string.Join(", ", patternList)}");

            var extracted = ExtractPluginArchive(safeZipPath, safeDestDir,
                path => patternList.Any(pattern => WildcardMatch(path, pattern) ||
                                                   WildcardMatch(Path.GetFileName(path), pattern)), cancellationToken);

            LogMessage($"Pattern extraction complete, extracted {extracted} files");
        });
    }

    private int ExtractPluginArchive(string path, string destination, Func<string, bool>? include,
        CancellationToken cancellationToken)
    {
        if (_downloadedFiles.Contains(path)) _temporaryArchives.Add(path);
        try
        {
            return _archiveExtractor.ExtractAsync(path, destination, include, cancellationToken)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _installFailure = ex;
            throw;
        }
    }

    #endregion
}