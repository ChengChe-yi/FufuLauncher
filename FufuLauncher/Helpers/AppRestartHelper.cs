/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;

namespace FufuLauncher.Helpers;

public static class AppRestartHelper
{
    private const string RestartArgument = "restart";


    public static bool TryRestart()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                Debug.WriteLine("重启应用失败: 无法获取当前进程路径");
                return false;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = RestartArgument,
                    UseShellExecute = true
                }
            };
            process.Start();

            Environment.Exit(0);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"重启应用失败: {ex.Message}");
            return false;
        }
    }
}