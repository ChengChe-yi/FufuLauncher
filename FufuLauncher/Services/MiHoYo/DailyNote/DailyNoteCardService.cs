/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Services.MiHoYo.DailyNote;

/// <summary>
/// 便签卡片的取数入口：按服务器类型分发到国服 / 国际服实现。
/// <para>账号与凭据由 <see cref="DailyNoteService"/> 内部按活跃账号解析，调用方无需传入。</para>
/// </summary>
public class DailyNoteCardService
{
    private readonly DailyNoteService _dailyNoteService = new();

    public async Task<DailyNoteCardData?> LoadCardDataAsync(string roleId, string server)
    {
        return await _dailyNoteService.GetDailyNoteAsync(roleId, server);
    }
}
