/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace MihoyoBBS;

public class CnConfig
{
    public bool Enable
    {
        get;
        set;
    } = true;

    public string UserAgent
    {
        get;
        set;
    } =
        "Mozilla/5.0 (Linux; Android 12; Unspecified Device) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/103.0.5060.129 Mobile Safari/537.36";

    public int Retries
    {
        get;
        set;
    } = 3;

    public GameConfig Genshin
    {
        get;
        set;
    } = new GameConfig();
}
