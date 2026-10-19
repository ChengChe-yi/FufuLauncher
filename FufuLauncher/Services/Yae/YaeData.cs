/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Services.Yae;

public sealed class YaeData
{
    public YaeData(YaeCommandKind kind, byte[] payload)
    {
        Kind = kind;
        Payload = payload;
    }


    public static YaeData SessionEnd
    {
        get;
    } = new(YaeCommandKind.SessionEnd, []);

    public YaeCommandKind Kind
    {
        get;
    }


    public byte[] Payload
    {
        get;
    }
}