/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using Microsoft.Win32;

namespace FufuLauncher.Services.AuthTicket;

public sealed class GameRegistrySnapshot
{
    private const string CnRegistryKey = @"HKEY_CURRENT_USER\Software\miHoYo\原神";
    private const string CnRegistryValue = "MIHOYOSDK_ADL_PROD_CN_h3123967166";
    
    private const string OsRegistryKey = @"HKEY_CURRENT_USER\Software\miHoYo\Genshin Impact";
    private const string OsRegistryValue = "MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810";

    private byte[]? _snapshot;
    private string? _keyName;
    private string? _valueName;
    
    public void TakeSnapshot(bool isOversea)
    {
        _keyName = isOversea ? OsRegistryKey : CnRegistryKey;
        _valueName = isOversea ? OsRegistryValue : CnRegistryValue;

        try
        {
            _snapshot = Registry.GetValue(_keyName, _valueName, null) as byte[];
            Debug.WriteLine($"[RegistrySnapshot] 已保存快照: {_keyName}\\{_valueName} (长度: {_snapshot?.Length ?? 0})");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RegistrySnapshot] 保存快照失败: {ex.Message}");
            _snapshot = null;
        }
    }
    
    public void RestoreSnapshot()
    {
        if (_keyName == null || _valueName == null)
        {
            Debug.WriteLine("[RegistrySnapshot] 无快照可恢复（未调用 TakeSnapshot）");
            return;
        }

        if (_snapshot == null || _snapshot.Length == 0)
        {
            Debug.WriteLine("[RegistrySnapshot] 快照为空，跳过恢复");
            return;
        }

        try
        {
            Registry.SetValue(_keyName, _valueName, _snapshot, RegistryValueKind.Binary);
            Debug.WriteLine($"[RegistrySnapshot] 已恢复快照: {_keyName}\\{_valueName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RegistrySnapshot] 恢复快照失败: {ex.Message}");
        }
        finally
        {
            _snapshot = null;
            _keyName = null;
            _valueName = null;
        }
    }
    
    public bool HasSnapshot => _snapshot is { Length: > 0 };
}
