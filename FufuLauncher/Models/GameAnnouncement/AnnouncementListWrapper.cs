/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.GameAnnouncement
{
    public class AnnouncementListWrapper
    {
        [JsonPropertyName("list")]
        public List<GameAnnouncement> List
        {
            get; set;
        } = new();

        [JsonPropertyName("type_id")]
        public int TypeId
        {
            get; set;
        }

        [JsonPropertyName("type_label")]
        public string TypeLabel
        {
            get; set;
        } = string.Empty;
    }
}
