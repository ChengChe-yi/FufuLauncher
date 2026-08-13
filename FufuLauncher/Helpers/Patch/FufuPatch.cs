/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Helpers.Patch;

public static class FufuPatch
{
    internal const int CacheSize = 4096;
    internal const int MaxPluginTypeLength = 259;

    private const string VersionMagic = "HDIFF13";
    private const int SignTagBit = 1;
    
    internal static Action<string>? Diagnostics { get; set; }

    internal static void Diag(string message)
    {
        Diagnostics?.Invoke(message);
    }
    
    public static long GetDataSize(Stream diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        if (!diff.CanSeek)
        {
            throw new ArgumentException("Patch_DiffNotSeekable".GetLocalized(), nameof(diff));
        }

        PatchInput input = new(diff);
        try
        {
            return ReadHead(input).NewDataSize;
        }
        catch (PatchFormatException)
        {
            throw new InvalidOperationException("Patch_ReadHeaderFailed".GetLocalized());
        }
    }
    
    public static bool Merge(Stream source, Stream diff, Stream target)
    {
        return Apply(source, diff, target, null);
    }
    
    public static bool MergeZstd(Stream source, Stream diff, Stream target)
    {
        return Apply(source, diff, target, ZstdPatchDecompressor.Instance);
    }
    
