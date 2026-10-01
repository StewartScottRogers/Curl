namespace Curl.Protocol.Http;

/// <summary>
/// A content coding <c>--compressed</c> decodes (ADR-0287).
/// </summary>
internal enum HttpContentCoding
{
    /// <summary><c>gzip</c> or <c>x-gzip</c>: a gzip stream, or a zlib stream as curl's zlib also accepts.</summary>
    Gzip,

    /// <summary><c>deflate</c>: a zlib stream, or a raw deflate stream when the first two bytes are no zlib header.</summary>
    Deflate,

    /// <summary><c>br</c>: a Brotli stream.</summary>
    Brotli,

    /// <summary><c>zstd</c>: one or more Zstandard frames, decoded by <c>Curl.Zstandard</c> (ADR-0287).</summary>
    Zstandard,
}
