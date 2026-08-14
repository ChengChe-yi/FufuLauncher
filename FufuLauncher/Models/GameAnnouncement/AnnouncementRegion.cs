/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Helpers;

namespace FufuLauncher.Models.GameAnnouncement
{
    public enum AnnouncementRegion
    {
        CNGF01,
        CNQD01,
        OSUSA,
        OSEURO,
        OSASIA,
        OSCHT
    }

    public static class AnnouncementRegionExtensions
    {
        public static string ToCode(this AnnouncementRegion region)
        {
            return region switch
            {
                AnnouncementRegion.CNGF01 => "cn_gf01",
                AnnouncementRegion.CNQD01 => "cn_qd01",
                AnnouncementRegion.OSUSA => "os_usa",
                AnnouncementRegion.OSEURO => "os_euro",
                AnnouncementRegion.OSASIA => "os_asia",
                AnnouncementRegion.OSCHT => "os_cht",
                _ => "cn_gf01"
            };
        }

        public static bool TryParse(string? code, out AnnouncementRegion region)
        {
            region = code switch
            {
                "cn_gf01" => AnnouncementRegion.CNGF01,
                "cn_qd01" => AnnouncementRegion.CNQD01,
                "os_usa" => AnnouncementRegion.OSUSA,
                "os_euro" => AnnouncementRegion.OSEURO,
                "os_asia" => AnnouncementRegion.OSASIA,
                "os_cht" => AnnouncementRegion.OSCHT,
                _ => AnnouncementRegion.CNGF01
            };

            return code is not null && region.ToCode() == code;
        }

        public static bool IsOversea(this AnnouncementRegion region)
        {
            return region is AnnouncementRegion.OSUSA or AnnouncementRegion.OSEURO
                or AnnouncementRegion.OSASIA or AnnouncementRegion.OSCHT;
        }
        
        public static AnnouncementRegion GetDefaultRegion(ServerType server)
        {
            return server == ServerType.OS ? AnnouncementRegion.OSUSA : AnnouncementRegion.CNGF01;
        }

        public static string GetDisplayName(this AnnouncementRegion region)
        {
            return region switch
            {
                AnnouncementRegion.CNGF01 => "GameAnnouncement_Region_CNGF01".GetLocalized(),
                AnnouncementRegion.CNQD01 => "GameAnnouncement_Region_CNQD01".GetLocalized(),
                AnnouncementRegion.OSUSA => "GameAnnouncement_Region_OSUSA".GetLocalized(),
                AnnouncementRegion.OSEURO => "GameAnnouncement_Region_OSEURO".GetLocalized(),
                AnnouncementRegion.OSASIA => "GameAnnouncement_Region_OSASIA".GetLocalized(),
                AnnouncementRegion.OSCHT => "GameAnnouncement_Region_OSCHT".GetLocalized(),
                _ => region.ToString()
            };
        }
    }
    
    public sealed class AnnouncementRegionOption
    {
        public AnnouncementRegion Value
        {
            get;
        }

        public string DisplayName
        {
            get => Value.GetDisplayName();
        }

        public AnnouncementRegionOption(AnnouncementRegion value)
        {
            Value = value;
        }
    }
}
