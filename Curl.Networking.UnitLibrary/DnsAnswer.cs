using System.Net;

namespace Curl.Networking;

/// <summary>The outcome of <see cref="DnsAnswerDecoder.Decode" />.</summary>
/// <param name="Failure">
/// <see cref="DnsMessageFailure.None" /> when the answer decoded; otherwise why it did not, and
/// then <paramref name="Addresses" /> and <paramref name="CanonicalNames" /> are empty.
/// </param>
/// <param name="Addresses">The addresses of the type asked for, in answer order, at most 24.</param>
/// <param name="CanonicalNames">The CNAME targets, dotted and in answer order, at most 4.</param>
/// <param name="TimeToLiveSeconds">
/// The smallest TTL among the answer records read; <see cref="int.MaxValue" /> when there are none.
/// </param>
public sealed record DnsAnswer(
    DnsMessageFailure Failure,
    IReadOnlyList<IPAddress> Addresses,
    IReadOnlyList<string> CanonicalNames,
    uint TimeToLiveSeconds)
{
    /// <summary>
    /// Gets the SRV records of an answer to a <see cref="DnsRecordType.Srv" /> query, in answer
    /// order; empty for any other query and when <see cref="Failure" /> is not <see cref="DnsMessageFailure.None" />.
    /// </summary>
    public IReadOnlyList<DnsServiceRecord> ServiceRecords { get; init; } = [];
}
