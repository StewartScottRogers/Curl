namespace Curl.Protocol.Abstractions;

/// <summary>
/// Converts between a time in Unix seconds, the 64-bit <c>time_t</c> curl keeps every file
/// time and <c>-z</c>/<c>--time-cond</c> time in, and the <see cref="DateTimeOffset" />
/// view of it, whose range ends at 9999-12-31T23:59:59Z. See ADR-0410.
/// </summary>
internal static class UnixSeconds
{
    /// <summary>The Unix seconds of <see cref="DateTimeOffset.MinValue" />, 0001-01-01T00:00:00Z.</summary>
    internal const long MinDateTimeOffsetSeconds = -62_135_596_800;

    /// <summary>The Unix seconds of 9999-12-31T23:59:59Z, the last whole second <see cref="DateTimeOffset" /> holds.</summary>
    internal const long MaxDateTimeOffsetSeconds = 253_402_300_799;

    /// <summary>
    /// Gets the time <paramref name="seconds" /> names, or <see langword="null" /> when it is
    /// unknown or falls outside <see cref="DateTimeOffset" />'s range.
    /// </summary>
    /// <param name="seconds">Seconds since 1970-01-01T00:00:00Z, or <see langword="null" />.</param>
    /// <returns>The time in UTC, or <see langword="null" />.</returns>
    internal static DateTimeOffset? ToTimeInRange(long? seconds) =>
        seconds is >= MinDateTimeOffsetSeconds and <= MaxDateTimeOffsetSeconds
            ? DateTimeOffset.FromUnixTimeSeconds(seconds.Value)
            : null;

    /// <summary>
    /// Gets the time <paramref name="seconds" /> names, clamped to
    /// <see cref="DateTimeOffset" />'s range: <see cref="DateTimeOffset.MaxValue" /> for a later
    /// time and <see cref="DateTimeOffset.MinValue" /> for an earlier one.
    /// </summary>
    /// <param name="seconds">Seconds since 1970-01-01T00:00:00Z.</param>
    /// <returns>The time in UTC, clamped to <see cref="DateTimeOffset" />'s range.</returns>
    internal static DateTimeOffset ToTimeClamped(long seconds) =>
        ToTimeInRange(seconds)
        ?? (seconds > MaxDateTimeOffsetSeconds ? DateTimeOffset.MaxValue : DateTimeOffset.MinValue);
}
