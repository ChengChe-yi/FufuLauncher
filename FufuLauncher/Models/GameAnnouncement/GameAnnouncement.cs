/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;
using FufuLauncher.Helpers;
using FufuLauncher.Helpers.Serialization;

namespace FufuLauncher.Models.GameAnnouncement
{
    public class GameAnnouncement : AnnouncementContent
    {
        #region 界面绑定辅助
        
        public bool ShouldShowTimeDescription
        {
            get => Type == 1;
        }

        public string TimeDescription
        {
            get
            {
                if (StartTime <= DateTimeOffset.UnixEpoch)
                {
                    return string.Empty;
                }

                DateTimeOffset now = DateTimeOffset.UtcNow;

                // 尚未开始
                if (StartTime > now)
                {
                    TimeSpan span = StartTime - now;
                    return span.TotalDays <= 1
                        ? string.Format("GameAnnouncement_TimeHoursBegin".GetLocalized(), (int)span.TotalHours)
                        : string.Format("GameAnnouncement_TimeDaysBegin".GetLocalized(), (int)span.TotalDays);
                }

                TimeSpan remaining = EndTime - now;
                return remaining.TotalDays <= 1
                    ? string.Format("GameAnnouncement_TimeHoursEnd".GetLocalized(), (int)remaining.TotalHours)
                    : string.Format("GameAnnouncement_TimeDaysEnd".GetLocalized(), (int)remaining.TotalDays);
            }
        }

        public bool ShouldShowTimePercent
        {
            get => ShouldShowTimeDescription && TimePercent is > 0 and < 1;
        }

        public double TimePercent
        {
            get
            {
                TimeSpan total = EndTime - StartTime;
                if (total <= TimeSpan.Zero)
                {
                    return 0;
                }

                TimeSpan current = DateTimeOffset.UtcNow - StartTime;
                return current / total;
            }
        }

        public string TimeFormatted
        {
            get
            {
                if (StartTime <= DateTimeOffset.UnixEpoch)
                {
                    return string.Empty;
                }

                return $"{StartTime.ToLocalTime():yyyy.MM.dd HH:mm} - {EndTime.ToLocalTime():yyyy.MM.dd HH:mm}";
            }
        }

        #endregion

        [JsonPropertyName("type_label")]
        public string TypeLabel
        {
            get; set;
        } = string.Empty;

        [JsonPropertyName("tag_label")]
        public string TagLabel
        {
            get; set;
        } = string.Empty;

        [JsonPropertyName("tag_icon")]
        public string TagIcon
        {
            get; set;
        } = string.Empty;

        [JsonPropertyName("login_alert")]
        public int LoginAlert
        {
            get; set;
        }

        [JsonPropertyName("start_time")]
        [JsonConverter(typeof(SimpleDateTimeOffsetConverter))]
        public DateTimeOffset StartTime
        {
            get; set;
        }

        [JsonPropertyName("end_time")]
        [JsonConverter(typeof(SimpleDateTimeOffsetConverter))]
        public DateTimeOffset EndTime
        {
            get; set;
        }

        [JsonPropertyName("type")]
        public int Type
        {
            get; set;
        }

        [JsonPropertyName("remind")]
        public int Remind
        {
            get; set;
        }

        [JsonPropertyName("alert")]
        public int Alert
        {
            get; set;
        }

        [JsonPropertyName("tag_start_time")]
        public string TagStartTime
        {
            get; set;
        } = string.Empty;

        [JsonPropertyName("tag_end_time")]
        public string TagEndTime
        {
            get; set;
        } = string.Empty;

        [JsonPropertyName("remind_ver")]
        public int RemindVersion
        {
            get; set;
        }

        [JsonPropertyName("has_content")]
        public bool HasContent
        {
            get; set;
        }
    }
}
