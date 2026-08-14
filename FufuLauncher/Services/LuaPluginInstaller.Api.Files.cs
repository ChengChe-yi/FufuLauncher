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
                LogMessage($"移动失败: 源文件不存在: {safeSource}");
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

            LogMessage($"移动文件: {safeSource} -> {finalDest}");

            if (File.Exists(finalDest))
                File.Delete(finalDest);

            File.Move(safeSource, finalDest);
            LogMessage("文件移动完成");
        });

        table["move_files"] = (Action<DynValue, string>)((sources, destDir) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceList = TableToStringList(sources, "move_files");
            var safeDestDir = SanitizePath(destDir, "move_files destination");

            if (!Directory.Exists(safeDestDir))
                Directory.CreateDirectory(safeDestDir);

            LogMessage($"批量移动 {sourceList.Count} 个文件到: {safeDestDir}");

            foreach (var src in sourceList)
            {
                var safeSrc = SanitizePath(src, "move_files source");

                if (!File.Exists(safeSrc))
                {
                    LogMessage($"  跳过不存在的文件: {safeSrc}");
                    continue;
                }

                var destPath = Path.Combine(safeDestDir, Path.GetFileName(safeSrc));
                destPath = SanitizePath(destPath, "move_files dest");

                if (File.Exists(destPath))
                    File.Delete(destPath);

                File.Move(safeSrc, destPath);
                LogMessage($"  移动: {Path.GetFileName(safeSrc)}");
            }

            LogMessage("批量移动完成");
        });

        table["copy_file"] = (Action<string, string>)((source, dest) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeSource = SanitizePath(source, "copy_file source");
            var safeDest = SanitizePath(dest, "copy_file destination");

            if (!File.Exists(safeSource))
            {
                LogMessage($"复制失败: 源文件不存在: {safeSource}");
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

            LogMessage($"复制文件: {safeSource} -> {finalDest}");
            File.Copy(safeSource, finalDest, true);
            LogMessage("文件复制完成");
        });

        table["copy_files"] = (Action<DynValue, string>)((sources, destDir) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceList = TableToStringList(sources, "copy_files");
            var safeDestDir = SanitizePath(destDir, "copy_files destination");

            if (!Directory.Exists(safeDestDir))
                Directory.CreateDirectory(safeDestDir);

            LogMessage($"批量复制 {sourceList.Count} 个文件到: {safeDestDir}");

            foreach (var src in sourceList)
            {
                var safeSrc = SanitizePath(src, "copy_files source");

                if (!File.Exists(safeSrc))
                {
                    LogMessage($"  跳过不存在的文件: {safeSrc}");
                    continue;
                }

                var destPath = Path.Combine(safeDestDir, Path.GetFileName(safeSrc));
                destPath = SanitizePath(destPath, "copy_files dest");

                File.Copy(safeSrc, destPath, true);
                LogMessage($"  复制: {Path.GetFileName(safeSrc)}");
            }

            LogMessage("批量复制完成");
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
                LogMessage($"重命名失败: 路径不存在: {safeOldPath}");
                return;
            }

            if (isDirectory)
            {
                LogMessage($"重命名目录: {safeOldPath} -> {newPath}");
                if (Directory.Exists(newPath))
                {
                    LogMessage($"  目标目录已存在，尝试合并或覆盖...");
                }
                Directory.Move(safeOldPath, newPath);
                LogMessage("目录重命名完成");
            }
            else
            {
                LogMessage($"重命名文件: {safeOldPath} -> {newPath}");
                if (File.Exists(newPath))
                    File.Delete(newPath);
                File.Move(safeOldPath, newPath);
                LogMessage("文件重命名完成");
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
        for (int i = 1; ; i++)
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
