namespace Curl.Networking;

/// <summary>
/// Why a DNS message could not be encoded or decoded, one value per failure code of
/// curl 8.21.0's DoH codec (<c>DOHcode</c> in <c>lib/doh.c</c>), less the out-of-memory one.
/// <see cref="DnsMessageFailureText.Describe" /> gives the text curl's <c>--trace-config doh</c>
/// lines print for each.
/// </summary>
public enum DnsMessageFailure
{
    /// <summary>The message was encoded or decoded.</summary>
    None,

    /// <summary>A label is empty or longer than 63 bytes, or a length byte has a reserved top-bit pattern.</summary>
    BadLabel,

    /// <summary>A field runs past the end of the message.</summary>
    OutOfRange,

    /// <summary>A compressed name follows more than 128 labels or pointers, as a pointer loop does.</summary>
    LabelLoop,

    /// <summary>The message is shorter than its 12-byte header.</summary>
    TooSmall,

    /// <summary>An A record's data is not 4 bytes, or an AAAA record's is not 16.</summary>
    RdataLength,

    /// <summary>Bytes are left over after the last record.</summary>
    Malformed,

    /// <summary>The response code is not 0 (NOERROR), e.g. 2 (SERVFAIL) or 3 (NXDOMAIN).</summary>
    BadRcode,

    /// <summary>An answer record is neither the type asked for, a CNAME nor a DNAME.</summary>
    UnexpectedType,

    /// <summary>An answer record's class is not IN.</summary>
    UnexpectedClass,

    /// <summary>The answer holds no address and no CNAME.</summary>
    NoContent,

    /// <summary>The message ID is not 0, the ID every DoH query is sent with (RFC 8484 section 4.1).</summary>
    BadId,

    /// <summary>The host name makes a query longer than curl's 272-byte limit.</summary>
    NameTooLong,
}
