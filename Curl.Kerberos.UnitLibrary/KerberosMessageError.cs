namespace Curl.Kerberos;

/// <summary>Why a Kerberos V5 message or one of the structures inside it could not be decoded.</summary>
public enum KerberosMessageError
{
    /// <summary>
    /// The bytes are not the structure: truncated, a length that runs past the end, a
    /// missing or out-of-order field, an integer too large for its 32-bit field, or bytes
    /// left over.
    /// </summary>
    Malformed,

    /// <summary>
    /// The bytes are a different message or structure: another application tag than the
    /// decoder reads, or a <c>msg-type</c> that disagrees with the application tag.
    /// </summary>
    UnexpectedMessage,

    /// <summary>The <c>pvno</c>, <c>tkt-vno</c> or <c>authenticator-vno</c> is not 5.</summary>
    UnsupportedVersion,
}
