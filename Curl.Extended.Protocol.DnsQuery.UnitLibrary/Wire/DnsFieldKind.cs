namespace Curl.Extended.Protocol.DnsQuery.Wire;

/// <summary>How a field of a record's RDATA is presented.</summary>
internal enum DnsFieldKind
{
    /// <summary>An unsigned integer, written in decimal.</summary>
    Number,

    /// <summary>A name, address, tag or RFC 3597 <c>\#</c> form, written as it stands.</summary>
    Text,

    /// <summary>One or more character-strings (RFC 1035 section 3.3), each written in double quotes.</summary>
    CharacterStrings,
}
