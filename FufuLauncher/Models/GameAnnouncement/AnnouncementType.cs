/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.GameAnnouncement
{
    public class AnnouncementType
    {
        [JsonPropertyName("id")]
        public int Id
        {
            get; set;
        }

        [JsonPropertyName("name")]
        public string Name
        {
            get; set;
        } = string.Empty;

        [JsonPropertyName("mi18n_name")]
        public string MI18NName
        {
            get; set;
        } = string.Empty;
    }
}
