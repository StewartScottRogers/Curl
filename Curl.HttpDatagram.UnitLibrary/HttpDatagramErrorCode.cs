namespace Curl.HttpDatagram;

/// <summary>
/// The errors RFC 9297 and RFC 9114 give for HTTP Datagram violations.
/// </summary>
public enum HttpDatagramErrorCode
{
    /// <summary>
    /// The stream's capsules are malformed: one ended before its length said it would
    /// (RFC 9297 section 3.3), so the request or response is malformed.
    /// </summary>
    MalformedCapsule = 0,

    /// <summary>
    /// H3_DATAGRAM_ERROR (0x33): a malformed HTTP/3 datagram, or one received when the
    /// extension was not negotiated.
    /// </summary>
    DatagramError = 0x33,

    /// <summary>
    /// H3_SETTINGS_ERROR (0x0109): a <c>SETTINGS_H3_DATAGRAM</c> value other than 0 or 1.
    /// </summary>
    SettingsError = 0x0109,
}
