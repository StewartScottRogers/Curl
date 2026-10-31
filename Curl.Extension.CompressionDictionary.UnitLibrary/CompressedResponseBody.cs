using System.IO.Compression;
using Curl.Extension.CompressionDictionary.Zstd;

namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// Decodes a response body the <c>--compression-dictionary</c> hook received from curl's handler with content decoding
/// off beside <c>--compressed</c> (BL-4351): every coding of its <c>Content-Encoding</c> fields, last applied first, the
/// dictionary codings <c>dcb</c> and <c>dcz</c> against the advertised dictionary (RFC 9842 sections 4, 5) and the four
/// curl decodes itself, <c>gzip</c> (or <c>x-gzip</c>), <c>deflate</c> (a zlib stream, or a raw deflate stream when the
/// first two bytes are no zlib header), <c>br</c> and <c>zstd</c>.
/// </summary>
internal static class CompressedResponseBody
{
    private const int ZLibMethodDeflate = 8;

    /// <summary>The codings of <paramref name="contentEncodings" />, in the order the server applied them; <c>identity</c> and <c>none</c> left out.</summary>
    public static IReadOnlyList<string> CodingsOf(IEnumerable<string> contentEncodings) =>
        [.. contentEncodings
            .SelectMany(field => field.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(coding => !coding.Equals("identity", StringComparison.OrdinalIgnoreCase) && !coding.Equals("none", StringComparison.OrdinalIgnoreCase))];

    /// <summary>Decodes <paramref name="body" />, the last of <paramref name="codings" /> first.</summary>
    /// <exception cref="InvalidDataException">A coding is not one curl or RFC 9842 decodes, or its data is corrupt.</exception>
    public static byte[] Decode(IReadOnlyList<string> codings, byte[] body, StoredDictionary dictionary, byte[] content)
    {
        for (var index = codings.Count - 1; index >= 0; index--)
        {
            body = DecodeOne(codings[index], body, dictionary, content);
        }

        return body;
    }

    private static byte[] DecodeOne(string coding, byte[] body, StoredDictionary dictionary, byte[] content)
    {
        if (DictionaryCompressedBody.CodingOf(coding) is { } encoding)
        {
            return DictionaryCompressedBody.Decode(encoding, body, dictionary, content);
        }

        return CurlCodings.TryGetValue(coding, out var decode) ? decode(body) : throw new InvalidDataException("unrecognized content encoding type " + coding);
    }

    /// <summary>The codings curl's <c>--compressed</c> decodes, by name without regard to case.</summary>
    private static readonly Dictionary<string, Func<byte[], byte[]>> CurlCodings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gzip"] = DecodeGzip,
        ["x-gzip"] = DecodeGzip,
        ["deflate"] = DecodeDeflate,
        ["br"] = body => Inflate(new BrotliStream(new MemoryStream(body), CompressionMode.Decompress)),
        ["zstd"] = DecodeZstandard,
    };

    private static byte[] DecodeGzip(byte[] body) => Inflate(new GZipStream(new MemoryStream(body), CompressionMode.Decompress));

    private static byte[] DecodeDeflate(byte[] body) =>
        Inflate(IsZLibHeader(body) ? new ZLibStream(new MemoryStream(body), CompressionMode.Decompress) : new DeflateStream(new MemoryStream(body), CompressionMode.Decompress));

    private static bool IsZLibHeader(byte[] body) =>
        body.Length >= 2 && (body[0] & 0x0F) == ZLibMethodDeflate && ((body[0] << 8) | body[1]) % 31 == 0;

    private static byte[] Inflate(Stream decompressor)
    {
        using (decompressor)
        {
            using MemoryStream decoded = new();
            decompressor.CopyTo(decoded);
            return decoded.ToArray();
        }
    }

    private static byte[] DecodeZstandard(byte[] body)
    {
        try
        {
            return DictionaryZstandardDecompressor.Decompress(body, ReadOnlyMemory<byte>.Empty);
        }
        catch (DictionaryZstandardException exception)
        {
            throw new InvalidDataException(exception.Message, exception);
        }
    }
}
