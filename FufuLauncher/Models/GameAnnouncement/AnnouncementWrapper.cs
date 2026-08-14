/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;
using FufuLauncher.Helpers.Serialization;

namespace FufuLauncher.Models.GameAnnouncement
{
    public class AnnouncementWrapper : IJsonOnDeserialized
    {
        [JsonPropertyName("list")]
        public List<AnnouncementListWrapper> List
        {
            get; set;
        } = new();

        [JsonPropertyName("total")]
        public int Total
        {
            get; set;
        }

        [JsonPropertyName("type_list")]
        public List<AnnouncementType> TypeList
        {
            get; set;
        } = new();

        [JsonPropertyName("alert")]
        public bool Alert
        {
            get; set;
        }

        [JsonPropertyName("alert_id")]
        public int AlertId
        {
            get; set;
        }

        [JsonPropertyName("timezone")]
        public int TimeZone
        {
            get; set;
        }

        [JsonPropertyName("t")]
        public string TimeStamp
        {
            get; set;
        } = string.Empty;

        public void OnDeserialized()
        {
            if (List is null)
            {
                return;
            }

            TimeSpan offset = TimeSpan.FromHours(TimeZone);

            foreach (AnnouncementListWrapper wrapper in List)
            {
                if (wrapper.List is null)
                {
                    continue;
                }

                foreach (GameAnnouncement item in wrapper.List)
                {
                    item.StartTime = item.StartTime.AdjustOffsetOnly(offset);
                    item.EndTime = item.EndTime.AdjustOffsetOnly(offset);
                }
            }
        }
    }
}
