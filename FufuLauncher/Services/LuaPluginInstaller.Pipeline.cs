/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Helpers;
using MoonSharp.Interpreter;

namespace FufuLauncher.Services;

public partial class LuaPluginInstaller
{
    #region Script Execution Pipeline

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

        RegisterInstallHandlers(script, table, cancellationToken);
        RegisterFileOperationHandlers(table, cancellationToken);
        RegisterFileInfoHandlers(script, table, cancellationToken);
        RegisterUtilityHandlers(table, cancellationToken);
        RegisterUiHandlers(table);
        RegisterGlobalHandlers(script);

        script.Globals["install"] = installTable;

        DynValue systemTable = DynValue.NewTable(script);
        var sysTable = systemTable.Table;

        RegisterSystemHandlers(script, sysTable);

        script.Globals["system"] = systemTable;
    }

    #endregion
}
