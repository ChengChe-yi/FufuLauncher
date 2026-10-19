/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Runtime.InteropServices;

namespace FufuLauncher.Services.Yae;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct YaePropertyTypeValue
{
    public readonly int Type;
    public readonly double Value;
}