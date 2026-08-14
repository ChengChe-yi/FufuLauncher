/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Models;
using MihoyoBBS;

namespace FufuLauncher.Contracts.Services;

public interface IHoyoverseCheckinService
{
    Task<List<string>> GetBoundUidsAsync(Dictionary<string, string> cookies, string serverType);
    Task<(string status, string summary)> GetCheckinStatusAsync(string targetUid, Dictionary<string, string> cookies, string serverType);
    Task<(bool success, string message)> ExecuteCheckinAsync(string targetUid, Dictionary<string, string> cookies, string serverType);
    Task<CheckinCalendarData?> GetCalendarDataAsync(Dictionary<string, string> cookies, string serverType);
    Task<CheckinResignInfo?> GetResignInfoAsync(string targetUid, Dictionary<string, string> cookies, string serverType);
    Task<(bool success, string message)> ExecuteResignAsync(string targetUid, Dictionary<string, string> cookies, string serverType);
}
