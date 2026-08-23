/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.

解析 AchievementAllDataNotify protobuf 数据，转换为 UIAF 项。
解析逻辑参考 HolographicHat/YaeAchievement (GPL-3.0)。
*/
using Google.Protobuf;
using FufuLauncher.Services.Yae.Proto;

namespace FufuLauncher.Services.Yae;

public static class YaeAchievementParser
{
    /// <summary>
    /// 解析成就数据。每个 LengthDelimited 子消息若含至少 3 个 varint 字段，则视为一条成就记录。
    /// </summary>
    public static List<YaeUiafItem> ParseAchievement(byte[] bytes, AchievementProtoFieldInfo pb)
    {
        var data = new List<Dictionary<uint, uint>>();
        var errorTimes = 0;

        using var stream = new CodedInputStream(bytes);
        try
        {
            uint tag;
            while ((tag = stream.ReadTag()) != 0)
            {
                if ((tag & 7) != 2) // LengthDelimited
                {
                    continue;
                }

                Dictionary<uint, uint>? record = [];
                using var entryStream = stream.ReadLengthDelimitedAsStream();
                try
                {
                    while ((tag = entryStream.ReadTag()) != 0)
                    {
                        if ((tag & 7) != 0) // Varint
                        {
                            record = null;
                            break;
                        }
                        record![tag >> 3] = entryStream.ReadUInt32();
                    }

                    if (record is { Count: > 2 })
                    {
                        data.Add(record);
                    }
                }
                catch (InvalidProtocolBufferException)
                {
                    // 允许 1 次失败（reward_taken_goal_id_list 等非目标子消息）
                    if (errorTimes++ > 0)
                    {
                        throw;
                    }
                }
            }
        }
        catch (InvalidProtocolBufferException)
        {
            // 解析失败返回已收集的数据
        }

        return data
            .Select(record => new YaeUiafItem
            {
                Id = (int)record.GetValueOrDefault(pb.Id),
                Current = (int)record.GetValueOrDefault(pb.CurrentProgress),
                Status = (int)record.GetValueOrDefault(pb.Status),
                Timestamp = record.GetValueOrDefault(pb.FinishTimestamp),
            })
            .ToList();
    }
}
