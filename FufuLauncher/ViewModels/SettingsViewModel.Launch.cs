/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.RegularExpressions;
using FufuLauncher.Helpers;
using FufuLauncher.Services;
using Microsoft.UI.Windowing;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 启动参数与分辨率

    partial void OnSelectedPostLaunchBehaviorItemChanged(PostLaunchBehaviorItem value)
    {
        if (value == null) return;
        _postLaunchBehavior = value.Value;
        _ = _localSettingsService.SaveSettingAsync("PostLaunchBehavior", value.Value.ToString());
    }

    partial void OnLaunchArgsMonitorIndexChanged(int value)
    {
        ApplyPresetsToText();
    }

    partial void OnSelectedMonitorChanged(MonitorItem value)
    {
        if (value != null && LaunchArgsMonitorIndex != value.Index)
        {
            LaunchArgsMonitorIndex = value.Index;
        }
    }

    private void LoadMonitors()
    {
        AvailableMonitors.Clear();
        AvailableMonitors.Add(new MonitorItem("默认 (不指定)", 0));

        var displayAreas = DisplayArea.FindAll();
        for (int i = 0; i < displayAreas.Count; i++)
        {
            int index = i + 1;
            AvailableMonitors.Add(new MonitorItem($"显示器 {index} ({displayAreas[i].OuterBounds.Width}x{displayAreas[i].OuterBounds.Height})", index));
        }

        SelectedMonitor = AvailableMonitors.FirstOrDefault(m => m.Index == LaunchArgsMonitorIndex) ?? AvailableMonitors.FirstOrDefault();
    }

    partial void OnLaunchArgsWidthChanged(string value) => ApplyPresetsToText();
    partial void OnLaunchArgsHeightChanged(string value) => ApplyPresetsToText();
    partial void OnLaunchArgsWindowModeChanged(WindowModeType value) => ApplyPresetsToText();

    private void InitializeDefaultResolution()
    {
        _launchArgsWidth = "";
        _launchArgsHeight = "";
    }

    partial void OnCustomGameExeNameChanged(string value)
    {
        _localSettingsService.SaveSettingAsync(GameExeManager.CustomExeNameKey, value);
    }

    private async Task ResetGameExeNameAsync()
    {
        CustomGameExeName = string.Empty;
        await _localSettingsService.SaveSettingAsync<string>(GameExeManager.CustomExeNameKey, null);
    }

    private void ParseLaunchParameters(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return;

        try
        {
            if (args.Contains("-popupwindow"))
            {
                LaunchArgsWindowMode = WindowModeType.Popup;
            }
            else
            {
                LaunchArgsWindowMode = WindowModeType.Normal;
            }
            
            var monitorMatch = Regex.Match(args, @"-monitor\s+(\d+)");
            if (monitorMatch.Success && int.TryParse(monitorMatch.Groups[1].Value, out int mIndex))
            {
                LaunchArgsMonitorIndex = mIndex;
            }
            else
            {
                LaunchArgsMonitorIndex = 0;
            }

            var parts = args.Split(' ');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i] == "-screen-width")
                    LaunchArgsWidth = parts[i + 1];
                if (parts[i] == "-screen-height")
                    LaunchArgsHeight = parts[i + 1];
            }
        }
        catch
        {
            // ignored
        }
    }

    private void ApplyPresetsToText()
    {
        if (_isLoadingLaunchParams) return;

        var currentArgs = CustomLaunchParameters ?? "";
        
        currentArgs = Regex.Replace(currentArgs, @"-screen-width\s+\S+", "");
        currentArgs = Regex.Replace(currentArgs, @"-screen-height\s+\S+", "");
        currentArgs = Regex.Replace(currentArgs, @"-popupwindow", "");
        currentArgs = Regex.Replace(currentArgs, @"-monitor\s+\d+", "");

        var sb = new System.Text.StringBuilder(currentArgs);
        if (!string.IsNullOrWhiteSpace(LaunchArgsWidth) && !string.IsNullOrWhiteSpace(LaunchArgsHeight))
        {
            sb.Append($" -screen-width {LaunchArgsWidth} -screen-height {LaunchArgsHeight}");
        }
        if (LaunchArgsWindowMode == WindowModeType.Popup)
        {
            sb.Append(" -popupwindow");
        }
        if (LaunchArgsMonitorIndex > 0)
        {
            sb.Append($" -monitor {LaunchArgsMonitorIndex}");
        }

        var finalArgs = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        if (CustomLaunchParameters != finalArgs)
        {
            CustomLaunchParameters = finalArgs;
        }
    }

    partial void OnCustomLaunchParametersChanged(string value)
    {
        _localSettingsService.SaveSettingAsync("CustomLaunchParameters", value);
    }

    #endregion
}