    public static Stream CreateSubStream(Stream stream, long offset, long length)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Patch_DiffNotSeekable".GetLocalized(), nameof(stream));
        }

        return new PatchSubStream(new PatchInput(stream), offset, offset + length);
    }

    private static bool Apply(Stream source, Stream diff, Stream target, IPatchDecompressor? decompressor)
    {
        if (!source.CanSeek || !diff.CanSeek || !target.CanWrite)
        {
            Diag("Apply rejected: stream capability check failed");
            return false;
        }

        string step = "head";
        PatchSectionReader? covers = null;
        PatchSectionReader? rleCtrl = null;
        PatchSectionReader? rleCode = null;
        PatchSectionReader? newDataDiff = null;
        try
        {
            PatchInput input = new(diff);
            DiffHead head = ReadHead(input);
            Diag($"head ok: new={head.NewDataSize} old={head.OldDataSize} covers={head.CoverCount} type='{head.CompressType}' compressedCount={head.CompressedCount} headEnd={head.HeadEndPos}");

            if (head.OldDataSize != source.Length)
            {
                Diag($"Apply rejected: oldDataSize {head.OldDataSize} != source length {source.Length}");
                return false;
            }

            if (head.CompressedCount > 0)
            {
                if (decompressor is null || !decompressor.CanOpen(head.CompressType))
                {
                    Diag($"Apply rejected: compressed sections need plugin for '{head.CompressType}'");
                    return false;
                }
            }

            step = "sections";
            long diffPos = head.HeadEndPos;
            covers = CreateSection(input, ref diffPos, head.CoverBufSize, head.CompressedCoverBufSize, decompressor);
            rleCtrl = CreateSection(input, ref diffPos, head.RleCtrlBufSize, head.CompressedRleCtrlBufSize, decompressor);
            rleCode = CreateSection(input, ref diffPos, head.RleCodeBufSize, head.CompressedRleCodeBufSize, decompressor);
            newDataDiff = CreateSection(input, ref diffPos, head.NewDataDiffSize, head.CompressedNewDataDiffSize, decompressor);
            Diag($"sections ok: diffPos={diffPos} total={input.Length}");

            if (diffPos != input.Length)
            {
                Diag($"Apply rejected: sections end at {diffPos}, diff length {input.Length}");
                return false;
            }

            step = "apply";
            var coverReader = new CoverReader(covers, head.CoverCount);
            var rle = new RleDecoder(rleCtrl, rleCode);
            var output = new PatchOutput(target, head.NewDataSize);

            ApplyCovers(source, coverReader, newDataDiff, rle, output, head.NewDataSize);

            bool ok = output.Written == head.NewDataSize
                      && rle.IsFinished
                      && coverReader.IsFinished
                      && newDataDiff.IsFinished;
            Diag($"apply done: written={output.Written}/{head.NewDataSize} rleFinished={rle.IsFinished} coversFinished={coverReader.IsFinished} newDataFinished={newDataDiff.IsFinished} ok={ok}");
            return ok;
        }
        catch (PatchFormatException ex)
        {
            Diag($"PatchFormatException at step '{step}': {ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            Diag($"IOException at step '{step}': {ex.Message}");
            return false;
        }
        catch (NotSupportedException ex)
        {
            Diag($"NotSupportedException at step '{step}': {ex.Message}");
            return false;
        }
        catch (ObjectDisposedException)
        {
            Diag($"ObjectDisposedException at step '{step}'");
            return false;
        }
        finally
        {
            covers?.Dispose();
            rleCtrl?.Dispose();
            rleCode?.Dispose();
            newDataDiff?.Dispose();
        }
    }
    
    private static DiffHead ReadHead(PatchInput input)
    {
        PatchSectionReader reader = PatchSectionReader.CreateRaw(input, 0, input.Length);

        string magic = reader.ReadTypeEnd('&');
        if (!string.Equals(magic, VersionMagic, StringComparison.Ordinal))
        {
            throw new PatchFormatException($"unexpected magic '{magic}'");
        }

        string compressType = reader.ReadTypeEnd('\0');
        long newDataSize = reader.ReadVarInt();
        long oldDataSize = reader.ReadVarInt();
        long coverCount = reader.ReadVarInt();
        long coverBufSize = reader.ReadVarInt();
        long compressedCoverBufSize = reader.ReadVarInt();
        long rleCtrlBufSize = reader.ReadVarInt();
        long compressedRleCtrlBufSize = reader.ReadVarInt();
        long rleCodeBufSize = reader.ReadVarInt();
        long compressedRleCodeBufSize = reader.ReadVarInt();
        long newDataDiffSize = reader.ReadVarInt();
        long compressedNewDataDiffSize = reader.ReadVarInt();

        long headEndPos = reader.Position;
        long coverEndPos = headEndPos + (compressedCoverBufSize > 0 ? compressedCoverBufSize : coverBufSize);

        int compressedCount =
            (compressedCoverBufSize > 0 ? 1 : 0) +
            (compressedRleCtrlBufSize > 0 ? 1 : 0) +
            (compressedRleCodeBufSize > 0 ? 1 : 0) +
            (compressedNewDataDiffSize > 0 ? 1 : 0);

        return new DiffHead(newDataSize, oldDataSize, coverCount, coverBufSize, compressedCoverBufSize,
            rleCtrlBufSize, compressedRleCtrlBufSize, rleCodeBufSize, compressedRleCodeBufSize,
            newDataDiffSize, compressedNewDataDiffSize, headEndPos, coverEndPos, compressedCount, compressType);
    }
    
    private static PatchSectionReader CreateSection(PatchInput input, ref long diffPos, long rawSize, long compressedSize, IPatchDecompressor? decompressor)
    {
        long sectionPos = diffPos;
        long sectionLength = compressedSize > 0 ? compressedSize : rawSize;

        if (sectionPos < 0 || sectionLength < 0 || sectionPos + sectionLength < sectionPos || sectionPos + sectionLength > input.Length)
        {
            throw new PatchFormatException("section out of range");
        }

        diffPos = sectionPos + sectionLength;

        if (compressedSize > 0)
        {
            Stream decompressed = decompressor!.Open(rawSize, input, sectionPos, sectionPos + sectionLength);
            return PatchSectionReader.CreateCompressed(decompressed, rawSize);
        }

        return PatchSectionReader.CreateRaw(input, sectionPos, sectionPos + rawSize);
    }
    
    private static void ApplyCovers(Stream oldData, CoverReader covers, PatchSectionReader newDataDiff, RleDecoder rle, PatchOutput output, long newDataSize)
    {
        long oldDataSize = oldData.Length;
        long newPosBack = 0;
        byte[] buffer = new byte[CacheSize];
        int coverIndex = 0;

        while (covers.ReadCover(out Cover cover))
        {
            coverIndex++;
            Diag($"cover {coverIndex}: old={cover.OldPos} new={cover.NewPos} len={cover.Length}");

            if (cover.NewPos < newPosBack || cover.Length > newDataSize - cover.NewPos
                || cover.OldPos > oldDataSize || cover.Length > oldDataSize - cover.OldPos)
            {
                throw new PatchFormatException("invalid cover bounds");
            }

            if (newPosBack < cover.NewPos)
            {
                long copyLength = cover.NewPos - newPosBack;
                newDataDiff.CopyTo(output, copyLength);
                rle.Skip(copyLength);
            }

            long oldPos = cover.OldPos;
            long addLength = cover.Length;
            while (addLength > 0)
            {
                int step = (int)Math.Min(addLength, buffer.Length);
                ReadExactAt(oldData, oldPos, buffer, step);
                rle.Decode(buffer, step);
                output.Write(buffer, 0, step);
                oldPos += step;
                addLength -= step;
            }

            newPosBack = cover.NewPos + cover.Length;
        }

        if (newPosBack < newDataSize)
        {
            long copyLength = newDataSize - newPosBack;
            newDataDiff.CopyTo(output, copyLength);
            rle.Skip(copyLength);
        }

        output.Flush();
        rle.EnsureFinished();
        covers.EnsureFinished();
        newDataDiff.EnsureFinished();
    }

    private static void ReadExactAt(Stream stream, long position, byte[] buffer, int count)
    {
        stream.Position = position;
        int total = 0;
        while (total < count)
        {
            int read = stream.Read(buffer, total, count - total);
            if (read <= 0)
            {
                throw new PatchFormatException("old file read failed");
            }

            total += read;
        }
    }
    
    private readonly record struct DiffHead(
        long NewDataSize,
        long OldDataSize,
        long CoverCount,
        long CoverBufSize,
        long CompressedCoverBufSize,
        long RleCtrlBufSize,
        long CompressedRleCtrlBufSize,
        long RleCodeBufSize,
        long CompressedRleCodeBufSize,
        long NewDataDiffSize,
        long CompressedNewDataDiffSize,
        long HeadEndPos,
        long CoverEndPos,
        int CompressedCount,
        string CompressType);

    private readonly record struct Cover(long OldPos, long NewPos, long Length);
    
    private sealed class CoverReader
    {
        private readonly PatchSectionReader _stream;
        private long _coverCount;
        private long _oldPosBack;
        private long _newPosBack;

        public CoverReader(PatchSectionReader stream, long coverCount)
        {
            _stream = stream;
            _coverCount = coverCount;
        }

        public bool IsFinished => _stream.IsFinished;

        public void EnsureFinished()
        {
            if (!IsFinished)
            {
                throw new PatchFormatException("covers not fully consumed");
            }
        }

        public bool ReadCover(out Cover cover)
        {
            if (_coverCount <= 0)
            {
                cover = default;
                return false;
            }

            _coverCount--;

            byte first = _stream.PeekByte();
            bool negative = (first & 0x80) != 0;
            long incOldPos = _stream.ReadVarInt(SignTagBit);
            long oldPos = negative ? _oldPosBack - incOldPos : _oldPosBack + incOldPos;
            long copyLength = _stream.ReadVarInt();
            long coverLength = _stream.ReadVarInt();

            _newPosBack += copyLength;
            _oldPosBack = oldPos + coverLength;
            cover = new Cover(oldPos, _newPosBack, coverLength);
            _newPosBack += coverLength;
            return true;
        }
    }
}


internal sealed class PatchFormatException : Exception
{
    public PatchFormatException()
    {
    }

    public PatchFormatException(string message)
        : base(message)
    {
    }
}
