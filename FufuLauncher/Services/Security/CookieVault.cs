using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FufuLauncher.Services.Security;

internal enum CookieVaultError
{
    None,
    KeyMissing,
    KeyUnreadable,
    KeyInvalid,
    KeyDecryptFailed,
    KeyMismatch,
    FileFormat,
    FileDecryptFailed
}

internal static class CookieVault
{
    internal const string FormatName = "FufuLauncher.CookieVault";
    private const int FormatVersion = 1;
    private const string AlgorithmName = "AES-256-GCM";
    private const string KeyProtectionName = "DPAPI-CurrentUser";
    private const int KeySize = 32;
    private const int KeyIdSize = 8;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const byte KeyFileVersion = 1;
    private const int KeyHeaderSize = 4 + 1 + KeyIdSize;
    private static readonly byte[] KeyMagic = Encoding.UTF8.GetBytes("FLVK");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FufuLauncher.CookieVault.DPAPI.v1");
    private static readonly object KeySync = new();

    public static bool IsEncryptedFile(byte[] data)
    {
        try
        {
            using var document = JsonDocument.Parse(data);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                   && root.TryGetProperty("format", out var format)
                   && format.ValueKind == JsonValueKind.String
                   && string.Equals(format.GetString(), FormatName, StringComparison.Ordinal)
                   && root.TryGetProperty("nonce", out var nonce)
                   && nonce.ValueKind == JsonValueKind.String
                   && root.TryGetProperty("tag", out var tag)
                   && tag.ValueKind == JsonValueKind.String
                   && root.TryGetProperty("ciphertext", out var ciphertext)
                   && ciphertext.ValueKind == JsonValueKind.String;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static byte[] Encrypt(string dataDir, string fileName, byte[] plaintext)
    {
        var key = GetOrCreateKey(dataDir);
        byte[]? associatedData = null;
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagSize];
            associatedData = BuildAssociatedData(fileName, key.KeyId, FormatVersion);

            using (var aes = new AesGcm(key.KeyBytes, TagSize))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
            }

            var envelope = new CookieFileEnvelope
            {
                Format = FormatName,
                Version = FormatVersion,
                Algorithm = AlgorithmName,
                KeyProtection = KeyProtectionName,
                KeyId = Convert.ToBase64String(key.KeyId),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag),
                Ciphertext = Convert.ToBase64String(ciphertext)
            };

