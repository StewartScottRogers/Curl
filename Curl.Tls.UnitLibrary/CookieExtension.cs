namespace Curl.Tls;

/// <summary>
/// The <c>cookie</c> extension (RFC 8446 section 4.2.2): opaque state a HelloRetryRequest
/// hands the client, which the client returns in its second ClientHello.
/// </summary>
public static class CookieExtension
{
    /// <summary>Returns a <c>cookie</c> extension carrying <paramref name="cookie" />.</summary>
    /// <param name="cookie">The cookie bytes.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(byte[] cookie)
    {
        TlsWriter writer = new();
        writer.WriteOpaque(2, cookie);
        return new TlsExtension(TlsExtensionType.Cookie, writer.ToArray());
    }

    /// <summary>Decodes <c>cookie</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The cookie bytes, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<byte[]> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadOpaque(2));
    }
}
