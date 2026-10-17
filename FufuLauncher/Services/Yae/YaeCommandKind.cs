/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Services.Yae;

public enum YaeCommandKind : byte
{
    None = 0,
    
    ResponseAchievement = 1,
    
    ResponsePlayerStore = 2,
    
    ResponsePlayerProp = 3,
    
    RequestCmdId = 252,
    
    RequestRva = 253,
    
    RequestResumeThread = 254,
    
    SessionEnd = 255,
}