/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;

namespace FufuLauncher.Helpers
{
    public static class PluginFolderHelper
    {
        public static void CheckAndCreatePluginsFolder()
        {
            try
            {
                var pluginsPath = Path.Combine(AppContext.BaseDirectory, "Plugins");

                if (!Directory.Exists(pluginsPath))
                {
                    Directory.CreateDirectory(pluginsPath);
                    DebugLog.Info("PluginFolderHelper", "Plugins folder created automatically");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Info("PluginFolderHelper", $"Failed to create Plugins folder: {ex.Message}");
            }
        }
    }
}