using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Writes the lines curl 8.21.0 prints under <c>-v --trace-config doh</c> once both of a name's DoH
/// queries have finished (measured with <c>Record-CurlExchange.ps1 -Tls</c>, BL-850): a
/// <c>[DNS] DoH request &lt;error&gt;</c> line for each query whose connection or exchange failed, a
/// <c>[DNS] DoH: &lt;failure&gt; type A|AAAA for &lt;host&gt;</c> line for each answer that did not
/// decode, and, when at least one query decoded or failed to exchange, the decoded entry:
/// <c>[DNS] hostname: &lt;host&gt;</c>, <c>[DoH] TTL: N seconds</c>, a <c>[DoH] A:</c> or
/// <c>[DoH] AAAA:</c> line per address, A first, and a <c>CNAME:</c> line per canonical name.
/// </summary>
/// <remarks>
/// The class is split by what its lines report: this file orders the A and AAAA queries' lines, the
/// <c>ExchangeFailures</c> file writes the failed connections and exchanges, the <c>AnswerDecoding</c>
/// file the decode failures and the decoded entry, and the <c>HttpsRecord</c> file the HTTPS query's.
/// </remarks>
internal static partial class DohTraceLines
{
    /// <summary>Reports the lines for <paramref name="host" />'s two queries, A first.</summary>
    /// <param name="trace">Receives each line as a <see cref="ITransferEvents.ReportInfo" /> text.</param>
    /// <param name="describeExitCode">Gives curl's <c>curl_easy_strerror</c> text for an exit code.</param>
    /// <param name="host">The name asked for.</param>
    /// <param name="results">The A query's result, then the AAAA query's.</param>
    public static void Report(ITransferEvents trace, Func<CurlExitCode, string> describeExitCode, string host, IReadOnlyList<DohQueryResult> results)
    {
        var lines = new List<string>();
        lines.AddRange(ExchangeFailureLines(describeExitCode, results));
        lines.AddRange(results.Where(result => !result.CountsAsAnswered).Select(failed => DecodeFailureLine(host, failed)));
        if (results.Any(result => result.CountsAsAnswered))
        {
            lines.AddRange(DecodedEntryLines(host, results));
        }

        lines.ForEach(trace.ReportInfo);
    }
}
