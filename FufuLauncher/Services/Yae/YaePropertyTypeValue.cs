/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Runtime.InteropServices;

namespace FufuLauncher.Services.Yae;

/// <summary>
/// Yae 推送的玩家属性对（0x03），对应原生结构体：int 属性类型 + double 属性值。
/// 必须与 YaeAchievementLib 内部布局一致（Pack = 1，共 12 字节）。
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct YaePropertyTypeValue
{
    public readonly int Type;
    public readonly double Value;
}
