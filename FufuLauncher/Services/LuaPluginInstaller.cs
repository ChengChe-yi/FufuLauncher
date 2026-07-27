/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Messages;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoonSharp.Interpreter;

namespace FufuLauncher.Services;

public class LuaPluginInstaller
{
    private readonly PluginStoreService _storeService;
    private string _pluginsDir;
    private string? _expectedFileHash;
    private string? _expectedLuaHash;
    private string? _dlToken;
    private string? _accessToken;
    public event Action<DownloadProgressInfo>? ProgressChanged;
    public event Action<string>? LogReceived;
    
    public static DispatcherQueue? UIDispatcher { get; set; }
    
    public static XamlRoot? MainXamlRoot { get; set; }
    
    public List<string> CollectedLogs { get; } = new();

    public LuaPluginInstaller(PluginStoreService storeService)
    {
        _storeService = storeService;
        _pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
    }
    
    public void ClearCollectedLogs()
    {
        lock (CollectedLogs)
        {
            CollectedLogs.Clear();
        }
    }
    
    public void SaveLogsToFile(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        lock (CollectedLogs)
        {
            File.WriteAllLines(filePath, CollectedLogs, Encoding.UTF8);
        }
    }
    
    public async Task ExecuteUserScriptAsync(string luaScript,
        CancellationToken cancellationToken = default)
    {
        ClearCollectedLogs();

        LogMessage("开始");
        LogMessage($"脚本长度: {luaScript.Length} 字符");
        LogMessage($"沙箱目录: {_pluginsDir}");

        ReportProgress(0, "正在执行脚本...");

        try
        {
            await ExecuteScriptAsync(luaScript, cancellationToken);
            LogMessage("无异常");
            ReportProgress(100, "执行完成");
        }
        catch (Exception ex)
        {
            LogMessage($"脚本测试异常终止: {ex.Message}");
            ReportProgress(100, "执行失败");
            throw;
        }
    }
    
    public async Task ExecuteInstallScriptAsync(string luaScriptUrl,
        string? expectedLuaHash = null, string? expectedFileHash = null,
        CancellationToken cancellationToken = default,
        string? dllFileName = null, string? pluginId = null,
        string? dlToken = null, string? accessToken = null)
    {
        _expectedLuaHash = expectedLuaHash;
        _expectedFileHash = expectedFileHash;
        _dlToken = dlToken;
        _accessToken = accessToken;

        ReportProgress(0, "PluginStoreScriptDownloading".GetLocalized());
        LogMessage($"Downloading Lua script from: {luaScriptUrl}");

        var luaScript = await _storeService.DownloadLuaScriptAsync(luaScriptUrl, expectedLuaHash, dlToken, accessToken);
        
        ReportProgress(3, "PluginStoreScriptScanning".GetLocalized());
        LogMessage("Running Lua security validation...");
        var securityResult = PluginVerifier.ValidateLuaSecurity(luaScript);
        if (!securityResult.IsValid)
        {
            LogMessage($"SECURITY BLOCK: {securityResult.Reason}");
            throw new SecurityViolationException(securityResult.Reason ?? "PluginStoreLuaSecurityFailed".GetLocalized());
        }
        LogMessage("Lua security scan passed.");

        ReportProgress(5, "PluginStoreScriptExecuting".GetLocalized());
        LogMessage("Executing Lua install script...");

        await ExecuteScriptAsync(luaScript, cancellationToken);
        
        if (!string.IsNullOrEmpty(pluginId))
        {
            var pluginDir = Path.Combine(_pluginsDir, pluginId);
            EnsureConfigFileEntry(pluginDir, dllFileName);
        }
    }
    
