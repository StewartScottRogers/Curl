namespace Curl.Tls;

/// <summary>
/// The <c>compress_certificate</c> extension (RFC 8879 section 3): the certificate
/// compression algorithms the client can decompress (zlib 1, brotli 2, zstd 3), in
/// preference order.
/// </summary>
public static class CompressCertificateExtension
{
    /// <summary>Returns a <c>compress_certificate</c> extension listing <paramref name="algorithms" />.</summary>
    /// <param name="algorithms">The compression algorithm code points, in preference order.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(IReadOnlyList<ushort> algorithms)
    {
        TlsWriter writer = new();
        writer.WriteUInt16List(1, algorithms);
        return new TlsExtension(TlsExtensionType.CompressCertificate, writer.ToArray());
    }

    /// <summary>Decodes <c>compress_certificate</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The compression algorithm code points, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<IReadOnlyList<ushort>> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadUInt16List(1));
    }
}
