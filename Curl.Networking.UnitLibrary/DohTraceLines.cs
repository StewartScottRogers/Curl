using System.Globalization;
using System.Net;
using System.Net.Sockets;

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
internal static class DohTraceLines
{
    /// <summary>Reports the lines for <paramref name="host" />'s two queries, A first.</summary>
    /// <param name="trace">Receives each line as a <see cref="ITransferEvents.ReportInfo" /> text.</param>
    /// <param name="describeExitCode">Gives curl's <c>curl_easy_strerror</c> text for an exit code.</param>
    /// <param name="host">The name asked for.</param>
    /// <param name="results">The A query's result, then the AAAA query's.</param>
    public static void Report(ITransferEvents trace, Func<CurlExitCode, string> describeExitCode, string host, IReadOnlyList<DohQueryResult> results)
    {
        var lines = new List<string>();
        lines.AddRange(results.Where(result => result.ExchangeFailure != CurlExitCode.Ok).Select(failed => $"[DNS] DoH request {describeExitCode(failed.ExchangeFailure)}"));
        lines.AddRange(results.Where(result => !result.CountsAsAnswered).Select(failed => DecodeFailureLine(host, failed)));
        if (results.Any(result => result.CountsAsAnswered))
        {
            lines.AddRange(DecodedEntryLines(host, results));
        }

        lines.ForEach(trace.ReportInfo);
    }

    /// <summary>
    /// Formats an address as curl's <c>[DoH]</c> lines do: dotted for IPv4, and for IPv6 all eight
    /// groups as four lower-case hex digits, uncompressed (<c>0000:...:0001</c> for <c>::1</c>).
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The text after <c>[DoH] A: </c> or <c>[DoH] AAAA: </c>.</returns>
    internal static string FormatAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        return string.Join(':', Enumerable.Range(0, 8).Select(group => ((bytes[2 * group] << 8) | bytes[(2 * group) + 1]).ToString("x4", CultureInfo.InvariantCulture)));
    }

    // curl keeps one entry for both queries, so the TTL is the smallest either answer read and the
    // addresses and names come A's first.
    private static IEnumerable<string> DecodedEntryLines(string host, IReadOnlyList<DohQueryResult> results)
    {
        var answers = results.Select(result => result.Answer).OfType<DnsAnswer>().ToList();
        return
        [
            $"[DNS] hostname: {host}",
            $"[DoH] TTL: {SmallestTimeToLive(answers)} seconds",
            .. results.SelectMany(AddressLines),
            .. answers.SelectMany(answer => answer.CanonicalNames).Select(name => $"CNAME: {name}"),
        ];
    }

    private static uint SmallestTimeToLive(List<DnsAnswer> answers) =>
        answers.Select(answer => answer.TimeToLiveSeconds).DefaultIfEmpty((uint)int.MaxValue).Min();

    private static IEnumerable<string> AddressLines(DohQueryResult result) =>
        result.Addresses.Select(address => $"[DoH] {TypeName(result.RecordType)}: {FormatAddress(address)}");

    private static string DecodeFailureLine(string host, DohQueryResult failed) =>
        $"[DNS] DoH: {DnsMessageFailureText.Describe(failed.Answer!.Failure)} type {TypeName(failed.RecordType)} for {host}";

    private static string TypeName(DnsRecordType recordType) => recordType == DnsRecordType.A ? "A" : "AAAA";
}
