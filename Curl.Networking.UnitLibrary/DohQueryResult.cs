using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>What one of <see cref="DohDnsResolver" />'s two DoH queries came back with.</summary>
/// <param name="RecordType">The type asked for: <see cref="DnsRecordType.A" /> or <see cref="DnsRecordType.Aaaa" />.</param>
/// <param name="ExchangeFailure">
/// <see cref="CurlExitCode.Ok" /> when the response was read; otherwise the exit code the connection
/// or the exchange failed with, and then <paramref name="Answer" /> is <see langword="null" />.
/// </param>
/// <param name="Answer">The decoded answer, or <see langword="null" /> when the exchange failed.</param>
internal sealed record DohQueryResult(DnsRecordType RecordType, CurlExitCode ExchangeFailure, DnsAnswer? Answer)
{
    /// <summary>Gets the addresses the query yielded; empty when the exchange or the decode failed.</summary>
    public IReadOnlyList<System.Net.IPAddress> Addresses => Answer is { Failure: DnsMessageFailure.None } decoded ? decoded.Addresses : [];

    /// <summary>
    /// Gets a value indicating whether curl counts the query as answered when it decides to print the
    /// decoded entry: a decoded answer, and also a failed exchange, whose decode curl never runs
    /// (<c>doh_resp_decode</c> is skipped, so its result stays <c>DOH_OK</c>).
    /// </summary>
    public bool CountsAsAnswered => Answer is not { Failure: not DnsMessageFailure.None };
}
