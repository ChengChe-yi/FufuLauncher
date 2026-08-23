/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.

Yae 成就读取编排：
- 主进程：校验游戏状态 → 获取 Yae 元数据 → 计算游戏哈希 → 解析 RVA 配置
  → 写配置文件 → runas 启动提权子进程（--yae-inject）→ 读取 UIAF 结果。
- 提权子进程：读配置 → 建管道 → 注入 Yae DLL → 收集数据 → 解析 → 写 UIAF 结果文件。
架构复用背包功能的"提权子进程 + 配置文件/结果文件交换"模式。
*/
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace FufuLauncher.Services.Yae;

public static class YaeAchievementReader
{
    private static class ExitCode
    {
        public const int Success = 0;
        public const int InvalidConfig = 1;
        public const int GameCreateFailed = 2;
        public const int InjectionFailed = 3;
        public const int NoAchievementData = 4;
        public const int Unexpected = 5;
    }

    /// <summary>
    /// 通过 Embedded Yae 从游戏内导入成就并返回 UIAF 结果（主进程入口）。
    /// </summary>
    public static async Task<YaeUiafResult?> ReadAchievementsAsync(string gameExePath, CancellationToken cancellationToken = default)
    {
        if (IsGameRunning())
        {
            throw new ApplicationException("检测到游戏正在运行，请先退出游戏后再读取成就。");
        }
        if (!File.Exists(gameExePath))
        {
            throw new ApplicationException("未找到游戏主程序，请在设置中确认游戏安装目录。");
        }

        // 直接注入安装目录下的 YaeAchievementLib.dll，与背包模块（modules\backpack.dll）一致，
        // 不做 AppData 临时拷贝。
        var dllPath = ResolveYaeDll()
            ?? throw new ApplicationException("未找到 YaeAchievementLib.dll 组件，请重新安装本程序。");

        var metadata = await YaeMetadataService.GetMetadataAsync(cancellationToken).ConfigureAwait(false);
        var gameHash = YaeMetadataService.ComputeGameHash(gameExePath);
        var nativeConfig = YaeMetadataService.Resolve(metadata, gameHash);

        var cfgFile = Path.Combine(Path.GetTempPath(), $"YaeRead_{Guid.NewGuid():N}.tmp");
        var resultFile = Path.Combine(Path.GetTempPath(), $"YaeResult_{Guid.NewGuid():N}.json");
        var errorFile = Path.Combine(Path.GetTempPath(), $"YaeError_{Guid.NewGuid():N}.err");

        try
        {
            YaeReadConfig.Write(cfgFile, new YaeReadConfig
            {
                GameExePath = gameExePath,
                DllPath = dllPath,
                GameDir = Path.GetDirectoryName(gameExePath) ?? AppContext.BaseDirectory,
                ResultFilePath = resultFile,
                ErrorFilePath = errorFile,
                NativeConfig = nativeConfig,
                PbInfo = metadata.PbInfo,
            });

            var currentExe = Environment.ProcessPath
                ?? throw new InvalidOperationException("无法定位当前程序路径。");
            var psi = new ProcessStartInfo
            {
                FileName = currentExe,
                Arguments = $"--yae-inject \"{cfgFile}\"",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(currentExe),
            };

            Process? helper;
            try
            {
                helper = Process.Start(psi);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                throw new InvalidOperationException("已取消管理员权限确认。");
            }

            if (helper is null)
            {
                throw new InvalidOperationException("无法启动提权读取进程。");
            }

            using (helper)
            {
                helper.WaitForExit();
                var exitCode = helper.ExitCode;
                if (exitCode != ExitCode.Success)
                {
                    var detail = File.Exists(errorFile)
                        ? await File.ReadAllTextAsync(errorFile, cancellationToken).ConfigureAwait(false)
                        : string.Empty;
                    throw new InvalidOperationException(exitCode switch
                    {
                        ExitCode.InvalidConfig => "读取配置文件无效。",
                        ExitCode.GameCreateFailed => "游戏进程创建失败。",
                        ExitCode.InjectionFailed => "Yae 组件注入失败。",
                        ExitCode.NoAchievementData => "未能从游戏获取成就数据，请确认已进入游戏世界。",
                        _ => $"读取失败（错误码 {exitCode}）。",
                    } + (string.IsNullOrEmpty(detail) ? string.Empty : $"\n{detail}"));
                }
            }

            if (!File.Exists(resultFile))
            {
                throw new InvalidOperationException("未能生成成就读取结果。");
            }

            var json = await File.ReadAllTextAsync(resultFile, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<YaeUiafResult>(json);
        }
        finally
        {
            TryDelete(cfgFile);
            TryDelete(resultFile);
            TryDelete(errorFile);
        }
    }

    /// <summary>
    /// 提权子进程入口（--yae-inject）。
    /// </summary>
    public static int RunElevatedInjection(string configFile)
    {
        string? errorFilePath = null;
        try
        {
            if (!File.Exists(configFile))
            {
                return ExitCode.InvalidConfig;
            }

            var cfg = YaeReadConfig.Read(File.ReadAllLines(configFile));
            if (cfg is null)
            {
                return ExitCode.InvalidConfig;
            }
            errorFilePath = cfg.ErrorFilePath;

            var pipe = new YaeNamedPipeServer(cfg.NativeConfig);
            using var game = new YaeGameProcess(cfg.GameExePath, cfg.DllPath);
            pipe.AttachGame(game);

            var data = pipe.Collect();
            var achievement = data.FirstOrDefault(d => d.Kind == YaeCommandKind.ResponseAchievement);
            if (achievement is null)
            {
                WriteError(errorFilePath, "未获取到成就数据。");
                return ExitCode.NoAchievementData;
            }

            var items = YaeAchievementParser.ParseAchievement(achievement.Payload, cfg.PbInfo);
            var result = new YaeUiafResult
            {
                Info = new YaeUiafInfo
                {
                    ExportApp = "FufuLauncher",
                    ExportAppVersion = typeof(YaeAchievementReader).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                    UiafVersion = "v1.1",
                    ExportTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                },
                List = items,
            };

            File.WriteAllText(cfg.ResultFilePath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return ExitCode.Success;
        }
        catch (YaeGameCreateException ex)
        {
            WriteError(errorFilePath, ex.Message);
            return ExitCode.GameCreateFailed;
        }
        catch (YaeInjectionException ex)
        {
            WriteError(errorFilePath, ex.Message);
            return ExitCode.InjectionFailed;
        }
        catch (Exception ex)
        {
            WriteError(errorFilePath, ex.Message);
            return ExitCode.Unexpected;
        }
    }

    private static bool IsGameRunning()
        => Process.GetProcessesByName("YuanShen").Length > 0 || Process.GetProcessesByName("GenshinImpact").Length > 0;

    private static string? ResolveYaeDll()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "YaeAchievementLib.dll"),
            Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "YaeAchievementLib.dll"),
            Path.Combine(AppContext.BaseDirectory, "modules", "YaeAchievementLib.dll"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static void WriteError(string? errorFilePath, string message)
    {
        if (string.IsNullOrEmpty(errorFilePath)) return;
        try { File.WriteAllText(errorFilePath, message); } catch { /* ignore */ }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* ignore */ }
    }
}
