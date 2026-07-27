/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FufuLauncher.Constants;
using FufuLauncher.Models.DataCenter;

namespace FufuLauncher.Services;

public sealed class GameStatsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(20);

    private static readonly HttpClient Http = CreateClient();

    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private sealed record CacheEntry(string Payload, DateTimeOffset FetchedAt);

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(25)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }
    
    public bool HasAnyCache
    {
        get
        {
            lock (_cache) return _cache.Count > 0;
        }
    }

    public void ClearCache()
    {
        lock (_cache) _cache.Clear();
    }

    public Task<RoleAvgResponse?> GetRoleAveragesAsync(bool forceRefresh, CancellationToken token)
        => GetAsync<RoleAvgResponse>(ApiEndpoints.RoleAvgUrl, forceRefresh, token);

    public Task<AbyssStatsBundle?> GetSpiralAbyssAsync(string? version, string? role, bool forceRefresh,
        CancellationToken token)
        => GetAbyssAsync(ApiEndpoints.SpiralAbyssRankUrl, version, role, forceRefresh, token);

    public Task<AbyssStatsBundle?> GetStygianAsync(string? version, string? role, bool forceRefresh,
        CancellationToken token)
        => GetAbyssAsync(ApiEndpoints.AbyssRank2Url, version, role, forceRefresh, token);

    public Task<WishHistoryResponse?> GetWishHistoryAsync(bool forceRefresh, CancellationToken token)
        => GetAsync<WishHistoryResponse>(ApiEndpoints.WishHistoryUrl, forceRefresh, token);

    public Task<RerunResponse?> GetRerunListAsync(bool forceRefresh, CancellationToken token)
        => GetAsync<RerunResponse>(ApiEndpoints.RerunListUrl, forceRefresh, token);

    private async Task<AbyssStatsBundle?> GetAbyssAsync(string baseUrl, string? version, string? role,
        bool forceRefresh, CancellationToken token)
    {
        var url = BuildQuery(baseUrl, version, role);
        var response = await GetAsync<AbyssStatsResponse>(url, forceRefresh, token).ConfigureAwait(false);
        return response == null ? null : new AbyssStatsBundle(response);
    }
    
    private static string BuildQuery(string baseUrl, string? version, string? role)
    {
        var url = baseUrl;

        if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, "all", StringComparison.OrdinalIgnoreCase))
        {
            url = url.Replace("role=all", "role=" + Uri.EscapeDataString(role!), StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(version))
        {
            url += (url.Contains('?', StringComparison.Ordinal) ? "&" : "?")
                   + "version=" + Uri.EscapeDataString(version!);
        }

        return url;
    }

    private async Task<T?> GetAsync<T>(string url, bool forceRefresh, CancellationToken token) where T : class
    {
        var payload = await GetPayloadAsync(url, forceRefresh, token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload)) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(payload, DataCenterJson.Options);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[GameStatsService] 解析失败 {url}: {ex.Message}");
            lock (_cache) _cache.Remove(url);
            return null;
        }
    }

    private async Task<string?> GetPayloadAsync(string url, bool forceRefresh, CancellationToken token)
    {
        if (!forceRefresh)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(url, out var cached) &&
                    DateTimeOffset.UtcNow - cached.FetchedAt < CacheTtl)
                {
                    return cached.Payload;
                }
            }
        }
        
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!forceRefresh)
            {
                lock (_cache)
                {
                    if (_cache.TryGetValue(url, out var cached) &&
                        DateTimeOffset.UtcNow - cached.FetchedAt < CacheTtl)
                    {
                        return cached.Payload;
                    }
                }
            }

            var payload = await Http.GetStringAsync(url, token).ConfigureAwait(false);

            lock (_cache)
            {
                _cache[url] = new CacheEntry(payload, DateTimeOffset.UtcNow);
            }

            return payload;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameStatsService] 请求失败 {url}: {ex.Message}");
            
            lock (_cache)
            {
                if (_cache.TryGetValue(url, out var stale)) return stale.Payload;
            }

            return null;
        }
        finally
        {
            _gate.Release();
        }
    }
}
