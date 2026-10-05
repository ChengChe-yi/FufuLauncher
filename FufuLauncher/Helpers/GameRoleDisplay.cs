/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Contracts.Services;
using FufuLauncher.Models.MiHoYo;

namespace FufuLauncher.Helpers;

public static class GameRoleDisplay
{
    public static string ServerName(string region, string? regionName = null) => region switch
    {
        ServerRegion.CnGf01 => LocalizedName("GameRole_SkyIsland", "天空岛"),
        ServerRegion.CnQd01 => LocalizedName("GameRole_WorldTree", "世界树"),
        _ => regionName ?? region
    };

    public static string BoundRole(GameRoleInfo role) =>
        $"{role.nickname} · {role.game_uid} · {ServerName(role.region, role.region_name)} · Lv. {role.level}";

    public static string Archive(string uid) => $"{uid} · {ServerName(ServerRegion.Resolve(uid))}";

    private static string LocalizedName(string key, string fallback)
    {
        var value = key.GetLocalized();
        return value == key ? fallback : value;
    }
}
