/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.RegularExpressions;

namespace FufuLauncher.Services.GameAnnouncement
{
    public static class GameAnnouncementRegex
    {
        public static readonly Regex XmlTimeTagRegex = new(
            "&lt;t class=\"t_(?:gl|lc)\".*?&gt;(?:<span .*?>)?(.*?)(?:</span>)?&lt;/t&gt;",
            RegexOptions.Multiline);
    }
}
