namespace Curl.Extension.Dane.Wire;

/// <summary>
/// A decoded DNS response: the header fields DNSSEC needs (RFC 1035 section 4.1.1, RFC 4035 section 3.2) and the
/// answer and authority sections.
/// </summary>
/// <param name="Id">The message ID, for the caller to match against its query's.</param>
/// <param name="IsTruncated">Whether TC is set: the server cut the response short.</param>
/// <param name="IsAuthenticData">Whether AD is set: the resolver says it validated every record (RFC 4035 section 3.2.3).</param>
/// <param name="IsCheckingDisabled">Whether CD is set, copied from the query (RFC 4035 section 3.2.2).</param>
/// <param name="ResponseCode">The header's 4-bit RCODE, such as 0 no error, 2 server failure or 3 no such name.</param>
/// <param name="Answers">The answer section's records.</param>
/// <param name="Authority">The authority section's records.</param>
public sealed record DnssecResponse(
    ushort Id,
    bool IsTruncated,
    bool IsAuthenticData,
    bool IsCheckingDisabled,
    int ResponseCode,
    IReadOnlyList<DnsResourceRecord> Answers,
    IReadOnlyList<DnsResourceRecord> Authority);
