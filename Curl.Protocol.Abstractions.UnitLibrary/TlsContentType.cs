namespace Curl.Protocol.Abstractions;

/// <summary>
/// The kind of TLS protocol message a <see cref="TlsMessageEvent" /> carries: a TLS record
/// content type, or one of OpenSSL's two pseudo types for the bytes around a record
/// (<c>SSL3_RT_*</c> in OpenSSL's <c>ssl3.h</c>).
/// </summary>
/// <remarks>Any other value is a content type this enumeration does not name.</remarks>
public enum TlsContentType
{
    /// <summary>A change cipher spec message (<c>SSL3_RT_CHANGE_CIPHER_SPEC</c>, 20).</summary>
    ChangeCipherSpec = 20,

    /// <summary>An alert (<c>SSL3_RT_ALERT</c>, 21).</summary>
    Alert = 21,

    /// <summary>A handshake message (<c>SSL3_RT_HANDSHAKE</c>, 22).</summary>
    Handshake = 22,

    /// <summary>Application data (<c>SSL3_RT_APPLICATION_DATA</c>, 23).</summary>
    ApplicationData = 23,

    /// <summary>A record's five-byte header (<c>SSL3_RT_HEADER</c>, 256).</summary>
    RecordHeader = 256,

    /// <summary>
    /// The one-byte inner content type of a decrypted TLS 1.3 record
    /// (<c>SSL3_RT_INNER_CONTENT_TYPE</c>, 257).
    /// </summary>
    InnerContentType = 257,
}
