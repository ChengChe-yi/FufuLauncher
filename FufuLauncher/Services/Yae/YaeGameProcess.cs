/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.

以 CREATE_SUSPENDED 启动游戏进程并注入 YaeAchievementLib.dll：
1. LoadLibraryW 远程线程加载 DLL；
2. 以 LoadLibraryW 线程退出码（HMODULE）作为 DLL 在目标进程的基址；
3. 在本进程 DONT_RESOLVE_DLL_REFERENCES 加载 DLL 计算 YaeMain RVA；
4. 在目标进程以 base + YaeMainRVA 创建远程线程执行入口。
注入流程参考 HolographicHat/YaeAchievement (GPL-3.0)。
*/
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace FufuLauncher.Services.Yae;

internal sealed class YaeGameProcess : IDisposable
{
    public uint Id
    {
        get;
    }
    public nint MainThreadHandle
    {
        get;
    }

    private readonly nint _processHandle;
    private readonly nint _mainThreadHandle;
    private readonly nint _entryThreadHandle;
    private bool _disposed;

    public YaeGameProcess(string gameExePath, string dllPath)
    {
        var startupInfo = new YaeNative.StartupInfoW
        {
            cb = (uint)System.Runtime.InteropServices.Marshal.SizeOf<YaeNative.StartupInfoW>(),
        };
        var workDir = Path.GetDirectoryName(gameExePath) ?? AppContext.BaseDirectory;
        var commandLine = $"\"{gameExePath}\"";

        if (!YaeNative.CreateProcessW(
                gameExePath, commandLine, 0, 0, false, YaeNative.CreateSuspended, 0, workDir,
                ref startupInfo, out var processInfo))
        {
            throw new YaeGameCreateException($"创建游戏进程失败：{Marshal.GetLastPInvokeErrorMessage()}");
        }

        _processHandle = processInfo.hProcess;
        _mainThreadHandle = processInfo.hThread;
        Id = processInfo.dwProcessId;

        try
        {
            var targetBase = InjectDllAndGetBase(processInfo.hProcess, dllPath);
            var entryRva = ResolveYaeMainRva(dllPath);
            _entryThreadHandle = StartRemoteThread(processInfo.hProcess, targetBase + entryRva);
        }
        catch (Exception ex)
        {
            YaeNative.TerminateProcess(_processHandle, 1);
            CloseHandles();
            throw new YaeInjectionException($"注入 Yae 组件失败：{ex.Message}", ex);
        }
    }

    /// <summary>进程是否仍在运行（非阻塞）。</summary>
    public bool IsRunning => YaeNative.WaitForSingleObject(_processHandle, 0) == 0x102; // WAIT_TIMEOUT

    /// <summary>恢复游戏主线程（对应管道 0xFE）。</summary>
    public void ResumeMainThread() => YaeNative.ResumeThread(_mainThreadHandle);

    /// <summary>结束游戏进程（对应会话结束）。</summary>
    public void Kill() => YaeNative.TerminateProcess(_processHandle, 0);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseHandles();
    }

    private void CloseHandles()
    {
        YaeNative.CloseHandle(_processHandle);
        YaeNative.CloseHandle(_mainThreadHandle);
        if (_entryThreadHandle != 0) YaeNative.CloseHandle(_entryThreadHandle);
    }

    private static nint InjectDllAndGetBase(nint hProcess, string dllPath)
    {
        var libPathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
        var libPathLen = (uint)libPathBytes.Length;
        var remotePath = YaeNative.VirtualAllocEx(hProcess, 0, libPathLen, 0x3000, 0x04);
        if (remotePath == 0)
        {
            throw new Win32Exception("VirtualAllocEx failed.");
        }

        if (!YaeNative.WriteProcessMemory(hProcess, remotePath, libPathBytes, libPathLen, out _))
        {
            throw new Win32Exception("WriteProcessMemory failed.");
        }

        var kernel32 = YaeNative.GetModuleHandleW("kernel32.dll");
        var loadLibraryW = YaeNative.GetProcAddress(kernel32, "LoadLibraryW");
        if (loadLibraryW == 0)
        {
            throw new Win32Exception("GetProcAddress(LoadLibraryW) failed.");
        }

        var loadThread = YaeNative.CreateRemoteThread(hProcess, 0, 0, loadLibraryW, remotePath, 0, out _);
        if (loadThread == 0)
        {
            throw new Win32Exception("CreateRemoteThread(LoadLibraryW) failed.");
        }

        try
        {
            // DLL 加载可能因杀软扫描等原因超过 2s，放宽到 10s。
            // 等待远程 LoadLibraryW 完成，其退出码即目标进程中 DLL 的 HMODULE（模块基址）。
            // 直接以该基址启动 YaeMain，不再枚举进程模块检测基址——
            // 反作弊驱动可能过滤 ToolHelp/psapi 枚举，造成"模块已加载但枚举不到"的假阴性
            // （此前实测的"未在目标进程中找到Yae模块"）。
            if (YaeNative.WaitForSingleObject(loadThread, 10000) != 0) // WAIT_TIMEOUT
            {
                throw new Win32Exception($"远程 LoadLibraryW 在 10s 内未完成，DLL 加载超时：{dllPath}");
            }

            YaeNative.GetExitCodeThread(loadThread, out uint moduleBase);
            YaeNative.VirtualFreeEx(hProcess, remotePath, 0, 0x8000);

            if (moduleBase == 0)
            {
                throw new Win32Exception($"远程 LoadLibraryW 返回 NULL，DLL 加载失败：{dllPath}");
            }

            return (nint)moduleBase;
        }
        finally
        {
            YaeNative.CloseHandle(loadThread);
        }
    }

    private static nint ResolveYaeMainRva(string dllPath)
    {
        var localHandle = YaeNative.LoadLibraryEx(dllPath, 0, YaeNative.DontResolveDllReferences);
        if (localHandle == 0)
        {
            throw new Win32Exception("LoadLibraryEx(DONT_RESOLVE_DLL_REFERENCES) failed.");
        }

        var mainProc = YaeNative.GetProcAddress(localHandle, "YaeMain");
        if (mainProc == 0)
        {
            throw new Win32Exception("GetProcAddress(YaeMain) failed.");
        }

        return mainProc - localHandle;
    }

    private static nint StartRemoteThread(nint hProcess, nint startAddress)
    {
        var thread = YaeNative.CreateRemoteThread(hProcess, 0, 0, startAddress, 0, 0, out _);
        if (thread == 0)
        {
            throw new Win32Exception("CreateRemoteThread(YaeMain) failed.");
        }
        return thread;
    }
}
