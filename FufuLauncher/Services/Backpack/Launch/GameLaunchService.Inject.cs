/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text;

namespace FufuLauncher.Services.Backpack;

internal static partial class GameLaunchService
{
    private static bool InjectDll(nint hProcess, string dllPath)
    {
        byte[] bytes = Encoding.Unicode.GetBytes(dllPath + "\0");

        nint mem = NativeMethods.VirtualAllocEx(hProcess, 0, (nuint)bytes.Length, 0x3000, 0x04);
        if (mem == 0) return false;

        if (!NativeMethods.WriteProcessMemory(hProcess, mem, bytes, (nuint)bytes.Length, out _))
        {
            NativeMethods.VirtualFreeEx(hProcess, mem, 0, 0x8000);
            return false;
        }

        nint k32   = NativeMethods.GetModuleHandleW("kernel32.dll");
        nint loadW = NativeMethods.GetProcAddress(k32, "LoadLibraryW");
        if (loadW == 0)
        {
            NativeMethods.VirtualFreeEx(hProcess, mem, 0, 0x8000);
            return false;
        }

        nint thread = NativeMethods.CreateRemoteThread(hProcess, 0, 0, loadW, mem, 0, out _);
        if (thread == 0)
        {
            NativeMethods.VirtualFreeEx(hProcess, mem, 0, 0x8000);
            return false;
        }

        NativeMethods.WaitForSingleObject(thread, 10000);
        NativeMethods.GetExitCodeThread(thread, out uint result);
        NativeMethods.CloseHandle(thread);
        NativeMethods.VirtualFreeEx(hProcess, mem, 0, 0x8000);
        return result != 0;
    }
}
