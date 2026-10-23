/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using FufuLauncher.Constants;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;

namespace FufuLauncher.Services.UID;

public enum BanCheckOutcome
{
    Passed,


    Revoked,


    Banned
}

public sealed class BanCheckResult
{
    public BanCheckOutcome Outcome
    {
        get;
        init;
    } = BanCheckOutcome.Passed;


    public string? Uid
    {
        get;
        init;
    }

    public string Reason
    {
        get;
        init;
    } = string.Empty;


    public string AppealUrl
    {
        get;
        init;
    } = string.Empty;

    public bool ShouldTerminate => Outcome is BanCheckOutcome.Revoked or BanCheckOutcome.Banned;
}

public sealed class BanCheckService
{
    private const string UserAgent = "FufuLauncher Unlock/1.7.0.0";
    private const int RequestTimeoutSeconds = 10;


    private const uint MinimumUid = 10000000;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds) };

    private readonly IUidLookupService _uidLookup;

    public BanCheckService(IUidLookupService uidLookup)
    {
        _uidLookup = uidLookup;
    }

    public async Task<BanCheckResult> CheckAsync(
        IReadOnlyList<string>? knownUids = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var localUids = knownUids ?? await _uidLookup.LoadAndWriteUidsAsync().ConfigureAwait(false);
            if (localUids.Count == 0)
            {
                DebugLog.Info("BanCheckService", "[BanCheck] No local UID available, skipping ban check");
                return new BanCheckResult { Outcome = BanCheckOutcome.Passed };
            }

            DebugLog.Info("BanCheckService", $"[BanCheck] Comparing {localUids.Count} local UIDs");

            var payload = await FetchAsync(cancellationToken).ConfigureAwait(false);
            if (payload == null)
            {
                DebugLog.Info("BanCheckService", "[BanCheck] Server unreachable or returned invalid data, allowing");
                return new BanCheckResult { Outcome = BanCheckOutcome.Passed };
            }

            if (payload.Status == false)
            {
                DebugLog.Info("BanCheckService", "[BanCheck] Access revoked by server");
                return new BanCheckResult
                {
                    Outcome = BanCheckOutcome.Revoked,
                    Reason = "BanCheck_RevokedReason".GetLocalized(),
                    AppealUrl = ResolveAppealUrl(payload)
                };
            }

            var matched = FindBlockedUid(localUids, payload.BannedUids);
            if (matched != null)
            {
                DebugLog.Info("BanCheckService", $"[BanCheck] UID {matched} found in ban list");
                return new BanCheckResult
                {
                    Outcome = BanCheckOutcome.Banned,
                    Uid = matched,
                    Reason = "BanCheck_BannedReason".GetLocalized(),
                    AppealUrl = ResolveAppealUrl(payload)
                };
            }

            DebugLog.Info("BanCheckService", "[BanCheck] Check passed");
            return new BanCheckResult { Outcome = BanCheckOutcome.Passed };
        }
        catch (Exception ex)
        {
            DebugLog.Info("BanCheckService", $"[BanCheck] Check error, allowing - {ex.Message}");
            return new BanCheckResult { Outcome = BanCheckOutcome.Passed };
        }
    }


    private static string? FindBlockedUid(IReadOnlyList<string> localUids, IReadOnlyList<uint> bannedUids)
    {
        if (bannedUids.Count == 0) return null;

        var banned = new HashSet<uint>(bannedUids);

        foreach (var uid in localUids)
        {
            if (!uint.TryParse(uid, out var value)) continue;
            if (value <= MinimumUid) continue;
            if (banned.Contains(value)) return uid;
        }

        return null;
    }

    private static string ResolveAppealUrl(BanStatusPayload payload)
    {
        return IsSafeUrl(payload.AppealUrl) ? payload.AppealUrl! : ApiEndpoints.GithubBanAppealUrl;
    }


    private static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    private static async Task<BanStatusPayload?> FetchAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiEndpoints.BanCheckUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            DebugLog.Info("BanCheckService", $"[BanCheck] HTTP {(int)response.StatusCode}");
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Parse(json);
    }

    private static BanStatusPayload? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            bool? status;
            if (root.TryGetProperty("Status", out var statusElement))
            {
                status = ReadStatus(statusElement);
            }
            else if (root.TryGetProperty("status", out statusElement))
            {
                status = ReadStatus(statusElement);
            }
            else
            {
                DebugLog.Info("BanCheckService", "[BanCheck] Response missing Status field");
                return null;
            }

            if (status == null)
            {
                DebugLog.Info("BanCheckService", "[BanCheck] Unrecognized Status field, allowing");
                return null;
            }

            var banned = new List<uint>();
            if (root.TryGetProperty("BannedUIDs", out var bannedElement) ||
                root.TryGetProperty("bannedUIDs", out bannedElement))
            {
                if (bannedElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in bannedElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Number && item.TryGetUInt32(out var number))
                        {
                            banned.Add(number);
                        }
                        else if (item.ValueKind == JsonValueKind.String &&
                                 uint.TryParse(item.GetString(), out var parsed))
                        {
                            banned.Add(parsed);
                        }
                    }
                }
            }

            string? appealUrl = null;
            if ((root.TryGetProperty("AppealUrl", out var appealElement) ||
                 root.TryGetProperty("appealUrl", out appealElement)) &&
                appealElement.ValueKind == JsonValueKind.String)
            {
                appealUrl = appealElement.GetString();
            }

            return new BanStatusPayload(status, banned, appealUrl);
        }
        catch (Exception ex)
        {
            DebugLog.Info("BanCheckService", $"[BanCheck] Failed to parse response - {ex.Message}");
            return null;
        }
    }


    private static bool? ReadStatus(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString()?.Trim().ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            _ => null
        },
        JsonValueKind.Number => element.TryGetInt32(out var number) ? number != 0 : null,
        _ => null
    };

    private sealed record BanStatusPayload(bool? Status, IReadOnlyList<uint> BannedUids, string? AppealUrl);
}