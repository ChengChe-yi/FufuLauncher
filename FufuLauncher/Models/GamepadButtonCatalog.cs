namespace FufuLauncher.Models;

[Flags]
public enum GamepadFamilies
{
    None = 0,
    Xbox = 1,
    DualSense = 2
}

public sealed record PluginConfigChoice(string Value, string Label);

// The gamepad source defines shared roles; it has no plugin/file/section checks.
public static class GamepadButtonCatalog
{
    private sealed record Button(string Xbox, string DualSense);
    private static readonly Button[] Buttons =
    [
        new("None", "None"), new("View", "Create"), new("Menu", "Options"),
        new("A", "Cross"), new("B", "Circle"), new("X", "Square"), new("Y", "Triangle"),
        new("LB", "L1"), new("RB", "R1"), new("LT", "L2"), new("RT", "R2"),
        new("LS", "L3"), new("RS", "R3"),
        new("Up", "Up"), new("Down", "Down"), new("Left", "Left"), new("Right", "Right")
    ];

    // Standard DualSense; other Sony products do not imply DS5 support.
    public static bool IsDualSense(ushort vendor, ushort product) => vendor == 0x054C && product == 0x0CE6;

    public static string Role(string value)
    {
        var name = value.Trim();
        if (name.Length == 0) return "None";
        foreach (var button in Buttons)
            if (name.Equals(button.Xbox, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(button.DualSense, StringComparison.OrdinalIgnoreCase)) return button.Xbox;
        return name.ToUpperInvariant() switch
        {
            "BACK" or "SHARE" => "View",
            "START" => "Menu",
            "DPADUP" => "Up", "DPADDOWN" => "Down", "DPADLEFT" => "Left", "DPADRIGHT" => "Right",
            "TOUCHPAD" => "Touchpad",
            _ => name
        };
    }

    public static IReadOnlyList<PluginConfigChoice> Create(GamepadFamilies families)
    {
        var dsOnly = families == GamepadFamilies.DualSense;
        var combined = families == GamepadFamilies.None || families == (GamepadFamilies.Xbox | GamepadFamilies.DualSense);
        var choices = Buttons.Select(button => new PluginConfigChoice(dsOnly ? button.DualSense : button.Xbox,
            combined && button.Xbox != button.DualSense ? $"{button.Xbox} / {button.DualSense}" :
                dsOnly ? button.DualSense : button.Xbox)).ToList();
        if (families != GamepadFamilies.Xbox) choices.Add(new PluginConfigChoice("Touchpad", "Touchpad"));
        return choices;
    }
}
