/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Services;

// Keep identities, not the full QR URL: changing expiry/display parameters is not a new login ticket.
internal sealed class QrScanTicketCache
{
    private readonly HashSet<(string? Biz, string Ticket, string Types)> _seen = [];

    internal bool TryAdd(MiyousheLoginQr qr)
    {
        var identity = (qr.GameBiz, qr.Ticket, qr.TokenTypes);
        if (_seen.Contains(identity)) return false;
        // End an unusually long session instead of evicting a ticket and authorizing it again.
        if (_seen.Count >= 1024) throw new MiyousheQrException("MiyousheQr_SessionLimit");
        return _seen.Add(identity);
    }
}
