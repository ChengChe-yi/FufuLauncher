using System.Diagnostics;
using System.Runtime.InteropServices;
using FufuLauncher.Helpers;

namespace FufuLauncher.Services.Backpack;

internal static partial class GameLaunchService
{
    private const string DllFileName = "backpack.dll";

    public static bool IsGameRunning()
        => Process.GetProcessesByName("YuanShen").Length > 0;

    public static string? GetDllPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "modules", DllFileName);
        return File.Exists(path) ? path : null;
    }

    public static async Task<int> LaunchAsync(string gameExePath)
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException(BackpackLocalization.Get("ErrX64Required"));

        var dllPath = GetDllPath()
            ?? throw new FileNotFoundException(BackpackLocalization.Get("ErrDllMissing"));
        var gameDir    = Path.GetDirectoryName(gameExePath) ?? AppContext.BaseDirectory;
        var cfgFile    = Path.Combine(Path.GetTempPath(), $"BackpackViewer_{Guid.NewGuid():N}.tmp");
        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException(BackpackLocalization.Get("ErrNoProcessPath"));

        File.WriteAllLines(cfgFile, [
            gameExePath,
            dllPath,
            gameDir,
            string.Empty,
            "0",
        ]);

        var psi = new ProcessStartInfo
        {
            FileName         = currentExe,
            Arguments        = $"--backpack-elevated-inject \"{cfgFile}\"",
            UseShellExecute  = true,
            Verb             = "runas",
            WorkingDirectory = Path.GetDirectoryName(currentExe),
        };

        return await Task.Run(() =>
        {
            Process? helper;
            try
            {
                helper = Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                TryDelete(cfgFile);
                throw new InvalidOperationException(BackpackLocalization.Get("ErrElevationCancelled"));
            }

            if (helper is null)
            {
                TryDelete(cfgFile);
                throw new InvalidOperationException(BackpackLocalization.Get("ErrHelperStartFailed"));
            }

            using (helper)
            {
                helper.WaitForExit();
                int code = helper.ExitCode;
                if (code != 0)
                {
                    TryDelete(cfgFile);
                    throw new InvalidOperationException(code switch
                    {
                        1 => BackpackLocalization.Get("ErrInvalidConfig"),
                        2 => BackpackLocalization.Get("ErrGameCreateFailed"),
                        3 => BackpackLocalization.Get("ErrDllInjFailed"),
                        _ => string.Format(BackpackLocalization.Get("ErrHelperExitCode"), code),
                    });
                }

                int gamePid = 0;
                try { int.TryParse(File.ReadAllText(cfgFile).Trim(), out gamePid); } catch { }
                TryDelete(cfgFile);
                return gamePid;
            }
        });
    }

    public static int RunElevatedInjection(string configFile)
    {
        try
        {
            if (!File.Exists(configFile)) return 1;

            string[] lines = File.ReadAllLines(configFile);
            if (lines.Length < 5) return 1;

            string gameExePath = lines[0];
            string dllPath     = lines[1];
            string workDir     = lines[2];
            string cmdArgs     = lines[3];

            int customCount = int.TryParse(lines[4], out int cnt) ? cnt : 0;
            var customDlls  = new List<string>();
            for (int i = 0; i < customCount && (5 + i) < lines.Length; i++)
                if (File.Exists(lines[5 + i]))
                    customDlls.Add(lines[5 + i]);

            string fullCmd = string.IsNullOrEmpty(cmdArgs)
                ? $"\"{gameExePath}\""
                : $"\"{gameExePath}\" {cmdArgs}";

            STARTUPINFOW si = new();
            si.cb = (uint)System.Runtime.InteropServices.Marshal.SizeOf<STARTUPINFOW>();

            if (!NativeMethods.CreateProcessW(
                gameExePath, fullCmd, 0, 0, false, 0x4, 0, workDir, ref si, out PROCESS_INFORMATION pi))
                return 2;

            if (!string.IsNullOrEmpty(dllPath) && !InjectDll(pi.hProcess, dllPath))
            {
                NativeMethods.TerminateProcess(pi.hProcess, 1);
                NativeMethods.CloseHandle(pi.hThread);
                NativeMethods.CloseHandle(pi.hProcess);
                return 3;
            }

            foreach (var dll in customDlls)
                InjectDll(pi.hProcess, dll);

            NativeMethods.ResumeThread(pi.hThread);
            NativeMethods.CloseHandle(pi.hThread);
            File.WriteAllText(configFile, pi.dwProcessId.ToString());
            NativeMethods.CloseHandle(pi.hProcess);
            return 0;
        }
        catch
        {
            return 99;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }
}
