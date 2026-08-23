/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.

Google.Protobuf 扩展：读取 LengthDelimited 嵌套消息。
CodedInputStream.ReadRawBytes 为内部方法，通过 UnsafeAccessor 访问。
技术方案参考 HolographicHat/YaeAchievement (GPL-3.0)。
*/
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Google.Protobuf;

[EditorBrowsable(EditorBrowsableState.Never)]
internal static class YaeCodedInputStreamExtensions
{
    [UnsafeAccessor(UnsafeAccessorKind.Method)]
    private static extern byte[] ReadRawBytes(CodedInputStream stream, int size);

    public static CodedInputStream ReadLengthDelimitedAsStream(this CodedInputStream stream)
        => new(ReadRawBytes(stream, stream.ReadLength()));
}
