/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Helpers;

public sealed class TokenBucketRateLimiter
{
    private readonly long _bytesPerSecond;
    private readonly double _baseCapacity;
    private readonly object _sync = new();

    private double _tokens;
    private long _lastRefillTicks;

    private int _largestRequest;

    public TokenBucketRateLimiter(long bytesPerSecond, double burstSeconds = 1.0)
    {
        _bytesPerSecond = Math.Max(bytesPerSecond, 0);
        _baseCapacity = Math.Max(_bytesPerSecond * Math.Max(burstSeconds, 0.1), 4096);
        _tokens = _baseCapacity;
        _lastRefillTicks = Environment.TickCount64;
    }

    public bool IsEnabled => _bytesPerSecond > 0;

    private double Capacity => Math.Max(_baseCapacity, _largestRequest);

    public async ValueTask AcquireAsync(int count, CancellationToken token = default)
    {
        if (!IsEnabled || count <= 0)
        {
            return;
        }

        lock (_sync)
        {
            if (count > _largestRequest)
            {
                _largestRequest = count;
            }
        }

        while (true)
        {
            token.ThrowIfCancellationRequested();

            TimeSpan wait;
            lock (_sync)
            {
                Refill();

                if (_tokens >= count)
                {
                    _tokens -= count;
                    return;
                }

                // 补足缺口所需时间；下限 1ms，避免空转占满 CPU。
                double missing = count - _tokens;
                wait = TimeSpan.FromMilliseconds(
                    Math.Max(missing / _bytesPerSecond * 1000.0, 1.0));
            }

            await Task.Delay(wait, token).ConfigureAwait(false);
        }
    }

    private void Refill()
    {
        long now = Environment.TickCount64;
        long elapsedMs = now - _lastRefillTicks;
        if (elapsedMs <= 0)
        {
            return;
        }

        _lastRefillTicks = now;
        _tokens = Math.Min(_tokens + _bytesPerSecond * elapsedMs / 1000.0, Capacity);
    }
}