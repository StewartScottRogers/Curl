namespace Curl.Core;

/// <summary>
/// A token bucket over the injected <see cref="TimeProvider" />, answering curl 8.22.0's
/// <c>lib/ratelimit.c</c> row for row: <c>Curl_rlimit_init</c> is the constructor, and
/// <see cref="Available" />, <see cref="Drain" />, <see cref="WaitMilliseconds" /> and <see cref="Block" />
/// are <c>Curl_rlimit_avail</c>, <c>_drain</c>, <c>_wait_ms</c> and <c>_block</c>, each reading the clock as
/// the C functions read their <c>pts</c>. Each whole second since the last refill adds the rate's tokens
/// (the part of a second left over carries to the next refill), capped at the burst when one is set; a
/// drain may take the tokens below zero, and the wait then grows by the share of a second the debt is of
/// the rate. A rate of zero is unlimited: <see cref="long.MaxValue" /> tokens, never a wait. A blocked
/// limit has no tokens and never waits; unblocking starts the limit afresh with one second's tokens, so
/// the time spent blocked earns none. <c>Curl_rlimit_start</c>'s tuning for a known total is not here.
/// </summary>
public sealed class TransferRateLimit
{
    private const long MicrosecondsPerStep = 1_000_000;

    private readonly long ratePerStep;

    private readonly long burstPerStep;

    private readonly TimeProvider clock;

    private readonly long origin;

    private long tokens;

    private long lastRefillMicroseconds;

    private long spareMicroseconds;

    private bool blocked;

    /// <summary>Starts the limit now with one second's tokens: <c>Curl_rlimit_init</c>.</summary>
    /// <param name="ratePerSecond">Tokens gained each second; 0 is unlimited.</param>
    /// <param name="burstPerSecond">The most tokens the limit holds after a refill; 0 is no cap.</param>
    /// <param name="clock">The clock the limit reads.</param>
    public TransferRateLimit(long ratePerSecond, long burstPerSecond, TimeProvider clock)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ratePerSecond);
        ArgumentOutOfRangeException.ThrowIfNegative(burstPerSecond);
        ArgumentNullException.ThrowIfNull(clock);
        ratePerStep = ratePerSecond;
        burstPerStep = burstPerSecond;
        this.clock = clock;
        origin = clock.GetTimestamp();
        tokens = ratePerSecond;
    }

    /// <summary>Gets whether the limit is blocked: <c>Curl_rlimit_is_blocked</c>.</summary>
    public bool IsBlocked => blocked;

    /// <summary>The tokens available now, after refilling: <c>Curl_rlimit_avail</c>.</summary>
    /// <returns>0 when blocked, <see cref="long.MaxValue" /> when unlimited, else the tokens, which may be negative.</returns>
    public long Available()
    {
        if (blocked)
        {
            return 0;
        }

        if (ratePerStep == 0)
        {
            return long.MaxValue;
        }

        Refill(NowMicroseconds());
        return tokens;
    }

    /// <summary>Takes <paramref name="count" /> tokens, after refilling, going below zero when it must: <c>Curl_rlimit_drain</c>.</summary>
    /// <param name="count">The tokens consumed; ignored when blocked or unlimited.</param>
    public void Drain(long count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (blocked || ratePerStep == 0)
        {
            return;
        }

        Refill(NowMicroseconds());
        tokens = long.MinValue + count < tokens ? tokens - count : long.MinValue;
    }

    /// <summary>How long until the tokens are positive again: <c>Curl_rlimit_wait_ms</c>.</summary>
    /// <returns>The wait in whole milliseconds, rounded up; 0 when blocked, unlimited or tokens remain.</returns>
    public long WaitMilliseconds()
    {
        if (blocked || ratePerStep == 0)
        {
            return 0;
        }

        long now = NowMicroseconds();
        Refill(now);
        if (tokens > 0)
        {
            return 0;
        }

        Int128 debtPercent = -(Int128)tokens * 100 / ratePerStep;
        Int128 waitMicroseconds = MicrosecondsPerStep - spareMicroseconds + (MicrosecondsPerStep * debtPercent / 100) - (now - lastRefillMicroseconds);
        return (long)Int128.Clamp((waitMicroseconds + 999) / 1000, 0, long.MaxValue);
    }

    /// <summary>
    /// Blocks the limit, emptying it, or unblocks it, starting it afresh with one second's tokens:
    /// <c>Curl_rlimit_block</c>. Asking for the state it is already in changes nothing.
    /// </summary>
    /// <param name="activate"><see langword="true" /> to block, <see langword="false" /> to unblock.</param>
    public void Block(bool activate)
    {
        if (activate == blocked)
        {
            return;
        }

        blocked = activate;
        lastRefillMicroseconds = NowMicroseconds();
        spareMicroseconds = 0;
        tokens = activate ? 0 : ratePerStep;
    }

    // rlimit_update: whole seconds since the last refill, with the spare carried, each add the rate's tokens.
    private void Refill(long now)
    {
        long elapsed = now - lastRefillMicroseconds + spareMicroseconds;
        if (elapsed < MicrosecondsPerStep)
        {
            return;
        }

        lastRefillMicroseconds = now;
        long steps = elapsed / MicrosecondsPerStep;
        spareMicroseconds = elapsed % MicrosecondsPerStep;
        long gain = ratePerStep > long.MaxValue / steps ? long.MaxValue : ratePerStep * steps;
        tokens = long.MaxValue - gain > tokens ? tokens + gain : long.MaxValue;
        if (burstPerStep > 0 && tokens > burstPerStep)
        {
            tokens = burstPerStep;
        }
    }

    private long NowMicroseconds() => clock.GetElapsedTime(origin).Ticks / TimeSpan.TicksPerMicrosecond;
}
