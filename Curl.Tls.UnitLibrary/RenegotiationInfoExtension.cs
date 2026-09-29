namespace Curl.Tls;

/// <summary>
/// The <c>renegotiation_info</c> extension (RFC 5746 section 3.2): the previous handshake's
/// verify data, empty on an initial handshake, which is the only kind Curl makes.
/// </summary>
public static class RenegotiationInfoExtension
{
    /// <summary>Returns a <c>renegotiation_info</c> extension carrying <paramref name="renegotiatedConnection" />.</summary>
    /// <param name="renegotiatedConnection">The previous verify data; empty on an initial handshake.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(byte[] renegotiatedConnection)
    {
        TlsWriter writer = new();
        writer.WriteOpaque(1, renegotiatedConnection);
        return new TlsExtension(TlsExtensionType.RenegotiationInfo, writer.ToArray());
    }

    /// <summary>Decodes <c>renegotiation_info</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The previous verify data, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<byte[]> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadOpaque(1));
    }
}
