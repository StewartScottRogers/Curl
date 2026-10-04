namespace Curl.Protocol.Abstractions;

/// <summary>
/// The <c>-z</c>/<c>--time-cond</c> condition a transfer is subject to: a timestamp and
/// the direction it is compared in.
/// </summary>
/// <param name="Value">
/// The timestamp to compare the resource's last-write time against. The command-line
/// layer has already parsed curl's accepted date spellings into this value.
/// </param>
/// <param name="Kind">Which way round the comparison runs.</param>
/// <remarks>
/// A handler compares this against the last-write time of the resource it opened — for
/// <c>file://</c>, <see cref="FileOpenResult.LastWriteTimeUtc" /> — and skips the body
/// when the condition is not met. curl treats a skipped transfer as a success, not an
/// error, so the exit code stays <see cref="CurlExitCode.Ok" />. When the resource's
/// last-write time is unknown (<see langword="null" />), the condition cannot be
/// evaluated and the body is transferred, as libcurl 8.21.0's
/// <c>Curl_meets_timecondition</c> does for an unknown document time.
/// </remarks>
public sealed record TimeCondition(DateTimeOffset Value, TimeConditionKind Kind)
{
    /// <summary>
    /// Gets the timestamp in seconds since 1970-01-01T00:00:00Z - the one stored value behind
    /// <see cref="Value" />, and the 64-bit <c>time_t</c> libcurl compares in, so a
    /// <c>-z</c> date past 9999-12-31T23:59:59Z is kept. See ADR-0410.
    /// </summary>
    public long ValueUnixSeconds { get; init; } = Value.ToUnixTimeSeconds();

    /// <summary>
    /// Gets the timestamp, the <see cref="DateTimeOffset" /> view of
    /// <see cref="ValueUnixSeconds" />, clamped to <see cref="DateTimeOffset.MaxValue" /> or
    /// <see cref="DateTimeOffset.MinValue" /> when it falls outside
    /// <see cref="DateTimeOffset" />'s range. Setting it stores its whole Unix seconds,
    /// dropping any fraction of a second as curl's <c>time_t</c> does.
    /// </summary>
    public DateTimeOffset Value
    {
        get => UnixSeconds.ToTimeClamped(ValueUnixSeconds);
        init => ValueUnixSeconds = value.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Creates a condition from a timestamp in Unix seconds, which may lie outside
    /// <see cref="DateTimeOffset" />'s range.
    /// </summary>
    /// <param name="unixSeconds">The timestamp, in seconds since 1970-01-01T00:00:00Z.</param>
    /// <param name="kind">Which way round the comparison runs.</param>
    /// <returns>A <see cref="TimeCondition" /> whose <see cref="ValueUnixSeconds" /> is <paramref name="unixSeconds" />.</returns>
    public static TimeCondition FromUnixSeconds(long unixSeconds, TimeConditionKind kind) =>
        new(DateTimeOffset.UnixEpoch, kind) { ValueUnixSeconds = unixSeconds };
}
