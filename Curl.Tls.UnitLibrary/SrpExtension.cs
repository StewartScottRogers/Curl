namespace Curl.Tls;

/// <summary>
/// The <c>srp</c> extension (RFC 5054 section 2.8.1): the client's SRP user name
/// (<c>srp_I</c>) as UTF-8 bytes.
/// </summary>
public static class SrpExtension
{
    /// <summary>Returns an <c>srp</c> extension carrying <paramref name="identity" />.</summary>
    /// <param name="identity">The user name bytes.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(byte[] identity)
    {
        TlsWriter writer = new();
        writer.WriteOpaque(1, identity);
        return new TlsExtension(TlsExtensionType.Srp, writer.ToArray());
    }

    /// <summary>Decodes <c>srp</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The user name bytes, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<byte[]> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadOpaque(1));
    }
}
