namespace Curl.Tls;

/// <summary>
/// The <c>early_data</c> extension (RFC 8446 section 4.2.10): empty in a ClientHello and in
/// EncryptedExtensions, and the maximum early data size in a NewSessionTicket.
/// </summary>
public static class EarlyDataExtension
{
    /// <summary>Returns the empty <c>early_data</c> of a ClientHello or EncryptedExtensions.</summary>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeIndication() => new(TlsExtensionType.EarlyData, []);

    /// <summary>Checks the <c>early_data</c> data of a ClientHello or EncryptedExtensions, which must be empty.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns><see langword="null" /> when the data is empty; otherwise <see cref="TlsAlertDescription.DecodeError" />.</returns>
    public static TlsAlertDescription? DecodeIndication(byte[] data) => new TlsReader(data).Finish(true).Alert;

    /// <summary>Returns a NewSessionTicket's <c>early_data</c> allowing <paramref name="maxEarlyDataSize" /> bytes.</summary>
    /// <param name="maxEarlyDataSize">The most early data, in bytes, the client may send with the ticket.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeMaxEarlyDataSize(uint maxEarlyDataSize)
    {
        TlsWriter writer = new();
        writer.WriteUInt32(maxEarlyDataSize);
        return new TlsExtension(TlsExtensionType.EarlyData, writer.ToArray());
    }

    /// <summary>Decodes a NewSessionTicket's <c>early_data</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The maximum early data size, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<uint> DecodeMaxEarlyDataSize(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadUInt32());
    }
}
