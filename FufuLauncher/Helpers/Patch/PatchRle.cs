/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Helpers.Patch;

internal sealed class RleDecoder
{
    private const int TypeBits = 2;

    private readonly PatchSectionReader _ctrl;
    private readonly PatchSectionReader _code;
    private readonly byte[] _scratch = new byte[FufuPatch.CacheSize];
    private long _memSetLength;
    private byte _memSetValue;
    private long _memCopyLength;

    public RleDecoder(PatchSectionReader ctrl, PatchSectionReader code)
    {
        _ctrl = ctrl;
        _code = code;
    }

    public bool IsFinished => _memSetLength == 0 && _memCopyLength == 0 && _ctrl.IsFinished && _code.IsFinished;

    public void EnsureFinished()
    {
        if (!IsFinished)
        {
            throw new PatchFormatException($"rle not finished: memSet={_memSetLength} memCopy={_memCopyLength} ctrlFinished={_ctrl.IsFinished} codeFinished={_code.IsFinished}");
        }
    }
    
    public void Decode(byte[] dst, int count)
    {
        int offset = 0;
        int remaining = count;
        while (remaining > 0)
        {
            EnsureData();

            if (_memSetLength > 0)
            {
                int step = (int)Math.Min(_memSetLength, remaining);
                if (_memSetValue != 0)
                {
                    for (int i = 0; i < step; i++)
                    {
                        dst[offset + i] += _memSetValue;
                    }
                }

                offset += step;
                remaining -= step;
                _memSetLength -= step;
                continue;
            }

            if (_memCopyLength > 0)
            {
                int step = (int)Math.Min(Math.Min(_memCopyLength, remaining), _scratch.Length);
                _code.CopyTo(_scratch, 0, step);
                for (int i = 0; i < step; i++)
                {
                    dst[offset + i] += _scratch[i];
                }

                offset += step;
                remaining -= step;
                _memCopyLength -= step;
            }
        }
    }
    
    public void Skip(long count)
    {
        long remaining = count;
        while (remaining > 0)
        {
            EnsureData();

            if (_memSetLength > 0)
            {
                long step = Math.Min(_memSetLength, remaining);
                _memSetLength -= step;
                remaining -= step;
                continue;
            }

            if (_memCopyLength > 0)
            {
                long step = Math.Min(Math.Min(_memCopyLength, remaining), _scratch.Length);
                _code.Skip(step);
                _memCopyLength -= step;
                remaining -= step;
            }
        }
    }
    
    private void EnsureData()
    {
        while (_memSetLength == 0 && _memCopyLength == 0 && !_ctrl.IsFinished)
        {
            // �����ֽڱ����Ǳ䳤������һ���֣�����ռ�� 2 λ�����ȴ�ͬһ�ֽڿ�ʼ����
            byte ctrl = _ctrl.PeekByte();
            int type = ctrl >> (8 - TypeBits);
            long length = _ctrl.ReadVarInt(TypeBits) + 1;
            FufuPatch.Diag($"rle entry: type={type} length={length} ctrl={ctrl:X2}");
            switch (type)
            {
                case 0:
                    _memSetLength = length;
                    _memSetValue = 0;
                    break;

                case 1:
                    _memSetLength = length;
                    _memSetValue = 255;
                    break;

                case 2:
                    _memSetLength = length;
                    _memSetValue = _code.ReadByteValue();
                    break;

                case 3:
                    _memCopyLength = length;
                    break;

                default:
                    throw new PatchFormatException($"invalid rle type {type}");
            }
        }

        if (_memSetLength == 0 && _memCopyLength == 0)
        {
            throw new PatchFormatException("rle stream exhausted");
        }
    }
}
