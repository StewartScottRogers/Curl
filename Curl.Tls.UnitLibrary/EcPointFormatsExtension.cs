namespace Curl.Tls;

/// <summary>
/// The <c>ec_point_formats</c> extension (RFC 8422 section 5.1.2): the elliptic curve point
/// formats the sender can parse, <c>uncompressed</c> (0) first.
/// </summary>
public static class EcPointFormatsExtension
{
    /// <summary>Returns an <c>ec_point_formats</c> extension listing <paramref name="formats" />.</summary>
    /// <param name="formats">The point format code points, in preference order.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(byte[] formats)
    {
        TlsWriter writer = new();
        writer.WriteOpaque(1, formats);
        return new TlsExtension(TlsExtensionType.EcPointFormats, writer.ToArray());
    }

    /// <summary>Decodes <c>ec_point_formats</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The point format code points, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<byte[]> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadOpaque(1));
    }
}
