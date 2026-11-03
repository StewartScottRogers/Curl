namespace Curl.Conformance.Extended.DnsQuery.Reference;

/// <summary>
/// One reply a case gives a <see cref="DnsReferenceServer" />: the records of each section, and what it overrides of
/// the header or of the whole message. <see cref="DnsReferenceMessageWriter.WriteReply" /> lays it out.
/// </summary>
public sealed record DnsReferenceReply
{
    /// <summary>Gets the answer section.</summary>
    public IReadOnlyList<DnsReferenceRecord> Answers { get; init; } = [];

    /// <summary>Gets the authority section.</summary>
    public IReadOnlyList<DnsReferenceRecord> Authority { get; init; } = [];

    /// <summary>Gets the additional section.</summary>
    public IReadOnlyList<DnsReferenceRecord> Additional { get; init; } = [];

    /// <summary>Gets the header's ID; <see langword="null" /> copies the query's (RFC 1035 section 4.1.1).</summary>
    public ushort? Id { get; init; }

    /// <summary>
    /// Gets the header's second 16 bits (QR, Opcode, AA, TC, RD, RA, Z, RCODE), which set TC and RCODE too;
    /// <see langword="null" /> gives QR and RA, the query's RD, and RCODE 0.
    /// </summary>
    public ushort? Flags { get; init; }

    /// <summary>Gets the whole message to send instead of one written from the other properties; <see langword="null" /> writes it.</summary>
    public byte[]? Raw { get; init; }
}
