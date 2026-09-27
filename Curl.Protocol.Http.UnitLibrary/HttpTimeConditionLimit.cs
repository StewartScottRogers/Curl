using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Decides whether a <c>-z</c> time is one curl 8.21.0 can write into its
/// <c>If-Modified-Since</c> or <c>If-Unmodified-Since</c> header. curl formats the header
/// with <c>Curl_gmtime</c>, which on Windows is the C runtime's <c>gmtime</c>: that refuses
/// any time after 3001-01-01 20:59:59 UTC, and curl then fails the transfer with exit 43,
/// <c>Invalid TIMEVALUE</c>, before sending a byte (measured, BL-381 Notes; ADR-0089).
/// Elsewhere <c>gmtime</c> takes every time a <see cref="TimeCondition" /> holds.
/// </summary>
internal static class HttpTimeConditionLimit
{
    /// <summary>
    /// The exit 43 message for a <c>-z</c> time the header cannot be written from.
    /// </summary>
    internal const string InvalidTimeValue = "Invalid TIMEVALUE";

    /// <summary>
    /// The last time the Windows C runtime's <c>gmtime</c> converts: its
    /// <c>_MAX__TIME64_T</c> (3001-01-01 07:59:59 UTC) plus its 13-hour
    /// <c>_MAX_LOCAL_TIME</c> allowance, Unix time 32535291599.
    /// </summary>
    internal static readonly DateTimeOffset LastWindowsTime = new(3001, 1, 1, 20, 59, 59, TimeSpan.Zero);

    /// <summary>
    /// Decides whether <paramref name="timeCondition" /> fails the transfer: it is set, the
    /// platform's curl formats with the Windows C runtime, and its time is after
    /// <see cref="LastWindowsTime" />.
    /// </summary>
    /// <param name="timeCondition">The transfer's <c>-z</c> condition, if any.</param>
    /// <param name="windowsRuntime">
    /// <see langword="true" /> when running on Windows, where the reference curl is the
    /// Schannel build linked against the Windows C runtime.
    /// </param>
    internal static bool Refuses(TimeCondition? timeCondition, bool windowsRuntime) =>
        windowsRuntime && timeCondition is not null && timeCondition.Value > LastWindowsTime;
}
