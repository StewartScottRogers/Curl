namespace Curl.Ntlm;

/// <summary>
/// Why an NTLM message or its target information could not be read. Every out-of-range
/// offset or length ends in one of these, never in an exception.
/// </summary>
public enum NtlmMessageFailure
{
    /// <summary>The message was read.</summary>
    None,

    /// <summary>
    /// The message is shorter than the 32 bytes curl requires of a CHALLENGE message.
    /// </summary>
    TooShort,

    /// <summary>
    /// The message does not start with <c>NTLMSSP\0</c> followed by the expected message type.
    /// </summary>
    WrongSignatureOrType,

    /// <summary>
    /// The CHALLENGE message announces target information whose offset or length lies
    /// outside the message, or whose offset points into the 48-byte fixed header.
    /// </summary>
    TargetInfoOutOfRange,

    /// <summary>An AV_PAIR's header or value runs past the end of the target information.</summary>
    AvPairTruncated,

    /// <summary>The target information ends without its <c>MsvAvEOL</c> pair.</summary>
    AvPairListUnterminated,

    /// <summary>
    /// The AUTHENTICATE message would exceed the 1024 bytes curl's <c>NTLM_BUFSIZE</c>
    /// allows; curl fails such a message with <c>CURLE_TOO_LARGE</c>.
    /// </summary>
    TooLarge,
}
