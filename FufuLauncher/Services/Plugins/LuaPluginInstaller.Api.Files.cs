/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using MoonSharp.Interpreter;

namespace FufuLauncher.Services;

public partial class LuaPluginInstaller
{
    #region File Operations API

    private void RegisterFileOperationHandlers(Table table, CancellationToken cancellationToken)
    {
        table["move_file"] = (Action<string, string>)((source, dest) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeSource = SanitizePath(source, "move_file source");
            var safeDest = SanitizePath(dest, "move_file destination");

            if (!File.Exists(safeSource))
            {
                LogMessage($"Move failed: source file not found: {safeSource}");
                return;
            }

            string finalDest;
            if (Directory.Exists(safeDest))
            {
                finalDest = Path.Combine(safeDest, Path.GetFileName(safeSource));
                finalDest = SanitizePath(finalDest, "move_file final destination");
            }
            else
            {
                finalDest = safeDest;
                var parentDir = Path.GetDirectoryName(finalDest);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    Directory.CreateDirectory(parentDir);
            }

            LogMessage($"Moving file: {safeSource} -> {finalDest}");

            if (File.Exists(finalDest))
                File.Delete(finalDest);

            File.Move(safeSource, finalDest);
            LogMessage("File move complete");
        });

        table["move_files"] = (Action<DynValue, string>)((sources, destDir) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceList = TableToStringList(sources, "move_files");
            var safeDestDir = SanitizePath(destDir, "move_files destination");

            if (!Directory.Exists(safeDestDir))
                Directory.CreateDirectory(safeDestDir);

            LogMessage($"Batch moving {sourceList.Count} files to: {safeDestDir}");

            foreach (var src in sourceList)
            {
                var safeSrc = SanitizePath(src, "move_files source");

                if (!File.Exists(safeSrc))
                {
                    LogMessage($"  skipping missing file: {safeSrc}");
                    continue;
                }

                var destPath = Path.Combine(safeDestDir, Path.GetFileName(safeSrc));
                destPath = SanitizePath(destPath, "move_files dest");

                if (File.Exists(destPath))
                    File.Delete(destPath);

                File.Move(safeSrc, destPath);
                LogMessage($"  moved: {Path.GetFileName(safeSrc)}");
            }

            LogMessage("Batch move complete");
        });

        table["copy_file"] = (Action<string, string>)((source, dest) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeSource = SanitizePath(source, "copy_file source");
            var safeDest = SanitizePath(dest, "copy_file destination");

            if (!File.Exists(safeSource))
            {
                LogMessage($"Copy failed: source file not found: {safeSource}");
                return;
            }

            string finalDest;
            if (Directory.Exists(safeDest))
            {
                finalDest = Path.Combine(safeDest, Path.GetFileName(safeSource));
                finalDest = SanitizePath(finalDest, "copy_file final destination");
            }
            else
            {
                finalDest = safeDest;
                var parentDir = Path.GetDirectoryName(finalDest);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    Directory.CreateDirectory(parentDir);
            }

            LogMessage($"Copying file: {safeSource} -> {finalDest}");
            File.Copy(safeSource, finalDest, true);
            LogMessage("File copy complete");
        });

        table["copy_files"] = (Action<DynValue, string>)((sources, destDir) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceList = TableToStringList(sources, "copy_files");
            var safeDestDir = SanitizePath(destDir, "copy_files destination");

            if (!Directory.Exists(safeDestDir))
                Directory.CreateDirectory(safeDestDir);

            LogMessage($"Batch copying {sourceList.Count} files to: {safeDestDir}");

            foreach (var src in sourceList)
            {
                var safeSrc = SanitizePath(src, "copy_files source");

                if (!File.Exists(safeSrc))
                {
                    LogMessage($"  skipping missing file: {safeSrc}");
                    continue;
                }

                var destPath = Path.Combine(safeDestDir, Path.GetFileName(safeSrc));
                destPath = SanitizePath(destPath, "copy_files dest");

                File.Copy(safeSrc, destPath, true);
                LogMessage($"  copied: {Path.GetFileName(safeSrc)}");
            }

            LogMessage("Batch copy complete");
        });

        table["rename"] = (Action<string, string>)((oldPath, newName) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeOldPath = SanitizePath(oldPath, "rename source");
            var safeNewName = SanitizeName(newName, "rename");

            var parentDir = Path.GetDirectoryName(safeOldPath);
            if (string.IsNullOrEmpty(parentDir))
            {
                throw new InvalidOperationException("Cannot rename root plugins directory.");
            }

            var newPath = Path.Combine(parentDir, safeNewName);
            newPath = SanitizePath(newPath, "rename destination");

            bool isDirectory = Directory.Exists(safeOldPath);
            bool isFile = File.Exists(safeOldPath);

            if (!isDirectory && !isFile)
            {
                LogMessage($"Rename failed: path not found: {safeOldPath}");
                return;
            }

            if (isDirectory)
            {
                LogMessage($"Renaming directory: {safeOldPath} -> {newPath}");
                if (Directory.Exists(newPath))
                {
                    LogMessage($"  target directory already exists, merging or overwriting...");
                }

                Directory.Move(safeOldPath, newPath);
                LogMessage("Directory rename complete");
            }
            else
            {
                LogMessage($"Renaming file: {safeOldPath} -> {newPath}");
                if (File.Exists(newPath))
                    File.Delete(newPath);
                File.Move(safeOldPath, newPath);
                LogMessage("File rename complete");
            }
        });
    }

    private static List<string> TableToStringList(DynValue tableValue, string operation)
    {
        var result = new List<string>();

        if (tableValue.Type != DataType.Table)
        {
            throw new InvalidOperationException(
                $"'{operation}' expects a table (array) of strings.");
        }

        var table = tableValue.Table;

        var firstIndex = table.Get(0).IsNil() ? 1 : 0;
        for (int i = firstIndex;; i++)
        {
            var entry = table.Get(i);
            if (entry.IsNil())
                break;

            if (entry.Type != DataType.String)
            {
                throw new InvalidOperationException(
                    $"'{operation}' expects all table entries to be strings.");
            }

            result.Add(entry.String);
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException(
                $"'{operation}' expects a non-empty table.");
        }

        return result;
    }

    #endregion
}