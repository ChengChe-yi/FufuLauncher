/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Security.Cryptography;
using MoonSharp.Interpreter;

namespace FufuLauncher.Services;

public partial class LuaPluginInstaller
{
    #region File Inspection API

    private void RegisterFileInfoHandlers(Script script, Table table, CancellationToken cancellationToken)
    {
        table["get_file_info"] = (Func<string, DynValue>)(path =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "get_file_info");
            LogMessage($"查询文件信息: {safePath}");

            var infoTable = new Table(script);

            bool exists = File.Exists(safePath) || Directory.Exists(safePath);
            infoTable["exists"] = DynValue.NewBoolean(exists);

            if (!exists)
            {
                infoTable["size"] = DynValue.NewNumber(0);
                infoTable["last_modified"] = DynValue.NewString("");
                infoTable["is_directory"] = DynValue.NewBoolean(false);
                infoTable["hash"] = DynValue.NewString("");
                return DynValue.NewTable(infoTable);
            }

            bool isDir = Directory.Exists(safePath);
            infoTable["is_directory"] = DynValue.NewBoolean(isDir);

            if (isDir)
            {
                var dirInfo = new DirectoryInfo(safePath);
                infoTable["size"] = DynValue.NewNumber(0);
                infoTable["last_modified"] = DynValue.NewString(dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                infoTable["hash"] = DynValue.NewString("");
            }
            else
            {
                var fileInfo = new FileInfo(safePath);
                infoTable["size"] = DynValue.NewNumber(fileInfo.Length);
                infoTable["last_modified"] = DynValue.NewString(fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));

                try
                {
                    var hash = PluginVerifier.ComputeFileSha256(safePath);
                    infoTable["hash"] = DynValue.NewString(hash);
                }
                catch
                {
                    infoTable["hash"] = DynValue.NewString("");
                }
            }

            return DynValue.NewTable(infoTable);
        });

        table["compare_files"] = (Func<string, string, DynValue>)((path1, path2) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath1 = SanitizePath(path1, "compare_files path1");
            var safePath2 = SanitizePath(path2, "compare_files path2");

            LogMessage($"比较文件: {safePath1} <-> {safePath2}");

            var result = new Table(script);

            var exists1 = File.Exists(safePath1);
            var exists2 = File.Exists(safePath2);

            if (!exists1 || !exists2)
            {
                result["same"] = DynValue.NewBoolean(false);
                result["same_size"] = DynValue.NewBoolean(false);
                result["same_hash"] = DynValue.NewBoolean(false);
                result["size1"] = DynValue.NewNumber(exists1 ? new FileInfo(safePath1).Length : -1);
                result["size2"] = DynValue.NewNumber(exists2 ? new FileInfo(safePath2).Length : -1);
                result["hash1"] = DynValue.NewString(exists1 ? PluginVerifier.ComputeFileSha256(safePath1) : "");
                result["hash2"] = DynValue.NewString(exists2 ? PluginVerifier.ComputeFileSha256(safePath2) : "");
                return DynValue.NewTable(result);
            }

            var fi1 = new FileInfo(safePath1);
            var fi2 = new FileInfo(safePath2);

            var sameSize = fi1.Length == fi2.Length;
            result["same_size"] = DynValue.NewBoolean(sameSize);
            result["size1"] = DynValue.NewNumber(fi1.Length);
            result["size2"] = DynValue.NewNumber(fi2.Length);

            string hash1, hash2;
            try
            {
                hash1 = PluginVerifier.ComputeFileSha256(safePath1);
                hash2 = PluginVerifier.ComputeFileSha256(safePath2);
            }
            catch
            {
                hash1 = "";
                hash2 = "";
            }

            result["hash1"] = DynValue.NewString(hash1);
            result["hash2"] = DynValue.NewString(hash2);

            var sameHash = string.Equals(hash1, hash2, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(hash1);
            result["same_hash"] = DynValue.NewBoolean(sameHash);
            result["same"] = DynValue.NewBoolean(sameSize && sameHash);

            LogMessage($"比较结果: same_size={sameSize}, same_hash={sameHash}");
            return DynValue.NewTable(result);
        });

        table["get_file_hash"] = (Func<DynValue, DynValue, string>)((pathArg, algoArg) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = pathArg.IsNil() ? "" : pathArg.String;
            var algorithm = algoArg.IsNil() ? "" : algoArg.String;

            var safePath = SanitizePath(path, "get_file_hash");
            var algo = string.IsNullOrWhiteSpace(algorithm) ? "sha256" : algorithm.ToLowerInvariant().Trim();

            LogMessage($"计算文件哈希 [{algo}]: {safePath}");

            if (!File.Exists(safePath))
            {
                LogMessage($"文件不存在: {safePath}");
                return "";
            }

            using var stream = File.OpenRead(safePath);
            byte[] hashBytes;

            switch (algo)
            {
                case "md5":
                    hashBytes = MD5.HashData(stream);
                    break;
                case "sha1":
                    hashBytes = SHA1.HashData(stream);
                    break;
                case "sha256":
                default:
                    hashBytes = SHA256.HashData(stream);
                    break;
            }

            var hash = PluginVerifier.BytesToHex(hashBytes);
            LogMessage($"哈希值: {hash}");
            return hash;
        });

        table["list_files"] = (Func<string, string, DynValue>)((dir, pattern) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeDir = SanitizePath(dir, "list_files");
            LogMessage($"列出文件: {safeDir}" + (string.IsNullOrEmpty(pattern) ? "" : $" (模式: {pattern})"));

            var fileList = new Table(script);

            if (!Directory.Exists(safeDir))
            {
                LogMessage($"目录不存在: {safeDir}");
                return DynValue.NewTable(fileList);
            }

            var files = Directory.GetFiles(safeDir, "*", SearchOption.TopDirectoryOnly);
            int index = 1;

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);

                if (!string.IsNullOrEmpty(pattern) && !WildcardMatch(fileName, pattern))
                    continue;

                fileList[index] = DynValue.NewString(file);
                index++;
            }

            LogMessage($"找到 {index - 1} 个文件");
            return DynValue.NewTable(fileList);
        });

        table["file_exists"] = (Func<string, bool>)(path =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "file_exists");
            var exists = File.Exists(safePath);
            LogMessage($"文件存在检查: {safePath} = {exists}");
            return exists;
        });

        table["dir_exists"] = (Func<string, bool>)(path =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safePath = SanitizePath(path, "dir_exists");
            var exists = Directory.Exists(safePath);
            LogMessage($"目录存在检查: {safePath} = {exists}");
            return exists;
        });
    }

    #endregion
}
