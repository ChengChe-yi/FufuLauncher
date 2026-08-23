/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.

主进程与提权子进程交换的配置：22 行文本文件（路径/命令 ID/RVA/pb 字段号）。
*/
using FufuLauncher.Services.Yae.Proto;

namespace FufuLauncher.Services.Yae;

/// <summary>主进程与提权子进程交换的配置。</summary>
internal sealed class YaeReadConfig
{
    public string GameExePath { get; set; } = string.Empty;
    public string DllPath { get; set; } = string.Empty;
    public string GameDir { get; set; } = string.Empty;
    public string ResultFilePath { get; set; } = string.Empty;
    public string ErrorFilePath { get; set; } = string.Empty;
    public YaeNativeConfiguration NativeConfig { get; set; } = new();
    public AchievementProtoFieldInfo PbInfo { get; set; } = new();

    public static void Write(string path, YaeReadConfig cfg)
    {
        var n = cfg.NativeConfig;
        File.WriteAllLines(path, [
            cfg.GameExePath,
            cfg.DllPath,
            cfg.GameDir,
            cfg.ResultFilePath,
            cfg.ErrorFilePath,
            n.StoreCmdId.ToString(),
            n.AchievementCmdId.ToString(),
            n.DoCmd.ToString(),
            n.UpdateNormalProperty.ToString(),
            n.NewString.ToString(),
            n.FindGameObject.ToString(),
            n.EventSystemUpdate.ToString(),
            n.SimulatePointerClick.ToString(),
            n.ToInt32.ToString(),
            n.TcpStatePtr.ToString(),
            n.SharedInfoPtr.ToString(),
            n.Decompress.ToString(),
            cfg.PbInfo.Id.ToString(),
            cfg.PbInfo.Status.ToString(),
            cfg.PbInfo.TotalProgress.ToString(),
            cfg.PbInfo.CurrentProgress.ToString(),
            cfg.PbInfo.FinishTimestamp.ToString(),
        ]);
    }

    public static YaeReadConfig? Read(string[] lines)
    {
        if (lines.Length < 22) return null;

        static uint ParseUInt(string s) => uint.TryParse(s, out var v) ? v : 0;

        var n = new YaeNativeConfiguration
        {
            StoreCmdId = ParseUInt(lines[5]),
            AchievementCmdId = ParseUInt(lines[6]),
            DoCmd = ParseUInt(lines[7]),
            UpdateNormalProperty = ParseUInt(lines[8]),
            NewString = ParseUInt(lines[9]),
            FindGameObject = ParseUInt(lines[10]),
            EventSystemUpdate = ParseUInt(lines[11]),
            SimulatePointerClick = ParseUInt(lines[12]),
            ToInt32 = ParseUInt(lines[13]),
            TcpStatePtr = ParseUInt(lines[14]),
            SharedInfoPtr = ParseUInt(lines[15]),
            Decompress = ParseUInt(lines[16]),
        };

        return new YaeReadConfig
        {
            GameExePath = lines[0],
            DllPath = lines[1],
            GameDir = lines[2],
            ResultFilePath = lines[3],
            ErrorFilePath = lines[4],
            NativeConfig = n,
            PbInfo = new AchievementProtoFieldInfo
            {
                Id = ParseUInt(lines[17]),
                Status = ParseUInt(lines[18]),
                TotalProgress = ParseUInt(lines[19]),
                CurrentProgress = ParseUInt(lines[20]),
                FinishTimestamp = ParseUInt(lines[21]),
            },
        };
    }
}
