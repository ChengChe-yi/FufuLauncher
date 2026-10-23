/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text;
using MoonSharp.Interpreter;

namespace FufuLauncher.Services;

public partial class LuaPluginInstaller
{
    #region Utility API

    private void RegisterUtilityHandlers(Table table, CancellationToken cancellationToken)
    {
        table["log"] = (Action<string>)(msg =>
        {
            LogMessage(msg);
        });

        table["set_progress"] = (Action<int, string>)((percent, status) =>
        {
            ReportProgress(Math.Clamp(percent, 0, 100), status);
        });

        table["write_config"] = (Action<string, DynValue>)((dir, value) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeDir = SanitizePath(dir, "write_config");
            LogMessage($"Write config: {safeDir}");

            var configPath = Path.Combine(safeDir, "config.ini");

            if (!Directory.Exists(safeDir))
                Directory.CreateDirectory(safeDir);

            var iniLines = new StringBuilder();
            if (value.Type == DataType.Table)
            {
                foreach (var sectionPair in value.Table.Pairs)
                {
                    var sectionName = sectionPair.Key.String;
                    var sectionTable = sectionPair.Value.Table;

                    iniLines.AppendLine($"[{sectionName}]");
                    foreach (var kvp in sectionTable.Pairs)
                    {
                        var key = kvp.Key.String;
                        var val = kvp.Value.String;
                        iniLines.AppendLine($"{key} = {val}");
                    }

                    iniLines.AppendLine();
                }
            }

            File.WriteAllText(configPath, iniLines.ToString(), Encoding.UTF8);
            LogMessage("Config write complete");
        });

        table["verify_file_hash"] = (Func<string, string, bool>)((path, expectedHash) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "verify_file_hash");
            LogMessage($"Verify file hash: {safePath}");

            try
            {
                PluginVerifier.VerifyFileHash(safePath, expectedHash, Path.GetFileName(safePath));
                LogMessage("File hash verified");
                return true;
            }
            catch (HashMismatchException ex)
            {
                LogMessage($"File hash verification failed: {ex.Message}");
                return false;
            }
        });
    }

    #endregion
}