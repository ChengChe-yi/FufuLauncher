/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Services.CycleChallenge;

/// <summary>
/// 周期挑战的徽章 / 塔罗 / 星级图标映射。
/// <para>
/// 图标资源位于 <c>FufuLauncher/Assets/CycleChallenge/Icons/</c>，
/// 由 csproj 的 Content 项随应用输出（ms-appx 路径）。
/// 所有方法都只返回字符串路径，不抛异常：调用方拿到的空字符串表示"无可用图标"，应自行隐藏控件。
/// </para>
/// </summary>
public static class CycleChallengeIcons
{
    /// <summary>图标目录的 ms-appx 前缀。</summary>
    public const string IconFolderUri = "ms-appx:///Assets/CycleChallenge/Icons/";

    /// <summary>危战难度徽章（UI_LeyLineChallenge_Medal_{0..7}）。</summary>
    private const string StygianMedalPrefix = "UI_LeyLineChallenge_Medal_";

    /// <summary>剧诗纹章（UI_RoleCombat_Medal_S_{0..5}）。</summary>
    private const string TheaterHeraldryPrefix = "UI_RoleCombat_Medal_S_";

    /// <summary>
    /// 把图标名（如 <c>UI_LeyLineChallenge_Medal_5</c>）转换成 ms-appx 完整路径。
    /// <list type="bullet">
    /// <item>传入 <c>null</c>/空白 → 返回 <see cref="string.Empty"/>。</item>
    /// <item>传入完整 URI（http/https/ms-appx/file）→ 原样返回，方便直接兼容接口返回的绝对地址。</item>
    /// <item>带目录或扩展名的名字 → 只保留文件名并统一补 <c>.png</c>。</item>
    /// </list>
    /// </summary>
    public static string GetLocal(string? iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return string.Empty;
        }

        string name = iconName.Trim();

        // 接口有时直接给绝对 URL，此时原样交给 RemoteImage
        if (name.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("ms-appx://", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("ms-appdata://", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        // 兼容 "Icon/UI_xxx.png"、"UI_xxx.png" 两种写法
        int slash = name.LastIndexOfAny(['/', '\\']);
        if (slash >= 0 && slash < name.Length - 1)
        {
            name = name[(slash + 1)..];
        }

        if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return name.Length == 0 ? string.Empty : IconFolderUri + name + ".png";
    }

    /// <summary>
    /// 危战难度徽章：<c>UI_LeyLineChallenge_Medal_{difficulty}</c>。
    /// 优先使用调用方传入的接口 icon 名（走 <see cref="GetLocal"/>），
    /// 没有 icon 名时再按 difficulty 兜底。
    /// </summary>
    /// <param name="difficulty">接口 best.difficulty，有效范围 1..7，越界会被夹取到 0..7。</param>
    /// <param name="iconName">接口返回的 best.icon（如 <c>UI_LeyLineChallenge_Medal_5</c>），可为空。</param>
    public static string DifficultyMedal(int difficulty, string? iconName = null)
    {
        string fromApi = GetLocal(iconName);
        if (fromApi.Length > 0)
        {
            return fromApi;
        }

        int index = Math.Clamp(difficulty, 0, 7);
        return GetLocal(StygianMedalPrefix + index);
    }

    /// <summary>
    /// 剧诗纹章图标：<c>UI_RoleCombat_Medal_S_{heraldry}</c>（heraldry 越界夹取到 0..5）。
    /// </summary>
    public static string TheaterHeraldry(int heraldry)
    {
        int index = Math.Clamp(heraldry, 0, 5);
        return GetLocal(TheaterHeraldryPrefix + index);
    }

    /// <summary>
    /// 剧诗塔罗图标：获得徽记 → <c>Tarot_04</c>，未获得 → <c>Tarot_05</c>。
    /// <c>tarot_serial_no</c> 只用于标题文案，不作为图标依据。
    /// </summary>
    public static string Tarot(bool gotMedal)
    {
        return GetLocal(gotMedal
            ? "UI_Icon_RoleCombat_Reward_Tarot_04"
            : "UI_Icon_RoleCombat_Reward_Tarot_05");
    }

    /// <summary>深境螺旋星级图标：<c>UI_Icon_Tower_Star</c>。</summary>
    public static string AbyssStar()
    {
        return GetLocal("UI_Icon_Tower_Star");
    }

    /// <summary>
    /// 剧诗单幕「星章」图标（参考 Hutao）：
    /// 获得 → <c>UI_Icon_Tower_Star1</c>；未获得 → <c>UI_Icon_Tower_Star2</c>。
    /// </summary>
    /// <param name="got">本幕是否获得星章。</param>
    public static string TheaterStar(bool got)
    {
        return GetLocal(got ? "UI_Icon_Tower_Star1" : "UI_Icon_Tower_Star2");
    }
}
