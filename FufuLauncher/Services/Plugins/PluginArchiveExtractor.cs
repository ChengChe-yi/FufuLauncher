/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace FufuLauncher.Services;

/// <summary>Extracts plugin packages by signature, retaining the caller's file name and directory layout.</summary>
public sealed class PluginArchiveExtractor
{
    private string? _password;
    private readonly Func<string, bool, CancellationToken, Task<string?>>? _requestPassword;

    public PluginArchiveExtractor(string? password = null,
        Func<string, bool, CancellationToken, Task<string?>>? requestPassword = null)
    {
        _password = password;
        _requestPassword = requestPassword;
    }

    public async Task<int> ExtractAsync(string archivePath, string destination,
        Func<string, bool>? include = null, CancellationToken cancellationToken = default)
    {
        var destinationRoot = Path.GetFullPath(destination);
        // Probe without a password first: some encrypted RAR headers report a format error
        // with a wrong password before IsEncrypted can be read.
        var encrypted = await Task.Run(() =>
        {
            try
            {
                using var stream = File.OpenRead(archivePath);
                using var archive = ArchiveFactory.OpenArchive(stream);
                return archive.IsEncrypted || archive.Entries.Any(entry => entry.IsEncrypted);
            }
            catch (Exception ex) when (IsPasswordFailure(ex, false))
            {
                return true;
            }
        }, cancellationToken).ConfigureAwait(false);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var staging = Directory.CreateTempSubdirectory("FufuLauncher-Archive-").FullName;
            var retryPassword = false;
            try
            {
                var files = await Task.Run(() =>
                {
                    using var stream = File.OpenRead(archivePath);
                    using var archive = ArchiveFactory.OpenArchive(stream, new ReaderOptions { Password = _password });
                    if (archive.Type is not (ArchiveType.Zip or ArchiveType.SevenZip or ArchiveType.Rar))
                        throw new NotSupportedException("Only ZIP, 7z and RAR plugin packages are supported.");

                    if (encrypted && _password == null)
                        throw new SharpCompress.Common.CryptographicException("Archive password required.");

                    var extracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    void ExtractEntry(IEntry entry, Func<Stream> openStream)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var relativePath = (entry.Key ?? "").Replace('\\', '/');
                        if (entry.IsDirectory && relativePath is "." or "./") return;
                        var stagedPath = GetEntryPath(staging, relativePath);
                        GetEntryPath(destinationRoot, relativePath);
                        if (!string.IsNullOrEmpty(entry.LinkTarget))
                            throw new SecurityViolationException("Archive links are not allowed in plugin packages.");
                        if (entry.IsDirectory)
                        {
                            if (include == null) Directory.CreateDirectory(stagedPath);
                            return;
                        }
                        if (include != null && !include(relativePath)) return;

                        Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                        using (var input = openStream())
                        using (var output = new FileStream(stagedPath, FileMode.Create, FileAccess.Write))
                            input.CopyToAsync(output, cancellationToken).GetAwaiter().GetResult();
                        if (entry.LastModifiedTime is { } modified) File.SetLastWriteTime(stagedPath, modified);
                        extracted.Add(relativePath);
                    }

                    if (archive.IsSolid || archive.Type == ArchiveType.SevenZip)
                    {
                        // Sequential reading avoids repeatedly decoding solid RAR/7z blocks.
                        using var reader = archive.ExtractAllEntries();
                        while (reader.MoveToNextEntry()) ExtractEntry(reader.Entry, reader.OpenEntryStream);
                    }
                    else
                    {
                        foreach (var entry in archive.Entries) ExtractEntry(entry, entry.OpenEntryStream);
                    }

                    return extracted.ToList();
                }, cancellationToken).ConfigureAwait(false);

                CommitFiles(staging, destinationRoot, files, cancellationToken);
                return files.Count;
            }
            catch (Exception ex) when (IsPasswordFailure(ex, encrypted))
            {
                if (_requestPassword == null)
                    throw new PluginArchivePasswordException("An archive password is required or the password is incorrect.");
                retryPassword = true;
            }
            finally
            {
                Directory.Delete(staging, true);
            }

            if (retryPassword)
            {
                var password = await _requestPassword!(Path.GetFileName(archivePath), _password != null,
                    cancellationToken).ConfigureAwait(false);
                if (password == null) throw new OperationCanceledException(cancellationToken);
                _password = password;
            }
        }
    }

    private static bool IsPasswordFailure(Exception exception, bool encrypted) =>
        exception is SharpCompress.Common.CryptographicException or System.Security.Cryptography.CryptographicException ||
        encrypted && (exception is SharpCompressException or InvalidDataException ||
            // SharpCompress's LZMA decoder uses an internal exception for incorrect decrypted data.
            exception.GetType().FullName == "SharpCompress.Compressors.LZMA.DataErrorException");

    private static string GetEntryPath(string root, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            throw new SecurityViolationException("Invalid archive entry path.");
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new SecurityViolationException("Archive entry escapes the extraction directory.");
        return path;
    }

    private static void RejectReparsePoints(string path)
    {
        for (var current = path; current != null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new SecurityViolationException("Extraction through a link or reparse point is not allowed.");
        }
    }

    private static void CommitFiles(string staging, string destination, List<string> files,
        CancellationToken cancellationToken)
    {
        var backup = Directory.CreateTempSubdirectory("FufuLauncher-Archive-Backup-").FullName;
        var changed = new List<(string Target, string? Backup)>();
        var createdDirectories = new List<string>();
        var keepBackup = false;
        void EnsureDirectory(string path)
        {
            RejectReparsePoints(path);
            var missing = new Stack<string>();
            for (var current = path; !Directory.Exists(current); current = Path.GetDirectoryName(current)!)
                missing.Push(current);
            while (missing.TryPop(out var directory))
            {
                Directory.CreateDirectory(directory);
                createdDirectories.Add(directory);
            }
        }
        try
        {
            foreach (var relativePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = GetEntryPath(destination, relativePath);
                RejectReparsePoints(target);
                EnsureDirectory(Path.GetDirectoryName(target)!);

                string? backupPath = null;
                if (File.Exists(target))
                {
                    backupPath = GetEntryPath(backup, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                    File.Copy(target, backupPath);
                }
                changed.Add((target, backupPath));
                File.Copy(GetEntryPath(staging, relativePath), target, true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            EnsureDirectory(destination);
            foreach (var directory in Directory.EnumerateDirectories(staging, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureDirectory(GetEntryPath(destination, Path.GetRelativePath(staging, directory)));
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception failure)
        {
            var errors = new List<Exception>();
            foreach (var (target, backupPath) in changed.AsEnumerable().Reverse())
            {
                try
                {
                    if (backupPath != null) File.Copy(backupPath, target, true);
                    else if (File.Exists(target)) File.Delete(target);
                }
                catch (Exception ex) { errors.Add(ex); }
            }
            foreach (var directory in createdDirectories.AsEnumerable().Reverse())
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
                }
                catch (Exception ex) { errors.Add(ex); }
            }
            if (errors.Count > 0)
            {
                keepBackup = true;
                errors.Insert(0, failure);
                throw new AggregateException($"Could not restore all plugin files. Backup retained at: {backup}", errors);
            }
            throw;
        }
        finally
        {
            if (!keepBackup) Directory.Delete(backup, true);
        }
    }
}

public sealed class PluginArchivePasswordException(string message) : Exception(message);
