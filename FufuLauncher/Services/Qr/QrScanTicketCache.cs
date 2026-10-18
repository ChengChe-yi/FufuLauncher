/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Services;

internal sealed class QrScanTicketCache
{
    private readonly HashSet<(string? Biz, string Ticket, string Types)> _seen = [];

    internal bool TryAdd(MiyousheLoginQr qr)
    {
        var identity = (qr.GameBiz, qr.Ticket, qr.TokenTypes);
        if (_seen.Contains(identity)) return false;

        if (_seen.Count >= 1024) throw new MiyousheQrException("MiyousheQr_SessionLimit");
        return _seen.Add(identity);
    }
}