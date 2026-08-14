/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.GameAnnouncement
{
    public class GameAnnouncementListResponse
    {
        [JsonPropertyName("retcode")]
        public int Retcode
        {
            get; set;
        }

        [JsonPropertyName("message")]
        public string Message
        {
            get; set;
        } = string.Empty;

        [JsonPropertyName("data")]
        public AnnouncementWrapper Data
        {
            get; set;
        }
    }
    
    public class GameAnnouncementContentResponse
    {
        [JsonPropertyName("retcode")]
        public int Retcode
        {
            get; set;
        }

        [JsonPropertyName("message")]
        public string Message
        {
            get; set;
        } = string.Empty;

        [JsonPropertyName("data")]
        public AnnouncementContentList Data
        {
            get; set;
        }
    }

    public class AnnouncementContentList
    {
        [JsonPropertyName("list")]
        public List<AnnouncementContent> List
        {
            get; set;
        } = new();
    }
}
