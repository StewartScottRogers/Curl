namespace Curl.Tls;

/// <summary>The <c>record_size_limit</c> extension (RFC 8449 section 4): the largest record the sender will accept.</summary>
public static class RecordSizeLimitExtension
{
    /// <summary>Returns a <c>record_size_limit</c> extension of <paramref name="limit" /> bytes.</summary>
    /// <param name="limit">The largest protected record plaintext, in bytes, the sender accepts.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(ushort limit)
    {
        TlsWriter writer = new();
        writer.WriteUInt16(limit);
        return new TlsExtension(TlsExtensionType.RecordSizeLimit, writer.ToArray());
    }

    /// <summary>Decodes <c>record_size_limit</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The limit, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<ushort> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadUInt16());
    }
}
