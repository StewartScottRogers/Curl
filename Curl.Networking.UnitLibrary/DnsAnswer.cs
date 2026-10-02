using System.Net;

namespace Curl.Networking;

/// <summary>The outcome of <see cref="DnsAnswerDecoder.Decode" />.</summary>
/// <param name="Failure">
/// <see cref="DnsMessageFailure.None" /> when the answer decoded; otherwise why it did not, and
/// then <paramref name="Addresses" /> and <paramref name="CanonicalNames" /> hold what the decode
/// read before it stopped, as curl's DoH entry keeps them (BL-958).
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

    /// <summary>
    /// Gets the record data of each HTTPS record of an answer to a <see cref="DnsRecordType.Https" />
    /// query, in answer order and at most 4, undecoded as curl's <c>doh_store_https</c> keeps them;
    /// <see cref="ServiceBindingRecordDecoder" /> decodes one. Empty for any other query and when
    /// <see cref="Failure" /> is not <see cref="DnsMessageFailure.None" />.
    /// </summary>
    public IReadOnlyList<byte[]> HttpsRecordData { get; init; } = [];
}
