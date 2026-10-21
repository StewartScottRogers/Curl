namespace Curl.Core;

/// <summary>
/// The time a transfer has left before a timeout, in milliseconds, as libcurl 8.22.0's
/// <c>Curl_timeleft_ms</c> answers it from <c>-m</c>/<c>--max-time</c> and <c>--connect-timeout</c>:
/// 0 when no timeout is active, -1 when the limit is reached exactly, negative when it is overdue.
/// While connecting the smaller of the two limits counts, the connect timeout defaulting to
/// <see cref="DefaultConnectTimeout"/>; once connected only <c>-m</c> counts.
/// </summary>
public static class TransferTimeLeft
{
    /// <summary>libcurl's <c>DEFAULT_CONNECT_TIMEOUT</c>, 300 seconds, the connect timeout when none is set.</summary>
    public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(300);

    /// <summary>Answers <c>Curl_timeleft_ms</c>.</summary>
    /// <param name="maxTime"><c>-m</c>; <see cref="TimeSpan.Zero"/> when none is set.</param>
    /// <param name="connectTimeout"><c>--connect-timeout</c>; <see cref="TimeSpan.Zero"/> when none is set.</param>
    /// <param name="connecting">Whether the transfer is still connecting.</param>
    /// <param name="elapsed">The time since the transfer started.</param>
    /// <returns>The milliseconds left: 0 for no timeout active, -1 for exactly expired, negative when overdue.</returns>
    public static long Milliseconds(TimeSpan maxTime, TimeSpan connectTimeout, bool connecting, TimeSpan elapsed)
    {
        long left = maxTime > TimeSpan.Zero ? Left(maxTime, elapsed) : 0;
        if (!connecting)
        {
            return left;
        }

        long connectLeft = Left(connectTimeout > TimeSpan.Zero ? connectTimeout : DefaultConnectTimeout, elapsed);
        return left == 0 ? connectLeft : Math.Min(left, connectLeft);
    }

    private static long Left(TimeSpan limit, TimeSpan elapsed)
    {
        long left = (long)(limit - elapsed).TotalMilliseconds;
        return left == 0 ? -1 : left;
    }
}
