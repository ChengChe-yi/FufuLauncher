/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text.Json;
using FufuLauncher.Models.MiHoYo.Passport;

namespace FufuLauncher.Services.MiHoYo.Passport;

internal static class PassportHttpUtil
{
    public static string? GetSingleHeader(HttpResponseMessage response, string headerName)
    {
        return response.Headers.TryGetValues(headerName, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;
    }
    
    public static async Task<PassportResponse<TData>> DeserializeAsync<TData>(HttpResponseMessage response, CancellationToken token)
    {
        string json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize<PassportResponse<TData>>(json)
                   ?? CreateFailure<TData>("响应解析失败");
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[Passport] 响应解析失败: {ex.Message}");
            return CreateFailure<TData>($"响应解析失败: {ex.Message}");
        }
    }
    
    public static async Task<PassportResponse> DeserializeAsync(HttpResponseMessage response, CancellationToken token)
    {
        string json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize<PassportResponse>(json) ?? CreateFailure("响应解析失败");
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[Passport] 响应解析失败: {ex.Message}");
            return CreateFailure($"响应解析失败: {ex.Message}");
        }
    }

    private static PassportResponse<TData> CreateFailure<TData>(string message) => new() { Message = message };

    private static PassportResponse CreateFailure(string message) => new() { Message = message };
}
