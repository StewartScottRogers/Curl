using System.Net;

namespace Curl.Networking;

// How a DoH answer's bytes become the addresses the resolver returns.
public sealed partial class DohDnsResolver
{
    // curl resolves from its one entry only when a query counts as answered, and then every
    // address in it counts, a failed decode's too (measured, BL-958); IPv6 first.
    private static IPAddress[] ResolvedAddresses(DohQueryResult[] results) =>
        results.Any(result => result.CountsAsAnswered)
            ? [.. Enumerable.Reverse(results).SelectMany(result => result.Addresses)]
            : [];

    private static DnsAnswer DecodeAnswer(byte[] body, DnsRecordType recordType) =>
        DnsAnswerDecoder.Decode(body, recordType);
}
