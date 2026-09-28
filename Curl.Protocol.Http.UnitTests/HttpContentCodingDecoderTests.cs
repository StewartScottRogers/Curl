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
    /// <summary><c>world</c> as gzip, as sent to curl after <c>hello</c> as a second member.</summary>
    private const string GzipWorld = "1F8B08000000000004002BCF2FCA4901004311773A05000000";

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

    /// <summary>
    /// Measured (BL-281 Notes): with <c>41 42</c> after the end of a gzip, zlib or Brotli
    /// stream, or a second complete gzip member after the first, curl writes <c>hello</c>
    /// and exits 23 with <c>Failed writing received data to disk/application</c>.
    /// </summary>
    [TestMethod]
    [DataRow("gzip", HttpContentDecoderTests.Gzip + "4142", DisplayName = "gzip, then 41 42")]
    [DataRow("gzip", HttpContentDecoderTests.Gzip + "1F", DisplayName = "gzip, then 1F")]
    [DataRow("gzip", HttpContentDecoderTests.Gzip + GzipWorld, DisplayName = "gzip, then a second gzip member")]
    [DataRow("gzip", HttpContentDecoderTests.ZLib + "4142", DisplayName = "gzip label on a zlib stream, then 41 42")]
    [DataRow("deflate", HttpContentDecoderTests.ZLib + "4142", DisplayName = "zlib, then 41 42")]
    [DataRow("br", HttpContentDecoderTests.Brotli + "4142", DisplayName = "br, then 41 42")]
    public void Decode_BytesAfterTheEndOfTheStream_DecodesTheStreamThenThrowsExit23(string coding, string encoded)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            List<byte> decoded = [];

            HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
                () => DecodeInto(decoded, coding, HttpContentDecoderTests.Bytes(encoded), chunkSize));

            Assert.AreEqual("hello", Encoding.ASCII.GetString([.. decoded]), $"Chunk size {chunkSize}");
            Assert.AreEqual(CurlExitCode.WriteError, thrown.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Failed writing received data to disk/application", thrown.Message, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured (BL-281 Notes): raw deflate with <c>41 42</c> after its end writes
    /// <c>hello</c> and exits 0; curl drops what follows a raw deflate stream.
    /// </summary>
    [TestMethod]
    public void Decode_BytesAfterTheEndOfARawDeflateStream_AreDropped()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            byte[] decoded = Decode("deflate", HttpContentDecoderTests.Bytes(HttpContentDecoderTests.RawDeflate + "4142"), chunkSize);

            Assert.AreEqual("hello", Encoding.ASCII.GetString(decoded), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("gzip")]
    [DataRow("deflate")]
    [DataRow("br")]
    public void Decode_LargeBodyThenOneByte_ThrowsExit23AfterTheWholeBody(string coding)
    {
        byte[] body = [.. Enumerable.Range(0, 100000).Select(index => (byte)(index * index % 251))];
        foreach (int chunkSize in ChunkSizes)
        {
            List<byte> decoded = [];

            HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
                () => DecodeInto(decoded, coding, [.. Encode(coding, body), 0x41], chunkSize));

            CollectionAssert.AreEqual(body, decoded, $"Chunk size {chunkSize}");
            Assert.AreEqual(CurlExitCode.WriteError, thrown.ExitCode, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("gzip")]
    [DataRow("deflate")]
    [DataRow("br")]
    public void Decode_LargeBodyThatEndsWithItsStream_DecodesWithoutFailing(string coding)
    {
        byte[] body = [.. Enumerable.Range(0, 100000).Select(index => (byte)(index * index % 251))];
        foreach (int chunkSize in ChunkSizes)
        {
            CollectionAssert.AreEqual(body, Decode(coding, Encode(coding, body), chunkSize), $"Chunk size {chunkSize}");
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
        List<byte> decoded = [];
        DecodeInto(decoded, coding, encoded, chunkSize);
        return [.. decoded];
    }

    private static void DecodeInto(List<byte> decoded, string coding, byte[] encoded, int chunkSize)
    {
        using HttpContentCodingDecoder decoder = new(CodingOf(coding));
        for (int offset = 0; offset < encoded.Length; offset += chunkSize)
        {
            foreach (ReadOnlyMemory<byte> piece in decoder.Decode(encoded.AsMemory(offset, Math.Min(chunkSize, encoded.Length - offset))))
            {
                decoded.AddRange(piece.ToArray());
            }
        }
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
