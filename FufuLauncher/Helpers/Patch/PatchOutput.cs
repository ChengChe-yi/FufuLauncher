/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Helpers.Patch;

internal sealed class PatchOutput
{
    private readonly Stream _target;
    private readonly byte[] _cache = new byte[FufuPatch.CacheSize];
    private int _cacheCur;
    private long _written;

    public PatchOutput(Stream target, long targetSize)
    {
        _target = target;
        TargetSize = targetSize;
    }

    public long TargetSize { get; }
    
    public long Written => _written;

    public void Write(byte[] data, int offset, int count)
    {
        while (count > 0)
        {
            int cur = _cacheCur;
            if (count >= _cache.Length && cur == 0)
            {
                _target.Write(data, offset, count);
                _written += count;
                return;
            }

            int copyLen = Math.Min(_cache.Length - cur, count);
            Buffer.BlockCopy(data, offset, _cache, cur, copyLen);
            _cacheCur = cur + copyLen;
            offset += copyLen;
            count -= copyLen;
            if (_cacheCur == _cache.Length)
            {
                Flush();
            }
        }
    }

    public void Flush()
    {
        if (_cacheCur <= 0)
        {
            return;
        }

        _target.Write(_cache, 0, _cacheCur);
        _written += _cacheCur;
        _cacheCur = 0;
    }
}
