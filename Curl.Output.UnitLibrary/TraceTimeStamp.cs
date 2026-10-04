using System.Globalization;

namespace Curl.Output;

/// <summary>
/// curl's <c>--trace-time</c> stamp, <c>HH:MM:SS.uuuuuu </c> in local time, as
/// <c>tool_debug_cb</c> in curl's <c>src/tool_cb_dbg.c</c> writes it before each line that
/// starts an event, for <c>-v</c> and the trace dumps alike.
/// </summary>
/// <remarks>
/// curl's <c>tvrealnow</c> (<c>src/tool_util.c</c>) reads <c>GetSystemTime</c> on Windows,
/// whose resolution is the millisecond, so its stamps there end in <c>000</c>; every other
/// platform reads <c>gettimeofday</c> and keeps full microseconds.
/// </remarks>
internal static class TraceTimeStamp
{
    /// <summary>
    /// Reads the clock once and returns its stamp, trailing space included, truncated to the
    /// millisecond on Windows as the running platform's curl does.
    /// </summary>
    /// <param name="timeProvider">The clock read.</param>
    /// <returns>The stamp, such as <c>03:30:30.939000 </c> on Windows.</returns>
    public static string Read(TimeProvider timeProvider)
    {
        return Read(timeProvider, truncatesToMillisecond: OperatingSystem.IsWindows());
    }

    /// <summary>Reads the clock once and returns its stamp, trailing space included.</summary>
    /// <param name="timeProvider">The clock read.</param>
    /// <param name="truncatesToMillisecond">
    /// Whether the six fractional digits are the milliseconds followed by <c>000</c>, as
    /// curl's <c>GetSystemTime</c> clock on Windows gives them, rather than the microseconds.
    /// </param>
    /// <returns>The stamp, such as <c>03:30:30.939000 </c> or <c>03:30:30.939512 </c>.</returns>
    public static string Read(TimeProvider timeProvider, bool truncatesToMillisecond)
    {
        DateTimeOffset now = timeProvider.GetLocalNow();
        long microseconds = now.Ticks / TimeSpan.TicksPerMicrosecond % 1_000_000;
        long shown = truncatesToMillisecond ? microseconds / 1000 * 1000 : microseconds;
        return string.Create(CultureInfo.InvariantCulture, $"{now:HH:mm:ss}.{shown:D6} ");
    }
}
