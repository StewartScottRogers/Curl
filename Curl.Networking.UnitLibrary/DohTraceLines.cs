using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

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
    private const string HttpsRequestCompleteLine = "[DoH] DoH request complete, 0 to go";

    private const ushort MandatoryKey = 0;

    // The ALPN identifiers curl's Curl_alpn2alpnid knows; it keeps no other.
    private static readonly string[] KnownApplicationProtocols = ["http/1.1", "h2", "h3"];

    // The CURLcode curl's doh_resp_decode_httpsrr and Curl_httpsrr_set return for each
    // ServiceBindingFailure, by value (None is never looked up): a target name that does not read
    // leaves the function's initial CURLE_OUT_OF_MEMORY.
    private static readonly CurlExitCode[] HttpsRecordExitCodes =
    [
        CurlExitCode.Ok,
        CurlExitCode.BadFunctionArgument,
        CurlExitCode.OutOfMemory,
        CurlExitCode.WeirdServerReply,
        CurlExitCode.WeirdServerReply,
        CurlExitCode.WeirdServerReply,
        CurlExitCode.BadContentEncoding,
        CurlExitCode.BadFunctionArgument,
    ];

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
    /// Reports the lines curl 8.22.0's HTTPSRR build writes once a name's HTTPS (type 65) DoH query has
    /// finished, derived from <c>doh_probe_done</c> and <c>doh_resp_decode_httpsrr</c> in
    /// <c>lib/vdns/doh.c</c> and <c>Curl_httpsrr_trace</c> in <c>lib/vdns/httpsrr.c</c> at tag
    /// <c>curl-8_22_0</c> (ADR-0727's BL-2954 amendment): <c>[DoH] [DoH] [HTTPS] error: &lt;error&gt;</c>
    /// for a query whose connection or exchange failed, <c>[DoH] [DoH] [HTTPS] error decoding response:
    /// &lt;failure&gt;</c> for an answer that did not decode (one holding no record reads
    /// <c>No such name</c>), and otherwise <c>[DNS] [HTTPS] got N records</c> when it holds any, a
    /// <c>[DNS] [HTTPS] record: </c> line for each parameter of the first record as it is read,
    /// <c>[DoH] [DoH] error decoding HTTPS RR: &lt;error&gt;</c> when that record does not decode, and
    /// <c>[DoH] DoH request complete, 0 to go</c> when it does, the HTTPS query being a resolve of its own.
    /// </summary>
    /// <param name="trace">Receives each line as a <see cref="ITransferEvents.ReportInfo" /> text.</param>
    /// <param name="describeExitCode">Gives curl's <c>curl_easy_strerror</c> text for an exit code.</param>
    /// <param name="result">The HTTPS query's result.</param>
    public static void ReportHttpsRecord(ITransferEvents trace, Func<CurlExitCode, string> describeExitCode, DohQueryResult result) =>
        HttpsRecordLines(describeExitCode, result).ForEach(trace.ReportInfo);

    /// <summary>
    /// Writes an HTTPS record as curl's <c>Curl_httpsrr_print</c> does: the priority, the target
    /// name with its final dot (<c>.</c> for none), <c>mandatory-keys(ignored)</c>, the
    /// <c>alpn=</c> identifiers curl knows (<c>http/1.1</c>, <c>h2</c>, <c>h3</c>, each once, in the
    /// record's order), <c>port=</c>, <c>no-default-alpn</c>, <c>ipv6hint=</c>, <c>ipv4hint=</c> and
    /// <c>ech=&lt;N bytes&gt;</c>, each only when the record has it.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="hasMandatoryKeys">Whether the record had a <c>mandatory</c> (key 0) parameter.</param>
    /// <returns>The text after <c>[DNS] [HTTPS] record: </c>.</returns>
    internal static string FormatHttpsRecord(ServiceBindingRecord record, bool hasMandatoryKeys)
    {
        var known = record.ApplicationProtocols.Where(KnownApplicationProtocols.Contains).Distinct().ToList();
        var text = new StringBuilder(FormattableString.Invariant($"{record.Priority} {(record.TargetName == "." ? "." : record.TargetName + ".")}"));
        text.Append(hasMandatoryKeys ? " mandatory-keys(ignored)" : string.Empty)
            .Append(known.Count > 0 ? " alpn=" + string.Join(',', known) : string.Empty);
        AppendHttpsRecordEndpointParameters(text, record);
        return text.ToString();
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

    private static List<string> HttpsRecordLines(Func<CurlExitCode, string> describeExitCode, DohQueryResult result)
    {
        if (result.ExchangeFailure != CurlExitCode.Ok)
        {
            return [$"[DoH] [DoH] [HTTPS] error: {describeExitCode(result.ExchangeFailure)}"];
        }

        // curl turns an HTTPS answer with no content into NXDOMAIN before it writes the failure.
        var answer = result.Answer!;
        if (answer.Failure != DnsMessageFailure.None)
        {
            var failure = answer.Failure == DnsMessageFailure.NoContent ? DnsMessageFailure.NxDomain : answer.Failure;
            return [$"[DoH] [DoH] [HTTPS] error decoding response: {DnsMessageFailureText.Describe(failure)}"];
        }

        return answer.HttpsRecordData is [var first, ..]
            ? [FormattableString.Invariant($"[DNS] [HTTPS] got {answer.HttpsRecordData.Count} records"), .. FirstHttpsRecordLines(describeExitCode, first)]
            : [HttpsRequestCompleteLine];
    }

    // curl writes the record so far after each parameter it reads, and stops at the first it refuses.
    private static List<string> FirstHttpsRecordLines(Func<CurlExitCode, string> describeExitCode, byte[] record)
    {
        var lines = new List<string>();
        var hasMandatoryKeys = false;
        foreach (var (key, end) in HttpsRecordParameterEnds(record))
        {
            if (ServiceBindingRecordDecoder.Decode(record.AsSpan(0, end)).Record is not { } readSoFar)
            {
                break;
            }

            hasMandatoryKeys |= key == MandatoryKey;
            lines.Add("[DNS] [HTTPS] record: " + FormatHttpsRecord(readSoFar, hasMandatoryKeys));
        }

        var failure = ServiceBindingRecordDecoder.Decode(record).Failure;
        lines.Add(failure == ServiceBindingFailure.None
            ? HttpsRequestCompleteLine
            : $"[DoH] [DoH] error decoding HTTPS RR: {describeExitCode(HttpsRecordExitCode(failure))}");
        return lines;
    }

    // Each parameter's key and the offset just past its value, for the parameters whose header and
    // value lie inside the record after a target name that reads; none when the name does not.
    private static IEnumerable<(ushort Key, int End)> HttpsRecordParameterEnds(byte[] record)
    {
        var position = 2;
        var length = -1;
        while (length != 0 && position < record.Length)
        {
            length = record[position];
            position += length + 1;
        }

        while (length == 0 && record.Length - position >= 4)
        {
            var key = BinaryPrimitives.ReadUInt16BigEndian(record.AsSpan(position));
            position += 4 + BinaryPrimitives.ReadUInt16BigEndian(record.AsSpan(position + 2));
            yield return (key, Math.Min(position, record.Length));
        }
    }

    // The port, no-default-alpn, hints and ech parts of Curl_httpsrr_print, in its order.
    private static void AppendHttpsRecordEndpointParameters(StringBuilder text, ServiceBindingRecord record) =>
        text.Append(record.Port is { } port ? FormattableString.Invariant($" port={port}") : string.Empty)
            .Append(record.NoDefaultApplicationProtocol ? " no-default-alpn" : string.Empty)
            .Append(record.IPv6Hints.Count > 0 ? " ipv6hint=" + string.Join(',', record.IPv6Hints) : string.Empty)
            .Append(record.IPv4Hints.Count > 0 ? " ipv4hint=" + string.Join(',', record.IPv4Hints) : string.Empty)
            .Append(record.EchConfigList.IsEmpty ? string.Empty : FormattableString.Invariant($" ech=<{record.EchConfigList.Length} bytes>"));

    private static CurlExitCode HttpsRecordExitCode(ServiceBindingFailure failure) => HttpsRecordExitCodes[(int)failure];
}
