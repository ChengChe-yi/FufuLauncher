/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.GameAnnouncement
{
    public class AnnouncementContent
    {
        [JsonPropertyName("ann_id")]
        public int AnnId
        {
            get; set;
        }

        [JsonPropertyName("title")]
        public string Title
        {
            get; set;
        } = string.Empty;

        [JsonPropertyName("subtitle")]
        public string Subtitle
        {
            get; set;
        } = string.Empty;

        [JsonPropertyName("banner")]
        public string Banner
        {
            get; set;
        } = string.Empty;

        [JsonPropertyName("content")]
        public string Content
        {
            get; set;
        } = string.Empty;

        [JsonPropertyName("lang")]
        public string Lang
        {
            get; set;
        } = string.Empty;
    }
}
