/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using ZstdSharp;

namespace FufuLauncher.Helpers.Patch;

internal interface IPatchDecompressor
{
    bool CanOpen(string compressType);
    
    Stream Open(long uncompressedSize, PatchInput diff, long codeBegin, long codeEnd);
}

internal sealed class ZstdPatchDecompressor : IPatchDecompressor
{
    public static readonly ZstdPatchDecompressor Instance = new();

    private ZstdPatchDecompressor()
    {
    }

    public bool CanOpen(string compressType)
    {
        return string.Equals(compressType, "zstd", StringComparison.Ordinal);
    }

    public Stream Open(long uncompressedSize, PatchInput diff, long codeBegin, long codeEnd)
    {
        return new DecompressionStream(new PatchSubStream(diff, codeBegin, codeEnd));
    }
}
