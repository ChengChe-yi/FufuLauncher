/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Services.Yae;

/// <summary>游戏进程创建失败。</summary>
internal sealed class YaeGameCreateException : Exception
{
    public YaeGameCreateException(string message) : base(message) { }
}
