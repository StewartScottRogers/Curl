using System.Globalization;
using Curl.Api.Info;
using Curl.Engine;

namespace Curl.Console;

/// <summary>
/// What curl's debug build prints through <c>-w</c> in place of the measured values under its test
/// variables: <c>CURL_TIME</c> fixes the <c>%time{format}</c> clock at that many seconds after the
/// epoch, with <c>CURL_TIME % 1000000</c> as its microseconds (<c>src/tool_writeout.c</c>), and makes
/// <c>curl_easy_getinfo</c> answer that number for <c>%{local_port}</c>, every <c>time_*</c> variable
/// (as microseconds) and both <c>speed_*</c> variables; <c>CURL_DEBUG_SIZE</c> makes it answer that
/// number for <c>%{size_header}</c> and <c>%{size_request}</c>; and <c>CURL_VERSION</c> replaces what
/// <c>curl_version()</c> returns, which <c>%{json}</c> prints last (<c>lib/getinfo.c</c>,
/// <c>lib/version.c</c>, curl-8_22_0). Only the conformance harness turns it on (ADR-0732, BL-2909).
/// </summary>
/// <param name="FixedTime">The <c>CURL_TIME</c> number, or <see langword="null" /> when it is not set or not a number up to <see cref="MaximumFixedTime" />.</param>
/// <param name="DebugSize">The <c>CURL_DEBUG_SIZE</c> number, or <see langword="null" /> when it is not set or not a number.</param>
/// <param name="Version">The <c>CURL_VERSION</c> text, or <see langword="null" /> when it is not set.</param>
internal sealed record DebugWriteOutStandIn(long? FixedTime, long? DebugSize, string? Version)
{
    /// <summary>The variable whose number fixes the <c>-w</c> clock, local port, times and speeds.</summary>
    internal const string FixedTimeVariableName = "CURL_TIME";

    /// <summary>The variable whose number replaces the header and request sizes.</summary>
    internal const string DebugSizeVariableName = "CURL_DEBUG_SIZE";

    /// <summary>The variable whose text replaces the library version.</summary>
    internal const string VersionVariableName = "CURL_VERSION";

    /// <summary>The last second <see cref="DateTimeOffset" /> holds, 9999-12-31T23:59:59Z: a larger <c>CURL_TIME</c> is not honoured.</summary>
    internal const long MaximumFixedTime = 253402300799;

    /// <summary>
    /// Reads the three variables from <paramref name="readEnvironmentVariable" />.
    /// </summary>
    /// <param name="readEnvironmentVariable">Reads one environment variable; <see langword="null" /> when it is not set.</param>
    /// <returns>The stand-in, or <see langword="null" /> when none of the three is honoured.</returns>
    internal static DebugWriteOutStandIn? Of(Func<string, string?> readEnvironmentVariable)
    {
        DebugWriteOutStandIn standIn = new(
            NumberOf(readEnvironmentVariable(FixedTimeVariableName), MaximumFixedTime),
            NumberOf(readEnvironmentVariable(DebugSizeVariableName), long.MaxValue),
            readEnvironmentVariable(VersionVariableName));
        return standIn == None ? null : standIn;
    }

    /// <summary>The stand-in that changes nothing: what a runner given none uses.</summary>
    internal static readonly DebugWriteOutStandIn None = new(null, null, null);

    /// <summary>The stand-in a runner uses: <paramref name="standIn" />, else <see cref="None" />.</summary>
    /// <param name="standIn">The stand-in the runner was given, or <see langword="null" />.</param>
    /// <returns>The stand-in.</returns>
    internal static DebugWriteOutStandIn OrNone(DebugWriteOutStandIn? standIn) => standIn ?? None;

    /// <summary>
    /// The stand-in a run composed with <paramref name="honoursVariables" /> gets: <see cref="Of" />'s, or
    /// <see langword="null" /> when the run does not honour the variables, as curl's release build does not.
    /// </summary>
    /// <param name="honoursVariables">Whether the run honours the variables.</param>
    /// <param name="readEnvironmentVariable">Reads one environment variable; <see langword="null" /> when it is not set.</param>
    /// <returns>The stand-in, or <see langword="null" />.</returns>
    internal static DebugWriteOutStandIn? OfHonoured(bool honoursVariables, Func<string, string?> readEnvironmentVariable) =>
        honoursVariables ? Of(readEnvironmentVariable) : null;

    /// <summary>
    /// The clock <c>%time{format}</c> reads: <see cref="FixedTime" /> seconds after the epoch plus
    /// <c>FixedTime % 1000000</c> microseconds, else <paramref name="runClock" />.
    /// </summary>
    /// <param name="runClock">The run's own clock.</param>
    /// <returns>The clock.</returns>
    internal TimeProvider ClockOr(TimeProvider runClock) => FixedTime is { } seconds
        ? new FixedWallClock(DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(seconds % 1000000 * TicksPerMicrosecond))
        : runClock;

    /// <summary>The library version <c>%{json}</c> prints: <see cref="Version" />, else <paramref name="libraryVersion" />.</summary>
    /// <param name="libraryVersion">The version as measured.</param>
    /// <returns>The version.</returns>
    internal string LibraryVersionOr(string libraryVersion) => Version ?? libraryVersion;

    /// <summary>
    /// The transfer's <c>curl_easy_getinfo</c> answers as the debug build gives them under the variables.
    /// </summary>
    /// <param name="info">The measured answers.</param>
    /// <returns>The answers with the fixed values laid over them.</returns>
    internal TransferInfo Apply(TransferInfo info)
    {
        TransferInfo timed = FixedTime is { } value ? WithFixedTime(info, value) : info;
        return DebugSize is { } size ? timed with { HeaderSize = size, RequestSize = size } : timed;
    }

    /// <summary>Ticks in one microsecond.</summary>
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    private static TransferInfo WithFixedTime(TransferInfo info, long value)
    {
        TimeSpan time = TimeSpan.FromTicks(value * TicksPerMicrosecond);
        return info with
        {
            LocalPort = value,
            TotalTime = time,
            NameLookupTime = time,
            ConnectTime = time,
            AppConnectTime = time,
            PreTransferTime = time,
            PostTransferTime = time,
            StartTransferTime = time,
            RedirectTime = time,
            QueueTime = time,
            SpeedDownload = value,
            SpeedUpload = value,
        };
    }

    private static long? NumberOf(string? text, long maximum) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long number) && number <= maximum ? number : null;
}
