/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace MihoyoBBS;

public class Config
{
    public AccountConfig Account
    {
        get;
        set;
    } = new();

    public DeviceConfig Device
    {
        get;
        set;
    } = new();

    public GamesConfig Games
    {
        get;
        set;
    } = new();

    public DisplayConfig Display
    {
        get;
        set;
    } = new();
}