    public async Task ExecuteScriptAsync(string luaScript, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            var script = new Script(CoreModules.None);
            
            RegisterInstallApi(script, cancellationToken);
            
            try
            {
                script.DoString(@"
                    ipairs = function(t)
                        local i = 0
                        return function()
                            i = i + 1
                            local v = t[i]
                            if v ~= nil then
                                return i, v
                            end
                        end, t, 0
                    end
                    pairs = function(t)
                        return next, t, nil
                    end
                ");
            }
            catch (InterpreterException ex)
            {
                Debug.WriteLine($"[LuaInstaller] Failed to inject Lua helpers: {ex.Message}");
            }

            try
            {
                script.DoString(luaScript);
            }
            catch (InterpreterException ex)
            {
                if (ex.InnerException is OperationCanceledException oce)
                {
                    Debug.WriteLine($"[LuaInstaller] Lua script cancelled");
                    LogMessage("脚本执行被取消");
                    throw oce;
                }
                Debug.WriteLine($"[LuaInstaller] Lua error: {ex.Message}");
                LogMessage($"Lua脚本错误: {ex.Message}");
                throw new InvalidOperationException(string.Format("PluginStoreLuaScriptFailed".GetLocalized(), ex.Message), ex);
            }
        }, cancellationToken);
    }

    private void RegisterInstallApi(Script script, CancellationToken cancellationToken)
    {
        DynValue installTable = DynValue.NewTable(script);

        var table = installTable.Table;
        
        table["download"] = (Action<string, string>)((url, path) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var safePath = SanitizePath(path, "download");
            LogMessage($"下载: {url} -> {safePath}");
            
            try
            {
                _storeService.DownloadFileAsync(url, safePath,
                    new Progress<DownloadProgressInfo>(p =>
                    {
                        ReportProgress(5 + p.Percent * 70 / 100, p.StatusText, p.BytesDownloaded, p.TotalBytes, p.SpeedBytesPerSecond);
                    }),
                    _expectedFileHash, _dlToken, _accessToken,
                    cancellationToken).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                LogMessage($"下载失败: {ex.Message}");
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
                
                var downloadUrl = Constants.ApiEndpoints.GetPluginFileDownloadUrl(pluginId);
                var zipPath = Path.Combine(pluginDir, "package.zip");
                zipPath = SanitizePath(zipPath, "download_plugin zip");

                LogMessage($"一键下载插件 [{pluginId}]: {downloadUrl} -> {zipPath}");

                ReportProgress(5, string.Format("PluginStoreDownloading".GetLocalized(), 0));

                try
                {
                    _storeService.DownloadFileAsync(downloadUrl, zipPath,
                        new Progress<DownloadProgressInfo>(p =>
                        {
                            ReportProgress(5 + p.Percent * 70 / 100, p.StatusText, p.BytesDownloaded, p.TotalBytes, p.SpeedBytesPerSecond);
                        }),
                        _expectedFileHash, _dlToken, _accessToken,
                        cancellationToken).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    LogMessage($"插件下载失败: {ex.Message}");
                    
                    try { if (File.Exists(zipPath)) File.Delete(zipPath); }
                    catch { }

                    result["error"] = DynValue.NewString(ex.Message);
                    return DynValue.NewTable(result);
                }

                LogMessage("插件 ZIP 下载完成，开始解压...");
                ReportProgress(75, "PluginStoreExtracting".GetLocalized());
                
                try
                {
                    ZipFile.ExtractToDirectory(zipPath, pluginDir, true);
                    LogMessage("解压完成");
                }
                catch (Exception ex)
                {
                    LogMessage($"解压失败: {ex.Message}");
                    result["error"] = DynValue.NewString($"Extract failed: {ex.Message}");
                    return DynValue.NewTable(result);
                }
                
                try
                {
                    if (File.Exists(zipPath))
                        File.Delete(zipPath);
                    LogMessage("已清理临时 ZIP 文件");
                }
                catch (Exception ex)
                {
                    LogMessage($"清理 ZIP 文件失败: {ex.Message}");
                }
                
                ReportProgress(90, "PluginStoreWritingConfig".GetLocalized());
                EnsureConfigFileEntry(pluginDir, null);

                ReportProgress(100, "PluginStoreInstallComplete".GetLocalized());
                LogMessage($"插件 [{pluginId}] 下载安装完成");

                result["success"] = DynValue.NewBoolean(true);
                return DynValue.NewTable(result);
            }
            catch (Exception ex)
            {
                LogMessage($"download_plugin 异常: {ex.Message}");
                result["error"] = DynValue.NewString(ex.Message);
                return DynValue.NewTable(result);
            }
        });

        table["extract"] = (Action<string, string>)((zipPath, destDir) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var safeZipPath = SanitizePath(zipPath, "extract source");
            var safeDestDir = SanitizePath(destDir, "extract destination");
            LogMessage($"解压: {safeZipPath} -> {safeDestDir}");

            if (!File.Exists(safeZipPath))
            {
                var msg = $"zip文件不存在: {safeZipPath}";
                LogMessage(msg);
                throw new FileNotFoundException(msg);
            }

            if (!Directory.Exists(safeDestDir))
                Directory.CreateDirectory(safeDestDir);

            try
            {
                ZipFile.ExtractToDirectory(safeZipPath, safeDestDir, true);
                LogMessage("解压完成");
            }
            catch (Exception ex)
            {
                LogMessage($"解压失败: {ex.Message}");
                throw;
            }
        });
        
        table["create_dir"] = (Action<string>)(path =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "create_dir");
            LogMessage($"创建目录: {safePath}");
            if (!Directory.Exists(safePath))
                Directory.CreateDirectory(safePath);
        });
        
        table["delete"] = (Action<string>)(path =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "delete");
            LogMessage($"删除: {safePath}");

            if (File.Exists(safePath))
                File.Delete(safePath);
            else if (Directory.Exists(safePath))
                Directory.Delete(safePath, true);
        });
        
        table["get_plugins_dir"] = (Func<string>)(() =>
        {
            return _pluginsDir;
        });
        
        table["log"] = (Action<string>)(msg =>
        {
            LogMessage(msg);
        });
        
        table["set_progress"] = (Action<int, string>)((percent, status) =>
        {
            ReportProgress(Math.Clamp(percent, 0, 100), status);
        });
        
        table["write_config"] = (Action<string, DynValue>)((dir, value) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeDir = SanitizePath(dir, "write_config");
            LogMessage($"写入配置: {safeDir}");

            var configPath = Path.Combine(safeDir, "config.ini");
            
            if (!Directory.Exists(safeDir))
                Directory.CreateDirectory(safeDir);

            var iniLines = new StringBuilder();
            if (value.Type == DataType.Table)
            {
                foreach (var sectionPair in value.Table.Pairs)
                {
                    var sectionName = sectionPair.Key.String;
                    var sectionTable = sectionPair.Value.Table;

                    iniLines.AppendLine($"[{sectionName}]");
                    foreach (var kvp in sectionTable.Pairs)
                    {
                        var key = kvp.Key.String;
                        var val = kvp.Value.String;
                        iniLines.AppendLine($"{key} = {val}");
                    }
                    iniLines.AppendLine();
                }
            }

            File.WriteAllText(configPath, iniLines.ToString(), Encoding.UTF8);
            LogMessage("配置写入完成");
        });
        
        table["verify_file_hash"] = (Func<string, string, bool>)((path, expectedHash) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "verify_file_hash");
            LogMessage($"验证文件哈希: {safePath}");

            try
            {
                PluginVerifier.VerifyFileHash(safePath, expectedHash, Path.GetFileName(safePath));
                LogMessage("文件哈希验证通过");
                return true;
            }
            catch (HashMismatchException ex)
            {
                LogMessage($"文件哈希验证失败: {ex.Message}");
                return false;
            }
        });
        
        table["show_notification"] = (Action<string, string, string, int>)((title, message, typeStr, duration) =>
        {
            LogMessage($"通知: [{typeStr}] {title} - {message}");

            var type = typeStr?.ToLowerInvariant() switch
            {
                "success" => NotificationType.Success,
                "warning" => NotificationType.Warning,
                "error" => NotificationType.Error,
                _ => NotificationType.Information
            };

            if (duration <= 0) duration = 5000;

            WeakReferenceMessenger.Default.Send(new NotificationMessage(title, message, type, duration));
        });
        
        table["show_dialog"] = (Func<string, string, string, string, string, string>)((title, content, primaryText, secondaryText, closeText) =>
        {
            LogMessage($"弹窗: {title}");

            var dispatcher = UIDispatcher;
            var xamlRoot = MainXamlRoot;

            if (dispatcher == null)
            {
                LogMessage("弹窗失败: UI 调度器未初始化");
                return "none";
            }

            var tcs = new TaskCompletionSource<string>();

            dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    var dialog = new ContentDialog
                    {
                        Title = title,
                        Content = content,
                        XamlRoot = xamlRoot,
                        DefaultButton = ContentDialogButton.Primary
                    };

                    if (!string.IsNullOrEmpty(primaryText))
                        dialog.PrimaryButtonText = primaryText;
                    if (!string.IsNullOrEmpty(secondaryText))
                        dialog.SecondaryButtonText = secondaryText;
                    if (!string.IsNullOrEmpty(closeText))
                        dialog.CloseButtonText = closeText;
                    else
                        dialog.CloseButtonText = "PluginStoreDialogClose".GetLocalized();

                    var result = await dialog.ShowAsync();
                    tcs.TrySetResult(result.ToString().ToLowerInvariant());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LuaInstaller] Dialog error: {ex.Message}");
                    tcs.TrySetResult("error");
                }
            });

            try
            {
                return tcs.Task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LuaInstaller] Dialog wait error: {ex.Message}");
                return "error";
            }
        });
        
        table["extract_files"] = (Action<string, DynValue, string>)((zipPath, patterns, destDir) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeZipPath = SanitizePath(zipPath, "extract_files source");
            var safeDestDir = SanitizePath(destDir, "extract_files destination");
            var patternList = TableToStringList(patterns, "extract_files");

            LogMessage($"按模式解压: {safeZipPath} -> {safeDestDir}");
            LogMessage($"  过滤模式: {string.Join(", ", patternList)}");

            if (!Directory.Exists(safeDestDir))
                Directory.CreateDirectory(safeDestDir);

            using var archive = ZipFile.OpenRead(safeZipPath);
            int extracted = 0;
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                    continue;
                
                bool matches = false;
                foreach (var pattern in patternList)
                {
                    if (WildcardMatch(entry.FullName, pattern) ||
                        WildcardMatch(entry.Name, pattern))
                    {
                        matches = true;
                        break;
                    }
                }

                if (!matches) continue;

                var destPath = Path.Combine(safeDestDir, entry.FullName);
                var destParent = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destParent) && !Directory.Exists(destParent))
                    Directory.CreateDirectory(destParent);

                entry.ExtractToFile(destPath, true);
                extracted++;
                LogMessage($"  解压: {entry.FullName}");
            }

            LogMessage($"按模式解压完成，共解压 {extracted} 个文件");
        });
        
        table["move_file"] = (Action<string, string>)((source, dest) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeSource = SanitizePath(source, "move_file source");
            var safeDest = SanitizePath(dest, "move_file destination");

            if (!File.Exists(safeSource))
            {
                LogMessage($"移动失败: 源文件不存在: {safeSource}");
                return;
            }
            
            string finalDest;
            if (Directory.Exists(safeDest))
            {
                finalDest = Path.Combine(safeDest, Path.GetFileName(safeSource));
                finalDest = SanitizePath(finalDest, "move_file final destination");
            }
            else
            {
                finalDest = safeDest;
                var parentDir = Path.GetDirectoryName(finalDest);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    Directory.CreateDirectory(parentDir);
            }

            LogMessage($"移动文件: {safeSource} -> {finalDest}");
            
            if (File.Exists(finalDest))
                File.Delete(finalDest);

            File.Move(safeSource, finalDest);
            LogMessage("文件移动完成");
        });
        
        table["move_files"] = (Action<DynValue, string>)((sources, destDir) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceList = TableToStringList(sources, "move_files");
            var safeDestDir = SanitizePath(destDir, "move_files destination");

            if (!Directory.Exists(safeDestDir))
                Directory.CreateDirectory(safeDestDir);

            LogMessage($"批量移动 {sourceList.Count} 个文件到: {safeDestDir}");

            foreach (var src in sourceList)
            {
                var safeSrc = SanitizePath(src, "move_files source");

                if (!File.Exists(safeSrc))
                {
                    LogMessage($"  跳过不存在的文件: {safeSrc}");
                    continue;
                }

                var destPath = Path.Combine(safeDestDir, Path.GetFileName(safeSrc));
                destPath = SanitizePath(destPath, "move_files dest");

                if (File.Exists(destPath))
                    File.Delete(destPath);

                File.Move(safeSrc, destPath);
                LogMessage($"  移动: {Path.GetFileName(safeSrc)}");
            }

            LogMessage("批量移动完成");
        });

        table["copy_file"] = (Action<string, string>)((source, dest) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeSource = SanitizePath(source, "copy_file source");
            var safeDest = SanitizePath(dest, "copy_file destination");

            if (!File.Exists(safeSource))
            {
                LogMessage($"复制失败: 源文件不存在: {safeSource}");
                return;
            }
            
            string finalDest;
            if (Directory.Exists(safeDest))
            {
                finalDest = Path.Combine(safeDest, Path.GetFileName(safeSource));
                finalDest = SanitizePath(finalDest, "copy_file final destination");
            }
            else
            {
                finalDest = safeDest;
                var parentDir = Path.GetDirectoryName(finalDest);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    Directory.CreateDirectory(parentDir);
            }

            LogMessage($"复制文件: {safeSource} -> {finalDest}");
            File.Copy(safeSource, finalDest, true);
            LogMessage("文件复制完成");
        });
        
        table["copy_files"] = (Action<DynValue, string>)((sources, destDir) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceList = TableToStringList(sources, "copy_files");
            var safeDestDir = SanitizePath(destDir, "copy_files destination");

            if (!Directory.Exists(safeDestDir))
                Directory.CreateDirectory(safeDestDir);

            LogMessage($"批量复制 {sourceList.Count} 个文件到: {safeDestDir}");

            foreach (var src in sourceList)
            {
                var safeSrc = SanitizePath(src, "copy_files source");

                if (!File.Exists(safeSrc))
                {
                    LogMessage($"  跳过不存在的文件: {safeSrc}");
                    continue;
                }

                var destPath = Path.Combine(safeDestDir, Path.GetFileName(safeSrc));
                destPath = SanitizePath(destPath, "copy_files dest");

                File.Copy(safeSrc, destPath, true);
                LogMessage($"  复制: {Path.GetFileName(safeSrc)}");
            }

            LogMessage("批量复制完成");
        });
        
        table["rename"] = (Action<string, string>)((oldPath, newName) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeOldPath = SanitizePath(oldPath, "rename source");
            var safeNewName = SanitizeName(newName, "rename");

            var parentDir = Path.GetDirectoryName(safeOldPath);
            if (string.IsNullOrEmpty(parentDir))
            {
                throw new InvalidOperationException("Cannot rename root plugins directory.");
            }

            var newPath = Path.Combine(parentDir, safeNewName);
            newPath = SanitizePath(newPath, "rename destination");

            bool isDirectory = Directory.Exists(safeOldPath);
            bool isFile = File.Exists(safeOldPath);

            if (!isDirectory && !isFile)
            {
                LogMessage($"重命名失败: 路径不存在: {safeOldPath}");
                return;
            }

            if (isDirectory)
            {
                LogMessage($"重命名目录: {safeOldPath} -> {newPath}");
                if (Directory.Exists(newPath))
                {
                    LogMessage($"  目标目录已存在，尝试合并或覆盖...");
                }
                Directory.Move(safeOldPath, newPath);
                LogMessage("目录重命名完成");
            }
            else
            {
                LogMessage($"重命名文件: {safeOldPath} -> {newPath}");
                if (File.Exists(newPath))
                    File.Delete(newPath);
                File.Move(safeOldPath, newPath);
                LogMessage("文件重命名完成");
            }
        });
        
        table["get_file_info"] = (Func<string, DynValue>)(path =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "get_file_info");
            LogMessage($"查询文件信息: {safePath}");

            var infoTable = new Table(script);

            bool exists = File.Exists(safePath) || Directory.Exists(safePath);
            infoTable["exists"] = DynValue.NewBoolean(exists);

            if (!exists)
            {
                infoTable["size"] = DynValue.NewNumber(0);
                infoTable["last_modified"] = DynValue.NewString("");
                infoTable["is_directory"] = DynValue.NewBoolean(false);
                infoTable["hash"] = DynValue.NewString("");
                return DynValue.NewTable(infoTable);
            }

            bool isDir = Directory.Exists(safePath);
            infoTable["is_directory"] = DynValue.NewBoolean(isDir);

            if (isDir)
            {
                var dirInfo = new DirectoryInfo(safePath);
                infoTable["size"] = DynValue.NewNumber(0);
                infoTable["last_modified"] = DynValue.NewString(dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                infoTable["hash"] = DynValue.NewString("");
            }
            else
            {
                var fileInfo = new FileInfo(safePath);
                infoTable["size"] = DynValue.NewNumber(fileInfo.Length);
                infoTable["last_modified"] = DynValue.NewString(fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));

                try
                {
                    var hash = PluginVerifier.ComputeFileSha256(safePath);
                    infoTable["hash"] = DynValue.NewString(hash);
                }
                catch
                {
                    infoTable["hash"] = DynValue.NewString("");
                }
            }

            return DynValue.NewTable(infoTable);
        });
        
        table["compare_files"] = (Func<string, string, DynValue>)((path1, path2) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath1 = SanitizePath(path1, "compare_files path1");
            var safePath2 = SanitizePath(path2, "compare_files path2");

            LogMessage($"比较文件: {safePath1} <-> {safePath2}");

            var result = new Table(script);

            var exists1 = File.Exists(safePath1);
            var exists2 = File.Exists(safePath2);

            if (!exists1 || !exists2)
            {
                result["same"] = DynValue.NewBoolean(false);
                result["same_size"] = DynValue.NewBoolean(false);
                result["same_hash"] = DynValue.NewBoolean(false);
                result["size1"] = DynValue.NewNumber(exists1 ? new FileInfo(safePath1).Length : -1);
                result["size2"] = DynValue.NewNumber(exists2 ? new FileInfo(safePath2).Length : -1);
                result["hash1"] = DynValue.NewString(exists1 ? PluginVerifier.ComputeFileSha256(safePath1) : "");
                result["hash2"] = DynValue.NewString(exists2 ? PluginVerifier.ComputeFileSha256(safePath2) : "");
                return DynValue.NewTable(result);
            }

            var fi1 = new FileInfo(safePath1);
            var fi2 = new FileInfo(safePath2);

            var sameSize = fi1.Length == fi2.Length;
            result["same_size"] = DynValue.NewBoolean(sameSize);
            result["size1"] = DynValue.NewNumber(fi1.Length);
            result["size2"] = DynValue.NewNumber(fi2.Length);

            string hash1, hash2;
            try
            {
                hash1 = PluginVerifier.ComputeFileSha256(safePath1);
                hash2 = PluginVerifier.ComputeFileSha256(safePath2);
            }
            catch
            {
                hash1 = "";
                hash2 = "";
            }

            result["hash1"] = DynValue.NewString(hash1);
            result["hash2"] = DynValue.NewString(hash2);

            var sameHash = string.Equals(hash1, hash2, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(hash1);
            result["same_hash"] = DynValue.NewBoolean(sameHash);
            result["same"] = DynValue.NewBoolean(sameSize && sameHash);

            LogMessage($"比较结果: same_size={sameSize}, same_hash={sameHash}");
            return DynValue.NewTable(result);
        });
        
        table["get_file_hash"] = (Func<DynValue, DynValue, string>)((pathArg, algoArg) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = pathArg.IsNil() ? "" : pathArg.String;
            var algorithm = algoArg.IsNil() ? "" : algoArg.String;

            var safePath = SanitizePath(path, "get_file_hash");
            var algo = string.IsNullOrWhiteSpace(algorithm) ? "sha256" : algorithm.ToLowerInvariant().Trim();

            LogMessage($"计算文件哈希 [{algo}]: {safePath}");

            if (!File.Exists(safePath))
            {
                LogMessage($"文件不存在: {safePath}");
                return "";
            }

            using var stream = File.OpenRead(safePath);
            byte[] hashBytes;

            switch (algo)
            {
                case "md5":
                    hashBytes = MD5.HashData(stream);
                    break;
                case "sha1":
                    hashBytes = SHA1.HashData(stream);
                    break;
                case "sha256":
                default:
                    hashBytes = SHA256.HashData(stream);
                    break;
            }

            var hash = PluginVerifier.BytesToHex(hashBytes);
            LogMessage($"哈希值: {hash}");
            return hash;
        });
        
        table["list_files"] = (Func<string, string, DynValue>)((dir, pattern) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeDir = SanitizePath(dir, "list_files");
            LogMessage($"列出文件: {safeDir}" + (string.IsNullOrEmpty(pattern) ? "" : $" (模式: {pattern})"));

            var fileList = new Table(script);

            if (!Directory.Exists(safeDir))
            {
                LogMessage($"目录不存在: {safeDir}");
                return DynValue.NewTable(fileList);
            }

            var files = Directory.GetFiles(safeDir, "*", SearchOption.TopDirectoryOnly);
            int index = 1;

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);

                if (!string.IsNullOrEmpty(pattern) && !WildcardMatch(fileName, pattern))
                    continue;

                fileList[index] = DynValue.NewString(file);
                index++;
            }

            LogMessage($"找到 {index - 1} 个文件");
            return DynValue.NewTable(fileList);
        });
        
        table["file_exists"] = (Func<string, bool>)(path =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "file_exists");
            var exists = File.Exists(safePath);
            LogMessage($"文件存在检查: {safePath} = {exists}");
            return exists;
        });
        
        table["dir_exists"] = (Func<string, bool>)(path =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "dir_exists");
            var exists = Directory.Exists(safePath);
            LogMessage($"目录存在检查: {safePath} = {exists}");
            return exists;
        });
        
        script.Globals["tostring"] = (Func<DynValue, string>)(value =>
        {
            if (value.IsNil()) return "nil";
            if (value.Type == DataType.String) return value.String ?? "";
            if (value.Type == DataType.Number) return value.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value.Type == DataType.Boolean) return value.Boolean ? "true" : "false";
            if (value.Type == DataType.Function) return "(function)";
            if (value.Type == DataType.Table) return "(table)";
            if (value.Type == DataType.UserData) return "(userdata)";
            if (value.Type == DataType.Thread) return "(thread)";
            var ps = value.ToPrintString();
            return ps ?? "";
        });
        
        script.Globals["print"] = (Action<DynValue>)(value =>
        {
            var str = value.IsNil() ? "nil" :
                value.Type == DataType.String ? value.String :
                value.ToPrintString();
            LogMessage($"[Lua print] {str}");
        });
        
        script.Globals["pcall"] = (Func<DynValue, DynValue>)(fn =>
        {
            if (fn.IsNil() || fn.Type != DataType.Function)
                return DynValue.True;

            try
            {
                script.Call(fn);
            }
            catch (Exception ex)
            {
                string msg;
                try
                {
                    if (ex is InterpreterException iex)
                        msg = iex.Message ?? iex.GetType().Name;
                    else if (ex is TargetInvocationException tie && tie.InnerException != null)
                        msg = tie.InnerException.Message ?? tie.InnerException.GetType().Name;
                    else
                        msg = ex.Message ?? ex.GetType().Name;
                }
                catch { msg = "Unknown error"; }
                LogMessage($"pcall caught: {msg}");
            }

            return DynValue.True;
        });

        script.Globals["install"] = installTable;
        
        DynValue systemTable = DynValue.NewTable(script);
        var sysTable = systemTable.Table;

        sysTable["get_gpu"] = (Func<DynValue>)(() =>
        {
            var info = SystemEnvironmentHelper.GetGpuInfo();
            var table = new Table(script);
            table["name"] = DynValue.NewString(info.Name);
            table["vendor"] = DynValue.NewString(info.Vendor);
            table["family"] = DynValue.NewString(info.Family);
            table["series"] = DynValue.NewString(info.Series);
            table["raw"] = DynValue.NewString(info.Name);
            return DynValue.NewTable(table);
        });
        
        sysTable["gpu_matches"] = (Func<DynValue, bool>)(rule =>
        {
            try
            {
                var info = SystemEnvironmentHelper.GetGpuInfo();
                if (rule.IsNil() || rule.Type != DataType.Table)
                {
                    return true;
                }

                var dict = TableToDict(rule, "gpu_matches");
                if (dict.Count == 0)
                {
                    return true;
                }
                return MatchesGpuRule(info, dict);
            }
            catch (Exception ex)
            {
                LogMessage($"gpu_matches 执行出错: {ex.Message}");
                return false;
            }
        });
        
        sysTable["gpu_matches_any"] = (Func<DynValue, bool>)(rules =>
        {
            try
            {
                var info = SystemEnvironmentHelper.GetGpuInfo();
                if (rules.IsNil() || rules.Type != DataType.Table)
                {
                    return true;
                }

                var anyRule = false;
                foreach (var pair in rules.Table.Pairs)
                {
                    var ruleValue = pair.Value;
                    if (ruleValue.IsNil() || ruleValue.Type != DataType.Table)
                    {
                        continue;
                    }

                    anyRule = true;
                    var dict = TableToDict(ruleValue, "gpu_matches_any rule");
                    if (MatchesGpuRule(info, dict))
                    {
                        return true;
                    }
                }
                return !anyRule;
            }
            catch (Exception ex)
            {
                LogMessage($"gpu_matches_any 执行出错: {ex.Message}");
                return false;
            }
        });

        sysTable["get_cpu"] = (Func<DynValue>)(() =>
        {
            var table = new Table(script);
            table["name"] = DynValue.NewString(SystemEnvironmentHelper.GetCpuName());
            return DynValue.NewTable(table);
        });

        sysTable["get_memory"] = (Func<DynValue>)(() =>
        {
            var table = new Table(script);
            table["total_gb"] = DynValue.NewNumber(SystemEnvironmentHelper.GetTotalMemoryGB());
            return DynValue.NewTable(table);
        });

        sysTable["get_os"] = (Func<DynValue>)(() =>
        {
            var table = new Table(script);
            table["version"] = DynValue.NewString(SystemEnvironmentHelper.GetOsVersion());
            return DynValue.NewTable(table);
        });

        script.Globals["system"] = systemTable;
    }
    
    private string SanitizePath(string rawPath, string operation)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new SecurityViolationException(string.Format("PluginStoreSecurityEmptyPath".GetLocalized(), operation));
        }
        
        if (rawPath.Contains(".."))
        {
            Debug.WriteLine($"[LuaInstaller] SECURITY: Path traversal attempt blocked in {operation}: {rawPath}");
            throw new SecurityViolationException(
                string.Format("PluginStorePathTraversal".GetLocalized()));
        }
        
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(rawPath);
        }
        catch (Exception ex)
        {
            throw new SecurityViolationException(
                string.Format("PluginStoreSecurityInvalidPath".GetLocalized(), operation, ex.Message));
        }
        
        var pluginsDirFull = Path.GetFullPath(_pluginsDir);
        
        if (!fullPath.StartsWith(pluginsDirFull, StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"[LuaInstaller] SECURITY: Path outside plugins dir blocked in {operation}: {fullPath}");
            throw new SecurityViolationException(
                string.Format("PluginStoreSecurityOutsideDir".GetLocalized(), operation));
        }

        return fullPath;
    }
    
    private static string SanitizeName(string name, string operation)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SecurityViolationException(
                string.Format("PluginStoreSecurityEmptyPath".GetLocalized(), operation));
        }

        if (name.Contains("..") ||
            name.Contains('/') || name.Contains('\\') ||
            name.Contains(':') || name.Contains('*') ||
            name.Contains('?') || name.Contains('"') ||
            name.Contains('<') || name.Contains('>') || name.Contains('|'))
        {
            Debug.WriteLine($"[LuaInstaller] SECURITY: Invalid name characters in {operation}: {name}");
            throw new SecurityViolationException(
                string.Format("PluginStorePathTraversal".GetLocalized()));
        }

        return name;
    }
    
    private static bool WildcardMatch(string input, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return string.IsNullOrEmpty(input);
        
        if (pattern.Length > 500)
            throw new SecurityViolationException("Wildcard pattern too long.");
        
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        try
        {
            return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
        }
        catch (RegexMatchTimeoutException)
        {
            Debug.WriteLine($"[LuaInstaller] Wildcard match timeout for pattern: {pattern}");
            return false;
        }
    }
    
    private static List<string> TableToStringList(DynValue tableValue, string operation)
    {
        var result = new List<string>();

        if (tableValue.Type != DataType.Table)
        {
            throw new InvalidOperationException(
                $"'{operation}' expects a table (array) of strings.");
        }

        var table = tableValue.Table;
        for (int i = 1; ; i++)
        {
            var entry = table.Get(i);
            if (entry.IsNil())
                break;

            if (entry.Type != DataType.String)
            {
                throw new InvalidOperationException(
                    $"'{operation}' expects all table entries to be strings.");
            }

            result.Add(entry.String);
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException(
                $"'{operation}' expects a non-empty table.");
        }

        return result;
    }
    
    private static Dictionary<string, string> TableToDict(DynValue tableValue, string operation)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        if (tableValue.IsNil())
        {
            return result;
        }

        if (tableValue.Type != DataType.Table)
        {
            throw new InvalidOperationException(
                $"'{operation}' expects a table of string key/value pairs.");
        }

        var table = tableValue.Table;
        foreach (var pair in table.Pairs)
        {
            var key = pair.Key;
            if (key.Type != DataType.String)
            {
                continue;
            }

            var value = pair.Value;
            if (value.IsNil())
            {
                continue;
            }

            string strValue;
            if (value.Type == DataType.String)
            {
                strValue = value.String;
            }
            else
            {
                strValue = value.ToString();
            }

            if (strValue == null)
            {
                continue;
            }

            result[key.String] = strValue;
        }

        return result;
    }
    
    private static bool MatchesGpuRule(GpuInfo info, IReadOnlyDictionary<string, string> rule)
    {
        if (rule == null || rule.Count == 0)
        {
            return true;
        }

        if (rule.TryGetValue("vendor", out var vendorValue)
            && !string.IsNullOrEmpty(vendorValue)
            && !string.Equals(info.Vendor, vendorValue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.TryGetValue("family", out var familyValue)
            && !string.IsNullOrEmpty(familyValue))
        {
            var familyOk = !string.IsNullOrEmpty(info.Family)
                           && !string.Equals(info.Family, "Unknown", StringComparison.OrdinalIgnoreCase)
                           && string.Equals(info.Family, familyValue, StringComparison.OrdinalIgnoreCase);
            if (!familyOk)
            {
                familyOk = info.Name != null
                           && info.Name.Contains(familyValue, StringComparison.OrdinalIgnoreCase);
            }
            if (!familyOk) return false;
        }

        if (rule.TryGetValue("series", out var seriesValue)
            && !string.IsNullOrEmpty(seriesValue))
        {
            var seriesOk = !string.IsNullOrEmpty(info.Series)
                           && string.Equals(info.Series, seriesValue, StringComparison.OrdinalIgnoreCase);
            if (!seriesOk)
            {
                seriesOk = info.Name != null
                           && info.Name.Contains(seriesValue, StringComparison.OrdinalIgnoreCase);
            }
            if (!seriesOk) return false;
        }

        if (rule.TryGetValue("name", out var nameValue)
            && !string.IsNullOrEmpty(nameValue))
        {
            if (info.Name == null
                || !info.Name.Contains(nameValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public void EnsureConfigFileEntry(string pluginDir, string? dllFileName = null)
    {
        if (string.IsNullOrWhiteSpace(pluginDir) || !Directory.Exists(pluginDir))
            return;

        var configPath = Path.Combine(pluginDir, "config.ini");
        
        if (!File.Exists(configPath))
        {
            var resolvedDll = ResolveDllFileName(pluginDir, dllFileName);
            if (string.IsNullOrEmpty(resolvedDll)) return;

            var content = $"[General]\nName = {Path.GetFileName(pluginDir)}\nFile = {resolvedDll}\n";
            File.WriteAllText(configPath, content, Encoding.UTF8);
            LogMessage($"已创建 config.ini 并写入 File = {resolvedDll}");
            return;
        }
        
        var lines = File.ReadAllLines(configPath, Encoding.UTF8);
        bool inGeneral = false;
        bool hasFileEntry = false;
        int generalEndIndex = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();

            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                if (inGeneral)
                {
                    generalEndIndex = i;
                    break;
                }
                inGeneral = trimmed.Equals("[General]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inGeneral)
            {
                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex > 0)
                {
                    var key = trimmed.Substring(0, separatorIndex).Trim();
                    if (key.Equals("File", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = trimmed.Substring(separatorIndex + 1).Trim();
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            hasFileEntry = true;
                            break;
                        }
                    }
                }
            }
        }

        if (hasFileEntry) return;
        
        var dllName = ResolveDllFileName(pluginDir, dllFileName);
        if (string.IsNullOrEmpty(dllName)) return;

        var lineList = new List<string>(lines);
        var insertLine = $"File = {dllName}";

        if (generalEndIndex > 0)
        {
            lineList.Insert(generalEndIndex, insertLine);
        }
        else if (inGeneral)
        {
            lineList.Add(insertLine);
        }
        else
        {
            lineList.Insert(0, "[General]");
            lineList.Insert(1, insertLine);
            lineList.Insert(2, "");
        }

        File.WriteAllLines(configPath, lineList, Encoding.UTF8);
        LogMessage($"已补全 config.ini File = {dllName}");
    }

    private static string? ResolveDllFileName(string pluginDir, string? dllFileName)
    {
        if (!string.IsNullOrWhiteSpace(dllFileName))
            return dllFileName;

        var dllFile = Directory.GetFiles(pluginDir, "*.dll", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => !f.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase));

        return dllFile != null ? Path.GetFileName(dllFile) : null;
    }

    private void ReportProgress(double percent, string status, long bytesDownloaded = 0, long totalBytes = -1, long speed = 0)
    {
        var info = new DownloadProgressInfo
        {
            Percent = percent,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes,
            SpeedBytesPerSecond = speed,
            StatusText = status
        };
        Debug.WriteLine($"[LuaInstaller] Progress {percent:F1}% ({FormatSize(bytesDownloaded)}/{FormatSize(totalBytes)} @ {FormatSpeed(speed)}): {status}");
        ProgressChanged?.Invoke(info);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "?";
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1_024 => $"{bytes / 1_024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }

    private static string FormatSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond <= 0) return "?";
        return bytesPerSecond switch
        {
            >= 1_048_576 => $"{bytesPerSecond / 1_048_576.0:F1} MB/s",
            >= 1_024 => $"{bytesPerSecond / 1_024.0:F1} KB/s",
            _ => $"{bytesPerSecond} B/s"
        };
    }

    private void LogMessage(string message)
    {
        Debug.WriteLine($"[LuaInstaller] {message}");
        LogReceived?.Invoke(message);

        lock (CollectedLogs)
        {
            CollectedLogs.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }
    }
}
