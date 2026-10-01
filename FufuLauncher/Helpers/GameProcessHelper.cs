/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;

namespace FufuLauncher.Helpers;

public static class GameProcessHelper
{
    public static async Task<bool> IsGameRunningAsync()
    {
        try
        {
            var exeNames = await GameExeManager.GetExeNamesAsync();

            foreach (var exeName in exeNames)
            {
                var processName = Path.GetFileNameWithoutExtension(exeName);
                if (string.IsNullOrWhiteSpace(processName))
                {
                    continue;
                }

                foreach (var process in Process.GetProcessesByName(processName))
                {
                    using (process)
                    {
                        if (!process.HasExited)
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameProcessHelper] 检查游戏进程失败: {ex.Message}");
        }

        return false;
    }
}