            var result = JsonSerializer.SerializeToUtf8Bytes(envelope);
            CryptographicOperations.ZeroMemory(ciphertext);
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key.KeyBytes);
            if (associatedData != null)
                CryptographicOperations.ZeroMemory(associatedData);
        }
    }

    public static byte[] Decrypt(string dataDir, string fileName, byte[] fileBytes)
    {
        CookieFileEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<CookieFileEnvelope>(fileBytes)
                       ?? throw new CookieVaultException(CookieVaultError.FileFormat, "Cookie 文件加密格式无效");
        }
        catch (JsonException ex)
        {
            throw new CookieVaultException(CookieVaultError.FileFormat, "Cookie 文件加密格式无效", ex);
        }

        if (!string.Equals(envelope.Format, FormatName, StringComparison.Ordinal)
            || envelope.Version != FormatVersion
            || !string.Equals(envelope.Algorithm, AlgorithmName, StringComparison.Ordinal)
            || !string.Equals(envelope.KeyProtection, KeyProtectionName, StringComparison.Ordinal))
            throw new CookieVaultException(CookieVaultError.FileFormat, "Cookie 文件加密格式不受支持");

        if (string.IsNullOrEmpty(envelope.KeyId)
            || string.IsNullOrEmpty(envelope.Nonce)
            || string.IsNullOrEmpty(envelope.Tag)
            || string.IsNullOrEmpty(envelope.Ciphertext))
            throw new CookieVaultException(CookieVaultError.FileFormat, "Cookie 文件加密参数缺失");

        byte[] keyId;
        byte[] nonce;
        byte[] tag;
        byte[] ciphertext;
        try
        {
            keyId = Convert.FromBase64String(envelope.KeyId);
            nonce = Convert.FromBase64String(envelope.Nonce);
            tag = Convert.FromBase64String(envelope.Tag);
            ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        }
        catch (FormatException ex)
        {
            throw new CookieVaultException(CookieVaultError.FileFormat, "Cookie 文件加密参数编码无效", ex);
        }

        if (keyId.Length != KeyIdSize || nonce.Length != NonceSize || tag.Length != TagSize)
            throw new CookieVaultException(CookieVaultError.FileFormat, "Cookie 文件加密参数长度无效");

        var key = LoadKey(dataDir);
        byte[]? associatedData = null;
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(key.KeyId, keyId))
                throw new CookieVaultException(CookieVaultError.KeyMismatch, "Cookie 文件加密密钥不匹配");

            associatedData = BuildAssociatedData(fileName, keyId, envelope.Version);
            var plaintext = new byte[ciphertext.Length];
            try
            {
                using var aes = new AesGcm(key.KeyBytes, TagSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
                return plaintext;
            }
            catch (CryptographicException ex)
            {
                CryptographicOperations.ZeroMemory(plaintext);
                throw new CookieVaultException(CookieVaultError.FileDecryptFailed, "Cookie 文件完整性校验失败", ex);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key.KeyBytes);
            if (associatedData != null)
                CryptographicOperations.ZeroMemory(associatedData);
        }
    }

    public static void DeleteKey(string dataDir)
    {
        string keyPath = GetKeyPath(dataDir);
        lock (KeySync)
        {
            try
            {
                if (!File.Exists(keyPath))
                    return;

                try
                {
                    long length = new FileInfo(keyPath).Length;
                    if (length > 0 && length <= int.MaxValue)
                    {
                        var scrub = RandomNumberGenerator.GetBytes((int)length);
                        try
                        {
                            using var stream = new FileStream(keyPath, FileMode.Open, FileAccess.Write, FileShare.None);
                            stream.Write(scrub, 0, scrub.Length);
                            stream.Flush(true);
                        }
                        finally
                        {
                            CryptographicOperations.ZeroMemory(scrub);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CookieVault] 覆盖 Cookie 加密密钥失败: {ex.Message}");
                }

                File.Delete(keyPath);

                string? directory = Path.GetDirectoryName(keyPath);
                if (!string.IsNullOrEmpty(directory)
                    && Directory.Exists(directory)
                    && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CookieVault] 删除 Cookie 加密密钥失败: {ex.Message}");
            }
        }
    }

    private static CookieKey GetOrCreateKey(string dataDir)
    {
        string keyPath = GetKeyPath(dataDir);
        lock (KeySync)
        {
            if (File.Exists(keyPath))
            {
                try
                {
                    return LoadKey(dataDir);
                }
                catch (CookieVaultException ex) when (ex.Error is CookieVaultError.KeyMissing or CookieVaultError.KeyInvalid or CookieVaultError.KeyDecryptFailed)
                {
                    QuarantineKeyFile(keyPath);
                }
            }

            return CreateKey(keyPath);
        }
    }

    private static CookieKey CreateKey(string keyPath)
    {
        string? directory = Path.GetDirectoryName(keyPath);
        if (string.IsNullOrEmpty(directory))
            throw new CookieVaultException(CookieVaultError.KeyInvalid, "Cookie 加密密钥路径无效");

        byte[]? keyBytes = null;
        byte[]? protectedKey = null;
        byte[]? payload = null;
        try
        {
            Directory.CreateDirectory(directory);
            keyBytes = RandomNumberGenerator.GetBytes(KeySize);
            var keyId = RandomNumberGenerator.GetBytes(KeyIdSize);
            protectedKey = ProtectedData.Protect(keyBytes, Entropy, DataProtectionScope.CurrentUser);
            payload = new byte[KeyHeaderSize + protectedKey.Length];
            KeyMagic.CopyTo(payload, 0);
            payload[KeyMagic.Length] = KeyFileVersion;
            keyId.CopyTo(payload, KeyMagic.Length + 1);
            protectedKey.CopyTo(payload, KeyHeaderSize);

            WriteKeyFile(keyPath, payload);

            var key = new CookieKey(keyBytes, keyId);
            keyBytes = null;
            return key;
        }
        catch (IOException) when (File.Exists(keyPath))
        {
            return LoadKeyFile(keyPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            throw new CookieVaultException(CookieVaultError.KeyInvalid, "Cookie 加密密钥无法创建", ex);
        }
        finally
        {
            if (keyBytes != null)
                CryptographicOperations.ZeroMemory(keyBytes);
            if (protectedKey != null)
                CryptographicOperations.ZeroMemory(protectedKey);
            if (payload != null)
                CryptographicOperations.ZeroMemory(payload);
        }
    }

    private static CookieKey LoadKey(string dataDir) => LoadKeyFile(GetKeyPath(dataDir));

    private static CookieKey LoadKeyFile(string keyPath)
    {
        byte[] fileBytes;
        try
        {
            fileBytes = File.ReadAllBytes(keyPath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new CookieVaultException(CookieVaultError.KeyMissing, "Cookie 加密密钥不存在", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CookieVaultException(CookieVaultError.KeyUnreadable, "Cookie 加密密钥无法读取", ex);
        }

        try
        {
            if (fileBytes.Length <= KeyHeaderSize
                || !fileBytes.AsSpan(0, KeyMagic.Length).SequenceEqual(KeyMagic)
                || fileBytes[KeyMagic.Length] != KeyFileVersion)
                throw new CookieVaultException(CookieVaultError.KeyInvalid, "Cookie 加密密钥文件格式无效");

            int keyIdOffset = KeyMagic.Length + 1;
            var keyId = fileBytes.AsSpan(keyIdOffset, KeyIdSize).ToArray();
            var protectedKey = fileBytes.AsSpan(KeyHeaderSize).ToArray();

            byte[] keyBytes;
            try
            {
                keyBytes = ProtectedData.Unprotect(protectedKey, Entropy, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException ex)
            {
                throw new CookieVaultException(CookieVaultError.KeyDecryptFailed,
                    "Cookie 加密密钥无法由 Windows DPAPI 解密", ex);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedKey);
            }

            if (keyBytes.Length != KeySize)
            {
                CryptographicOperations.ZeroMemory(keyBytes);
                throw new CookieVaultException(CookieVaultError.KeyInvalid, "Cookie 加密密钥长度无效");
            }

            return new CookieKey(keyBytes, keyId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(fileBytes);
        }
    }

    private static void QuarantineKeyFile(string keyPath)
    {
        try
        {
            if (!File.Exists(keyPath))
                return;

            string quarantinePath = keyPath + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            File.Move(keyPath, quarantinePath);
            Debug.WriteLine($"[CookieVault] 已隔离无法解密的 Cookie 加密密钥: {Path.GetFileName(quarantinePath)}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CookieVault] 隔离无法解密的 Cookie 加密密钥失败: {ex.Message}");
            try
            {
                if (File.Exists(keyPath))
                    File.Delete(keyPath);
            }
            catch
            {
            }
        }
    }

    private static void WriteKeyFile(string keyPath, byte[] payload)
    {
        string tempPath = keyPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(payload, 0, payload.Length);
                stream.Flush(true);
            }

            File.Move(tempPath, keyPath);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
            }
        }
    }

    private static byte[] BuildAssociatedData(string fileName, byte[] keyId, int version)
    {
        string value = string.Join(",",
            FormatName,
            version.ToString(),
            AlgorithmName,
            KeyProtectionName,
            Convert.ToBase64String(keyId),
            fileName);
        return Encoding.UTF8.GetBytes(value);
    }

    private static string GetKeyPath(string dataDir)
    {
        if (string.IsNullOrWhiteSpace(dataDir))
            throw new CookieVaultException(CookieVaultError.KeyInvalid, "Cookie 加密密钥目录无效");
        return Path.Combine(dataDir, "secrets", "cookie.key");
    }

    private readonly record struct CookieKey(byte[] KeyBytes, byte[] KeyId);
}

internal sealed class CookieFileEnvelope
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = string.Empty;

    [JsonPropertyName("keyProtection")]
    public string KeyProtection { get; set; } = string.Empty;

    [JsonPropertyName("keyId")]
    public string KeyId { get; set; } = string.Empty;

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("ciphertext")]
    public string Ciphertext { get; set; } = string.Empty;
}

internal sealed class CookieVaultException : Exception
{
    public CookieVaultException(CookieVaultError error, string message)
        : base(message)
    {
        Error = error;
    }

    public CookieVaultException(CookieVaultError error, string message, Exception innerException)
        : base(message, innerException)
    {
        Error = error;
    }

    public CookieVaultError Error { get; }
}
