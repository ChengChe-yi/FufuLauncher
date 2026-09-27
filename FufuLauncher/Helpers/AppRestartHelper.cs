/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;

namespace FufuLauncher.Helpers;

/// <summary>
/// 进程重启。存储路径、语言等只在启动时确定的配置，改动后需要重启才能生效。
/// </summary>
public static class AppRestartHelper
{
    private const string RestartArgument = "restart";

    /// <summary>
    /// 启动新进程并退出当前进程。返回 true 表示重启已发起（当前进程随即退出）；
    /// 返回 false 表示启动失败，调用方应继续以当前路径运行并提示用户手动重启。
    /// </summary>
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
