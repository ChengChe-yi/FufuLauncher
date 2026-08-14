/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text;

namespace FufuLauncher.Services;

public partial class LuaPluginInstaller
{
    #region Config Management

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

    #endregion
}
