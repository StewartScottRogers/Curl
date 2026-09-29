using System.IO.Compression;
using Curl.Zstandard;

namespace Curl.Tls;

/// <summary>
/// The CompressedCertificate message (RFC 8879 section 4): a Certificate message body
/// compressed with one of the algorithms the client offered in <c>compress_certificate</c>,
/// sent in place of the Certificate. zlib goes through the BCL's <see cref="ZLibStream" />,
/// brotli through <see cref="BrotliDecoder" />, and zstd through
/// <see cref="ZstandardDecoder" /> (ADR-0185).
/// </summary>
/// <param name="Algorithm">The <see cref="CertificateCompressionAlgorithm" /> code point.</param>
/// <param name="UncompressedLength">The length of the Certificate message body once decompressed.</param>
/// <param name="CompressedCertificateMessage">The compressed Certificate message body.</param>
public sealed record CompressedCertificate(ushort Algorithm, int UncompressedLength, byte[] CompressedCertificateMessage)
{
    private const int Failed = -1;

    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded CompressedCertificate.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        writer.WriteUInt16(Algorithm);
        writer.WriteUInt24(UncompressedLength);
        writer.WriteOpaque(3, CompressedCertificateMessage);
        return new HandshakeMessage(HandshakeType.CompressedCertificate, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a CompressedCertificate body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The CompressedCertificate, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<CompressedCertificate> Decode(byte[] body)
    {
        TlsReader reader = new(body);
        ushort algorithm = reader.ReadUInt16();
        int uncompressedLength = reader.ReadUInt24();
        byte[] compressed = reader.ReadOpaque(3);
        return reader.Finish(new CompressedCertificate(algorithm, uncompressedLength, compressed));
    }

    /// <summary>
    /// Decompresses the Certificate message body, provided <see cref="Algorithm" /> is one
    /// of <paramref name="offeredAlgorithms" /> and the data decompresses to exactly
    /// <see cref="UncompressedLength" /> bytes (RFC 8879 section 4).
    /// </summary>
    /// <param name="offeredAlgorithms">The algorithms the client offered, each one <see cref="CertificateCompressionAlgorithm.CanDecompress" /> accepts.</param>
    /// <returns>The Certificate message body, or <see langword="null" /> for a <c>bad_certificate</c> alert.</returns>
    public byte[]? Decompress(IReadOnlyList<ushort> offeredAlgorithms)
    {
        ArgumentNullException.ThrowIfNull(offeredAlgorithms);
        if (!offeredAlgorithms.Contains(Algorithm) || !CertificateCompressionAlgorithm.CanDecompress(Algorithm))
        {
            return null;
        }

        // One byte of room past the declared length shows a stream that decompresses to more.
        byte[] buffer = new byte[UncompressedLength + 1];
        int written = DecompressInto(buffer);
        return written == UncompressedLength ? buffer[..written] : null;
    }

    private int DecompressInto(byte[] buffer) => Algorithm switch
    {
        CertificateCompressionAlgorithm.Zlib => InflateZlib(buffer),
        CertificateCompressionAlgorithm.Brotli => BrotliDecoder.TryDecompress(CompressedCertificateMessage, buffer, out int written) ? written : Failed,
        _ => ZstandardDecoder.TryDecompress(CompressedCertificateMessage, buffer, out int written) ? written : Failed,
    };

    private int InflateZlib(byte[] buffer)
    {
        using ZLibStream stream = new(new MemoryStream(CompressedCertificateMessage), CompressionMode.Decompress);
        try
        {
            return stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        }
        catch (InvalidDataException)
        {
            return Failed;
        }
    }
}
