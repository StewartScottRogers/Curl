using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The line curl 8.22.0 prints under <c>-v --trace-config doh</c> for an A or AAAA DoH query that
/// failed, in place of its <c>DoH request complete, N to go</c> line (measured with
/// <c>Record-CurlExchange.ps1 -DohPort</c> and <c>-Tls</c>, BL-2970, ADR-0738):
/// <c>[DoH] [DoH] [A] error: &lt;curl_easy_strerror&gt;</c> for a query whose connection or
/// exchange failed, <c>[DoH] [DoH] [A] error: HTTP status N</c> for a response whose status is
/// outside 200-299 (BL-2972), and <c>[DoH] [DoH] [A] error decoding response: &lt;doh_strerror&gt;</c> for an
/// answer that did not decode, <c>[AAAA]</c> for the AAAA query. A decoded answer, with or without an
/// address, has no line here: 8.22.0 no longer prints the decoded entry.
/// </summary>
/// <remarks>
/// The class is split by what its lines report: this file the A and AAAA queries', the
/// <c>AnswerDecoding</c> file the decode failures, and the <c>HttpsRecord</c> file the HTTPS query's.
/// </remarks>
internal static partial class DohTraceLines
{
    /// <summary>Gives the failure line for <paramref name="result" />, or <see langword="null" /> when the query succeeded.</summary>
    /// <param name="describeExitCode">Gives curl's <c>curl_easy_strerror</c> text for an exit code.</param>
    /// <param name="result">The query's result.</param>
    /// <returns>The line, without curl's <c>* </c>.</returns>
    public static string? FailureLine(Func<CurlExitCode, string> describeExitCode, DohQueryResult result)
    {
        var probe = $"[DoH] [DoH] [{TypeName(result.RecordType)}] error";
        if (result.Answer is not { } answer)
        {
            return ExchangeFailureLine(describeExitCode, probe, result);
        }

        return DecodeFailureLine(probe, answer);
    }

    private static string TypeName(DnsRecordType recordType) => recordType == DnsRecordType.A ? "A" : "AAAA";
}
