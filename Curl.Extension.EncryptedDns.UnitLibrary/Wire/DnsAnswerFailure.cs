namespace Curl.Extension.EncryptedDns.Wire;

/// <summary>
/// Why a DNS answer message gave no addresses.
/// </summary>
public enum DnsAnswerFailure
{
    /// <summary>The answer decoded; its addresses may still be none.</summary>
    None,

    /// <summary>The message ends inside a field, or a name has a label type RFC 1035 does not define.</summary>
    Malformed,

    /// <summary>The message's ID is not the query's, or its QR bit says it is a query (RFC 1035 section 4.1.1).</summary>
    NotTheAnswer,

    /// <summary>The TC bit is set: the server cut the answer short (RFC 1035 section 4.1.1).</summary>
    Truncated,

    /// <summary>The RCODE is not zero: the server reported an error, such as 3, the name does not exist.</summary>
    ResponseCode,
}
