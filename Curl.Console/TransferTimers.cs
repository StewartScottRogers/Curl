using System.Globalization;
using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// The timers of a transfer's <c>-m</c> (<c>[TIMEOUT]</c>) and <c>--connect-timeout</c>
/// (<c>[CONNECTTIMEOUT]</c>) that curl 8.21.0's multi still waits on once the request is sent: each
/// given one, at its configured delay, as ADR-0357 records for volatile values, the connect timeout
/// included, which curl does not clear once connected (measured, BL-1258 Notes, ADR-0401).
/// </summary>
/// <param name="transferTimeout">The <c>-m</c> given, or <see langword="null" /> when none, or 0, was.</param>
/// <param name="connectTimeout">The <c>--connect-timeout</c> given, or <see langword="null" /> when none, or 0, was.</param>
internal sealed class TransferTimers(TimeSpan? transferTimeout, TimeSpan? connectTimeout)
{
    /// <summary>Gives the timers of the <c>-m</c> and <c>--connect-timeout</c> in <paramref name="options" />.</summary>
    /// <param name="options">The transfer's option group.</param>
    /// <returns>The transfer's timers.</returns>
    public static TransferTimers Of(CommandLineOptions options) =>
        new(Positive(options.MaxTime), Positive(options.ConnectTimeout));

    /// <summary>Gets the number of timers, which curl's <c>pollset[...]</c> lines give as <c>timeouts=</c>.</summary>
    public int Count => Pending().Count;

    /// <summary>
    /// Gets the milliseconds to the nearest timer, rounded up, which curl's <c>multi_wait</c> lines give as
    /// <c>tinternal=</c>, or <c>-1</c> with no timer (measured, BL-1188 and BL-1258 Notes).
    /// </summary>
    public string InternalTimeout =>
        Pending() is [var nearest, ..] ? Milliseconds(nearest.Remaining) : "-1";

    /// <summary>
    /// Gives the <c>[TIMER]</c> lines curl writes as it waits for the response under <c>--trace-config
    /// timer</c>: each timer's <c>expires in</c> line, nearest first, when the multi is traced too, then the
    /// nearest's <c>gives multi timeout</c> line; none with no timer.
    /// </summary>
    /// <param name="tracesExpiry">Whether the multi is traced too, so the <c>expires in</c> lines are written.</param>
    /// <returns>The lines, in curl's order.</returns>
    public IReadOnlyList<string> WaitLines(bool tracesExpiry)
    {
        var pending = Pending();
        if (pending.Count == 0)
        {
            return [];
        }

        List<string> lines = tracesExpiry
            ? [.. pending.Select(timer => $"[TIMER] [{timer.Name}] expires in {(long)timer.Remaining.TotalMicroseconds}ns")]
            : [];
        lines.Add($"[TIMER] [{pending[0].Name}] gives multi timeout in {Milliseconds(pending[0].Remaining)}ms");
        return lines;
    }

    // The given timers nearest first; with equal delays the -m's first, as curl sets it first.
    private List<(string Name, TimeSpan Remaining)> Pending()
    {
        var timers = new List<(string Name, TimeSpan Remaining)>();
        if (transferTimeout is { } transfer)
        {
            timers.Add(("TIMEOUT", transfer));
        }

        if (connectTimeout is { } connect)
        {
            timers.Add(("CONNECTTIMEOUT", connect));
        }

        return [.. timers.OrderBy(timer => timer.Remaining)];
    }

    private static string Milliseconds(TimeSpan remaining) =>
        ((long)Math.Ceiling(remaining.TotalMilliseconds)).ToString(CultureInfo.InvariantCulture);

    private static TimeSpan? Positive(TimeSpan? timeout) => timeout > TimeSpan.Zero ? timeout : null;
}
