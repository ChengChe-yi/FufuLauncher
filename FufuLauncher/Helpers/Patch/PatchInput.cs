/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text;

namespace FufuLauncher.Helpers.Patch;

internal sealed class PatchInput
{
    private readonly Stream _stream;

    public PatchInput(Stream stream)
    {
        _stream = stream;
        Length = stream.Length;
    }

    public long Length { get; }
    
    public int Read(long position, byte[] buffer, int offset, int count)
    {
        _stream.Position = position;
        int total = 0;
        while (total < count)
        {
            int read = _stream.Read(buffer, offset + total, count - total);
            if (read <= 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}

internal sealed class PatchSubStream : Stream
{
    private readonly PatchInput _input;
    private readonly long _begin;
    private readonly long _end;
    private long _position;

    public PatchSubStream(PatchInput input, long begin, long end)
    {
        _input = input;
        _begin = begin;
        _position = begin;
        _end = end;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _end - _begin;

    public override long Position
    {
        get => _position - _begin;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long remain = _end - _position;
        if (remain <= 0)
        {
            return 0;
        }

        int toRead = (int)Math.Min(remain, count);
        int read = _input.Read(_position, buffer, offset, toRead);
        if (read <= 0)
        {
            return 0;
        }

        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => _begin + offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _end + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        _position = Math.Clamp(target, _begin, _end);
        return _position - _begin;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}

internal sealed class PatchSectionReader : IDisposable
{
    private readonly PatchInput? _diff;
    private readonly Stream? _decompressSource;
    private long _streamPos;
    private readonly long _streamEnd;
    private readonly byte[] _cache = new byte[FufuPatch.CacheSize];
    private int _cacheBegin;
    private int _cacheEnd;
    private int _disposed;

    private PatchSectionReader(PatchInput? diff, Stream? decompressSource, long streamPos, long streamEnd)
    {
        _diff = diff;
        _decompressSource = decompressSource;
        _streamPos = streamPos;
        _streamEnd = streamEnd;
        _cacheBegin = _cache.Length;
        _cacheEnd = _cache.Length;
    }

    public static PatchSectionReader CreateRaw(PatchInput diff, long begin, long end)
    {
        return new PatchSectionReader(diff, null, begin, end);
    }

    public static PatchSectionReader CreateCompressed(Stream decompressSource, long uncompressedSize)
    {
        return new PatchSectionReader(null, decompressSource, 0, uncompressedSize);
    }

    public bool IsFinished => LeaveSize == 0;
    
    public long Position => _streamPos - (_cacheEnd - _cacheBegin);

    private long LeaveSize => (_streamEnd - _streamPos) + (_cacheEnd - _cacheBegin);

    public void EnsureFinished()
    {
        if (!IsFinished)
        {
            throw new PatchFormatException("section not fully consumed");
        }
    }
    
    private bool UpdateCache()
    {
        long streamSize = _streamEnd - _streamPos;
        int readSize = _cacheBegin;
        if (readSize > streamSize)
        {
            readSize = (int)streamSize;
        }

        if (readSize == 0)
        {
            return true;
        }

        if (_cacheEnd > _cacheBegin)
        {
            Buffer.BlockCopy(_cache, _cacheBegin, _cache, _cacheBegin - readSize, _cacheEnd - _cacheBegin);
        }

        if (!ReadSource(_streamPos, _cache, _cacheEnd - readSize, readSize))
        {
            return false;
        }

        _cacheBegin -= readSize;
        _streamPos += readSize;
        return true;
    }

    private bool ReadSource(long position, byte[] buffer, int offset, int count)
    {
        if (_diff is not null)
        {
            return _diff.Read(position, buffer, offset, count) == count;
        }

        int total = 0;
        while (total < count)
        {
            int read = _decompressSource!.Read(buffer, offset + total, count - total);
            if (read <= 0)
            {
                return false;
            }

            total += read;
        }

        return true;
    }
    
    private bool EnsureCached(int need)
    {
        if (_cacheEnd - _cacheBegin >= need)
        {
            return true;
        }

        if (!UpdateCache())
        {
            FufuPatch.Diag($"EnsureCached failed: need={need} cached={_cacheEnd - _cacheBegin} leave={LeaveSize}");
            return false;
        }

        return _cacheEnd - _cacheBegin >= need;
    }

    /// <summary>������һ�ֽڣ������ѣ���</summary>
    public byte PeekByte()
    {
        if (!EnsureCached(1))
        {
            throw new PatchFormatException("stream exhausted or read failed");
        }

        return _cache[_cacheBegin];
    }
    
    public byte ReadByteValue()
    {
        if (!EnsureCached(1))
        {
            throw new PatchFormatException("stream exhausted or read failed");
        }

        return _cache[_cacheBegin++];
    }
    
    public long ReadVarInt(int tagBit = 0)
    {
        int valueBits = 7 - tagBit;
        int valueMask = (1 << valueBits) - 1;
        int contMask = 1 << valueBits;

        byte code = ReadByteValue();
        long value = code & valueMask;
        if ((code & contMask) != 0)
        {
            do
            {
                if ((value >> 57) != 0)
                {
                    throw new PatchFormatException("varint overflow");
                }

                code = ReadByteValue();
                value = (value << 7) | (uint)(code & 0x7F);
            }
            while ((code & 0x80) != 0);
        }

        return value;
    }
    
    public string ReadTypeEnd(char endTag)
    {
        int readLen = FufuPatch.MaxPluginTypeLength + 1;
        long leave = LeaveSize;
        if (readLen > leave)
        {
            readLen = (int)leave;
        }

        if (!EnsureCached(readLen))
        {
            throw new PatchFormatException("type string truncated");
        }

        for (int i = 0; i < readLen; i++)
        {
            if (_cache[_cacheBegin + i] != endTag)
            {
                continue;
            }

            string result = Encoding.ASCII.GetString(_cache, _cacheBegin, i);
            _cacheBegin += i + 1;
            return result;
        }

        throw new PatchFormatException("type string terminator not found");
    }
    
    public void CopyTo(PatchOutput output, long count)
    {
        while (count > 0)
        {
            int step = (int)Math.Min(count, _cache.Length);
            if (!EnsureCached(step))
            {
                throw new PatchFormatException("stream exhausted or read failed");
            }

            output.Write(_cache, _cacheBegin, step);
            _cacheBegin += step;
            count -= step;
        }
    }
    
    public void CopyTo(byte[] dst, int offset, long count)
    {
        while (count > 0)
        {
            int step = (int)Math.Min(count, _cache.Length);
            if (!EnsureCached(step))
            {
                throw new PatchFormatException("stream exhausted or read failed");
            }

            Buffer.BlockCopy(_cache, _cacheBegin, dst, offset, step);
            offset += step;
            _cacheBegin += step;
            count -= step;
        }
    }
    
    public void Skip(long count)
    {
        while (count > 0)
        {
            int step = (int)Math.Min(count, _cache.Length);
            if (!EnsureCached(step))
            {
                throw new PatchFormatException("stream exhausted or read failed");
            }

            _cacheBegin += step;
            count -= step;
        }
    }
    
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        (_decompressSource as IDisposable)?.Dispose();
    }
}
