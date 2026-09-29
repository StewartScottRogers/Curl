namespace Curl.Authentication;

/// <summary>Why a SPNEGO token could not be decoded.</summary>
internal enum SpnegoTokenError
{
    /// <summary>
    /// The bytes are not the structure: truncated, a length that runs past the end, a field
    /// out of order or of the wrong type, a <c>negState</c> RFC 4178 does not define, or
    /// bytes left over.
    /// </summary>
    Malformed,

    /// <summary>The bytes are another token: anything but a NegTokenResp (<c>[1]</c>) where one is read.</summary>
    UnexpectedToken,
}
