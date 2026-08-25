/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Helpers;

public enum LaunchTriggerAction
{
    None = 0,
    Predownload,
    GameUpdate
}

public static class LaunchArguments
{
    public const string PredownloadArgument = "--predownload";
    public const string GameUpdateArgument = "--game-update";
    
    public static LaunchTriggerAction ParseTrigger(string[] args)
    {
        if (args is null || args.Length == 0)
        {
            return LaunchTriggerAction.None;
        }

        foreach (var arg in args)
        {
            if (string.Equals(arg, PredownloadArgument, StringComparison.OrdinalIgnoreCase))
            {
                return LaunchTriggerAction.Predownload;
            }

            if (string.Equals(arg, GameUpdateArgument, StringComparison.OrdinalIgnoreCase))
            {
                return LaunchTriggerAction.GameUpdate;
            }
        }

        return LaunchTriggerAction.None;
    }
    
    public static LaunchTriggerAction ParseTrigger(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return LaunchTriggerAction.None;
        }

        var tokens = commandLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return ParseTrigger(tokens);
    }
}
