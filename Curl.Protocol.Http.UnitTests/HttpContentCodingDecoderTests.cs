using System.IO.Compression;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpContentCodingDecoder" /> against curl 8.21.0 (mingw, Schannel) run
/// with <c>--compressed</c> against a loopback server that sent each body below (BL-177
/// Notes). Every body is fed in 1-byte, 7-byte and whole pieces and must decode the same.
/// </summary>
[TestClass]
public sealed class HttpContentCodingDecoderTests
{
    private static readonly int[] ChunkSizes = [1, 7, 65536];

    /// <summary>
    /// Measured: <c>00 01 02 …</c> as gzip gives <c>incorrect header check</c>, and
    /// <c>1F 8B 07</c> gives <c>unknown compression method</c>. A zlib header naming method 7
    /// (<c>77 09</c>) fails the same way, as zlib reports it. Corrupt data past a good header,
    /// and corrupt Brotli (measured), give curl's generic exit 61 text (ADR-0031).
    /// </summary>
    [TestMethod]
    [DataRow("gzip", "000102030405060708090A0B", "Error while processing content unencoding: incorrect header check", DisplayName = "gzip: not a gzip or zlib header")]
    [DataRow("gzip", "1F8B07000000", "Error while processing content unencoding: unknown compression method", DisplayName = "gzip: method 7")]
    [DataRow("gzip", "7709000000", "Error while processing content unencoding: unknown compression method", DisplayName = "gzip: zlib header with method 7")]
    [DataRow("deflate", "7709000000", "Error while processing content unencoding: unknown compression method", DisplayName = "deflate: zlib header with method 7")]
    [DataRow("gzip", "1F8B080000000000000AFFFFFFFF", "Unrecognized or bad HTTP Content or Transfer-Encoding", DisplayName = "gzip: corrupt data")]
    [DataRow("deflate", "FFFFFFFF", "Unrecognized or bad HTTP Content or Transfer-Encoding", DisplayName = "deflate: corrupt raw data")]
    [DataRow("br", "FFFFFFFF", "Unrecognized or bad HTTP Content or Transfer-Encoding", DisplayName = "br: corrupt data")]
    public void Decode_CorruptBody_ThrowsExit61WithTheMeasuredMessage(string coding, string encoded, string message)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
                () => Decode(coding, HttpContentDecoderTests.Bytes(encoded), chunkSize));

            Assert.AreEqual(CurlExitCode.BadContentEncoding, thrown.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, thrown.Message, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: the first 15 bytes of the gzip body alone make curl write <c>hell</c> and
    /// exit 0; a body that ends before its stream does is not an error.
    /// </summary>
    [TestMethod]
    public void Decode_TruncatedGzip_DecodesWhatArrivedWithoutFailing()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            byte[] decoded = Decode("gzip", HttpContentDecoderTests.Bytes(HttpContentDecoderTests.Gzip)[..15], chunkSize);

            Assert.AreEqual("hell", Encoding.ASCII.GetString(decoded), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("gzip", "1F", DisplayName = "gzip: one byte")]
    [DataRow("gzip", "1F8B", DisplayName = "gzip: two bytes of gzip header")]
    [DataRow("deflate", "78", DisplayName = "deflate: one byte")]
    public void Decode_TooFewBytesToTellTheFormat_DecodesNothing(string coding, string encoded)
    {
        byte[] decoded = Decode(coding, HttpContentDecoderTests.Bytes(encoded), 1);

        Assert.IsEmpty(decoded);
    }

    [TestMethod]
    [DataRow("gzip")]
    [DataRow("deflate")]
    [DataRow("br")]
    public void Decode_LargeBody_ReturnsPiecesOfAtMostTheOutputSize(string coding)
    {
        byte[] body = [.. Enumerable.Range(0, 40000).Select(index => (byte)(index % 251))];
        HttpContentCodingDecoder decoder = new(CodingOf(coding));

        int[] sizes = [.. decoder.Decode(Encode(coding, body)).Select(piece => piece.Length)];

        CollectionAssert.AreEqual(new[] { 16384, 16384, 7232 }, sizes);
        CollectionAssert.AreEqual(body, Decode(coding, Encode(coding, body), 65536));
    }

    private static byte[] Decode(string coding, byte[] encoded, int chunkSize)
    {
        HttpContentCodingDecoder decoder = new(CodingOf(coding));
        List<byte> decoded = [];
        for (int offset = 0; offset < encoded.Length; offset += chunkSize)
        {
            foreach (ReadOnlyMemory<byte> piece in decoder.Decode(encoded.AsMemory(offset, Math.Min(chunkSize, encoded.Length - offset))))
            {
                decoded.AddRange(piece.ToArray());
            }
        }

        return [.. decoded];
    }

    private static HttpContentCoding CodingOf(string coding) => coding switch
    {
        "gzip" => HttpContentCoding.Gzip,
        "deflate" => HttpContentCoding.Deflate,
        _ => HttpContentCoding.Brotli,
    };

    private static byte[] Encode(string coding, byte[] body)
    {
        MemoryStream encoded = new();
        using (Stream compressor = coding switch
        {
            "gzip" => new GZipStream(encoded, CompressionLevel.Optimal),
            "deflate" => new ZLibStream(encoded, CompressionLevel.Optimal),
            _ => new BrotliStream(encoded, CompressionLevel.Optimal),
        })
        {
            compressor.Write(body);
        }

        return encoded.ToArray();
    }
}
