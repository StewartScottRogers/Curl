namespace Curl.Tls;

/// <summary>
/// The <c>post_handshake_auth</c> extension (RFC 8446 section 4.2.6): empty, the client
/// offers to authenticate with a certificate after the handshake.
/// </summary>
public static class PostHandshakeAuthExtension
{
    /// <summary>Returns the empty <c>post_handshake_auth</c> extension.</summary>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode() => new(TlsExtensionType.PostHandshakeAuth, []);

    /// <summary>Decodes <c>post_handshake_auth</c> data, which must be empty.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns><see langword="null" />, or <see cref="TlsAlertDescription.DecodeError" /> when the data is not empty.</returns>
    public static TlsAlertDescription? Decode(byte[] data) => new TlsReader(data).Finish(true).Alert;
}
