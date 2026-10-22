
namespace Curl.Networking;

/// <summary>
/// Decodes the DNS answer a DNS-over-HTTPS server returns (RFC 1035 section 4, RFC 3596) the way
/// curl 8.22.0's <c>doh_resp_decode</c> does: ID 0 and RCODE 0 are required, though RCODE 3 is
/// <see cref="DnsMessageFailure.NxDomain" />, and each answer TTL is capped at 86400 seconds
/// (<c>MAX_DNS_TTL</c>) before the smallest is taken (BL-2969); each answer record must be the type
/// asked for, a CNAME or a DNAME, of class IN; A and AAAA data are stored, CNAME and DNAME data and
/// the authority and additional sections are skipped unread, as 8.22.0's <c>doh_rdata</c> skips
/// them (BL-2970); every byte must be accounted for, and an A or AAAA answer with no address decodes as
/// <see cref="DnsMessageFailure.None" />, since 8.22.0 has no "no content" result for them. For a
/// <see cref="DnsRecordType.Srv" /> query, which DoH never sends and <see cref="DnsServerResolver" />
/// does (BL-694), SRV records are stored as <see cref="DnsAnswer.ServiceRecords" />, their targets
/// followed through compression pointers. For a <see cref="DnsRecordType.Https" /> query (BL-707),
/// the first 4 HTTPS records' data are stored undecoded as <see cref="DnsAnswer.HttpsRecordData" />,
/// as curl's <c>doh_store_https</c> does, and an answer with no answer record is
/// <see cref="DnsMessageFailure.NoContent" />, which the HTTPS trace reports as NXDOMAIN. A failed decode keeps the addresses and TTL it read before
/// it stopped, as curl's <c>struct dohentry</c> does (BL-958).
/// </summary>
public static class DnsAnswerDecoder
{
    /// <summary>Decodes <paramref name="message" /> as the answer to a query for <paramref name="askedType" />.</summary>
    /// <param name="message">The DNS message, the DoH response body.</param>
    /// <param name="askedType">The record type the query asked for, <see cref="DnsRecordType.A" />, <see cref="DnsRecordType.Aaaa" />, <see cref="DnsRecordType.Srv" /> or <see cref="DnsRecordType.Https" />.</param>
    /// <returns>The addresses and TTL, or the failure that stopped the decode.</returns>
    public static DnsAnswer Decode(ReadOnlySpan<byte> message, DnsRecordType askedType)
    {
        var reader = new DnsAnswerReader(message.ToArray(), askedType);
        var failure = reader.Read();
        var answer = new DnsAnswer(failure, reader.Addresses, reader.TimeToLiveSeconds);
        return failure == DnsMessageFailure.None
            ? answer with { ServiceRecords = reader.ServiceRecords, HttpsRecordData = reader.HttpsRecordData }
            : answer;
    }
}
