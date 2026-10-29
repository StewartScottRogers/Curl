using System.Net;

namespace Curl.Extension.EncryptedDns.Wire;

/// <summary>
/// What <see cref="DnsAnswerDecoder" /> read from a DNS answer message.
/// </summary>
/// <param name="Addresses">The addresses of the type asked for, in the order the answer section lists them.</param>
/// <param name="Failure">Why there are no addresses, or <see cref="DnsAnswerFailure.None" />.</param>
/// <param name="ResponseCode">The header's RCODE (RFC 1035 section 4.1.1), or zero when the header was not read.</param>
public sealed record DnsAnswer(IReadOnlyList<IPAddress> Addresses, DnsAnswerFailure Failure, int ResponseCode)
{
    /// <summary>
    /// Creates an answer that failed for <paramref name="failure" />.
    /// </summary>
    /// <param name="failure">Why the answer gave no addresses.</param>
    /// <param name="responseCode">The header's RCODE.</param>
    /// <returns>An answer with no addresses.</returns>
    public static DnsAnswer Failed(DnsAnswerFailure failure, int responseCode) => new([], failure, responseCode);
}
