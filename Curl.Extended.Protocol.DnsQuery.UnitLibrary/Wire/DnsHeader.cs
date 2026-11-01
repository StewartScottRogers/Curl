namespace Curl.Extended.Protocol.DnsQuery.Wire;

/// <summary>A DNS message's identifier and flags word (RFC 1035 section 4.1.1, RFC 4035 section 3.2 for AD and CD).</summary>
/// <param name="Id">The 16-bit identifier that pairs a response with its query.</param>
/// <param name="Flags">The second 16-bit word: QR, Opcode, AA, TC, RD, RA, Z, AD, CD and RCODE.</param>
internal sealed record DnsHeader(ushort Id, ushort Flags)
{
    /// <summary>The QR bit: set in a response.</summary>
    public const ushort ResponseFlag = 0x8000;

    /// <summary>The AA bit: the answer comes from an authority for the name.</summary>
    public const ushort AuthoritativeAnswerFlag = 0x0400;

    /// <summary>The TC bit: the message was truncated.</summary>
    public const ushort TruncatedFlag = 0x0200;

    /// <summary>The RD bit: the query asks for recursion.</summary>
    public const ushort RecursionDesiredFlag = 0x0100;

    /// <summary>The RA bit: the server offers recursion.</summary>
    public const ushort RecursionAvailableFlag = 0x0080;

    /// <summary>The AD bit: the data is authenticated (RFC 4035).</summary>
    public const ushort AuthenticDataFlag = 0x0020;

    /// <summary>The CD bit: checking is disabled (RFC 4035).</summary>
    public const ushort CheckingDisabledFlag = 0x0010;

    private static readonly (ushort Flag, string Name)[] FlagNames =
    [
        (ResponseFlag, "qr"), (AuthoritativeAnswerFlag, "aa"), (TruncatedFlag, "tc"), (RecursionDesiredFlag, "rd"),
        (RecursionAvailableFlag, "ra"), (AuthenticDataFlag, "ad"), (CheckingDisabledFlag, "cd"),
    ];

    /// <summary>Gives the lower-case names of the flag bits that are set, in dig's order: qr aa tc rd ra ad cd.</summary>
    /// <returns>The names.</returns>
    public IReadOnlyList<string> SetFlagNames() => [.. FlagNames.Where(entry => HasFlag(entry.Flag)).Select(entry => entry.Name)];

    /// <summary>Gets the four-bit kind of query.</summary>
    public int Opcode => (Flags >> 11) & 0x0F;

    /// <summary>Gets the four-bit response code.</summary>
    public int ResponseCode => Flags & 0x0F;

    /// <summary>Tells whether a flag bit is set.</summary>
    /// <param name="flag">One of this type's flag constants.</param>
    /// <returns><see langword="true" /> when the bit is set.</returns>
    public bool HasFlag(ushort flag) => (Flags & flag) != 0;
}
