namespace Curl.Tls;

/// <summary>
/// The <c>quic_transport_parameters</c> extension (RFC 9001 section 8.2): the extension
/// data is the QUIC transport parameters as RFC 9000 section 18 encodes them, which QUIC
/// writes and reads. TLS carries the bytes unchanged.
/// </summary>
public static class QuicTransportParametersExtension
{
    /// <summary>Returns a <c>quic_transport_parameters</c> extension carrying <paramref name="transportParameters" />.</summary>
    /// <param name="transportParameters">The encoded transport parameters.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(byte[] transportParameters)
    {
        ArgumentNullException.ThrowIfNull(transportParameters);
        return new TlsExtension(TlsExtensionType.QuicTransportParameters, [.. transportParameters]);
    }

    /// <summary>Returns the encoded transport parameters a <c>quic_transport_parameters</c> extension carries.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The encoded transport parameters; any bytes are valid at this layer.</returns>
    public static TlsDecodeResult<byte[]> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadRemaining());
    }
}
