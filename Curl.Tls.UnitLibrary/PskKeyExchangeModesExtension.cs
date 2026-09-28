namespace Curl.Tls;

/// <summary>
/// The <c>psk_key_exchange_modes</c> extension (RFC 8446 section 4.2.9): the ways the
/// client will use a pre-shared key (<c>psk_ke</c> 0, <c>psk_dhe_ke</c> 1).
/// </summary>
public static class PskKeyExchangeModesExtension
{
    /// <summary>Returns a <c>psk_key_exchange_modes</c> extension listing <paramref name="modes" />.</summary>
    /// <param name="modes">The mode code points.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(byte[] modes)
    {
        TlsWriter writer = new();
        writer.WriteOpaque(1, modes);
        return new TlsExtension(TlsExtensionType.PskKeyExchangeModes, writer.ToArray());
    }

    /// <summary>Decodes <c>psk_key_exchange_modes</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The mode code points, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<byte[]> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadOpaque(1));
    }
}
