namespace Curl.Tls;

/// <summary>
/// The certificate compression algorithm code points of RFC 8879 section 3, which
/// <c>compress_certificate</c> lists and a <see cref="CompressedCertificate" /> names.
/// </summary>
public static class CertificateCompressionAlgorithm
{
    /// <summary><c>zlib</c> (RFC 1950).</summary>
    public const ushort Zlib = 1;

    /// <summary><c>brotli</c> (RFC 7932).</summary>
    public const ushort Brotli = 2;

    /// <summary><c>zstd</c> (RFC 8878).</summary>
    public const ushort Zstd = 3;

    /// <summary>Returns whether <paramref name="algorithm" /> is one the client can decompress.</summary>
    /// <param name="algorithm">A compression algorithm code point.</param>
    /// <returns><see langword="true" /> for zlib, brotli and zstd.</returns>
    public static bool CanDecompress(ushort algorithm) => algorithm is Zlib or Brotli or Zstd;
}
