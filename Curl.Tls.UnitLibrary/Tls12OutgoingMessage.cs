namespace Curl.Tls;

/// <summary>
/// One message <see cref="Tls12ClientHandshake" /> asks its caller to send as a record of
/// <paramref name="ContentType" />: a handshake message, or the one-byte ChangeCipherSpec
/// after which every record the client writes is protected with
/// <see cref="Tls12ClientHandshake.KeyBlock" />'s client keys.
/// </summary>
/// <param name="ContentType"><see cref="TlsContentType.Handshake" /> or <see cref="TlsContentType.ChangeCipherSpec" />.</param>
/// <param name="Bytes">The record content: a handshake message with its header, or the ChangeCipherSpec byte.</param>
public sealed record Tls12OutgoingMessage(TlsContentType ContentType, byte[] Bytes)
{
    /// <summary>Gets the ChangeCipherSpec message (RFC 5246 section 7.1): the single byte 1.</summary>
    public static Tls12OutgoingMessage ChangeCipherSpec { get; } = new(TlsContentType.ChangeCipherSpec, [1]);
}
