/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Helpers;

/// <summary>
/// 令牌桶限速器。线程安全，可用于全局或单连接限速。
/// <para>速率 &lt;= 0 表示不限速，此时 <see cref="AcquireAsync"/> 立即返回，
/// 调用方无需为“未启用”分支写特判。</para>
/// </summary>
public sealed class TokenBucketRateLimiter
{
    private readonly long _bytesPerSecond;
    private readonly double _baseCapacity;
    private readonly object _sync = new();

    private double _tokens;
    private long _lastRefillTicks;

    /// <summary>
    /// 迄今见过的最大单次申请量。
    /// <para>桶容量必须不低于它，否则一次超大申请（如 80KB 缓冲区配上极低速率）
    /// 会因为令牌被容量封顶而永远攒不够。</para>
    /// </summary>
    private int _largestRequest;

    /// <param name="bytesPerSecond">每秒允许的字节数；&lt;= 0 表示不限速。</param>
    /// <param name="burstSeconds">突发容量相当于多少秒的流量，至少 0.1 秒。</param>
    public TokenBucketRateLimiter(long bytesPerSecond, double burstSeconds = 1.0)
    {
        _bytesPerSecond = Math.Max(bytesPerSecond, 0);
        _baseCapacity = Math.Max(_bytesPerSecond * Math.Max(burstSeconds, 0.1), 4096);
        _tokens = _baseCapacity;
        _lastRefillTicks = Environment.TickCount64;
    }

    /// <summary>是否处于启用状态。</summary>
    public bool IsEnabled => _bytesPerSecond > 0;

    /// <summary>当前生效的桶容量。</summary>
    private double Capacity => Math.Max(_baseCapacity, _largestRequest);

    /// <summary>
    /// 申请 <paramref name="count"/> 字节的配额，额度不足时异步等待。
    /// </summary>
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

    /// <summary>按经过的时间补充令牌。</summary>
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
