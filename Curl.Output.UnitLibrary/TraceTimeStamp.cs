using System.Globalization;

namespace Curl.Output;

/// <summary>
/// curl's <c>--trace-time</c> stamp, <c>HH:MM:SS.uuuuuu </c> in local time, as
/// <c>tool_debug_cb</c> in curl's <c>src/tool_cb_dbg.c</c> writes it before each line that
/// starts an event, for <c>-v</c> and the trace dumps alike.
/// </summary>
internal static class TraceTimeStamp
{
    /// <summary>Reads the clock once and returns its stamp, trailing space included.</summary>
    /// <param name="timeProvider">The clock read.</param>
    /// <returns>The stamp, such as <c>03:30:30.939000 </c>.</returns>
    public static string Read(TimeProvider timeProvider)
    {
        DateTimeOffset now = timeProvider.GetLocalNow();
        long microseconds = now.Ticks / TimeSpan.TicksPerMicrosecond % 1_000_000;
        return string.Create(CultureInfo.InvariantCulture, $"{now:HH:mm:ss}.{microseconds:D6} ");
    }
}
