/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Services;

internal static class ContinuousQrScanService
{
    internal static async Task ScanAsync(Func<CancellationToken, Task<string[]>> readFrame,
        Func<MiyousheLoginQr, CancellationToken, Task> onCode, Func<bool> isPaused,
        Func<bool> isCurrentAccount, Action<string> reportStatus, CancellationToken cancellationToken,
        TimeSpan? interval = null)
    {
        var tickets = new QrScanTicketCache();
        TimeSpan delay = interval ?? TimeSpan.FromMilliseconds(200);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!isCurrentAccount()) throw new MiyousheQrException("MiyousheQr_AccountChanged");
            if (!isPaused())
            {
                var decoded = await readFrame(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!isCurrentAccount()) throw new MiyousheQrException("MiyousheQr_AccountChanged");
                if (decoded.Length > 1) reportStatus("MiyousheQr_Multiple");
                else if (decoded.Length == 1)
                {
                    if (!MiyousheLoginQr.TryParse(decoded[0], out var qr)) reportStatus("MiyousheQr_Unsupported");
                    else if (tickets.TryAdd(qr!))
                    {
                        if (qr!.IsExpired) reportStatus("MiyousheQr_Expired");
                        else await onCode(qr, cancellationToken);
                    }
                }
            }
            await Task.Delay(delay, cancellationToken);
        }
    }
}
