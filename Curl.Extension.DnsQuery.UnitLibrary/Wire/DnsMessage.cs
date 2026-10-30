namespace Curl.Extension.DnsQuery.Wire;

/// <summary>A decoded DNS message (RFC 1035 section 4.1).</summary>
/// <param name="Header">The identifier and flags.</param>
/// <param name="Questions">The question section.</param>
/// <param name="Answers">The answer section.</param>
/// <param name="Authority">The authority section.</param>
/// <param name="Additional">The additional section.</param>
internal sealed record DnsMessage(
    DnsHeader Header,
    IReadOnlyList<DnsQuestion> Questions,
    IReadOnlyList<DnsResourceRecord> Answers,
    IReadOnlyList<DnsResourceRecord> Authority,
    IReadOnlyList<DnsResourceRecord> Additional);
