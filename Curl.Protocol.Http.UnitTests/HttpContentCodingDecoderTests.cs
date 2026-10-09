using System.IO.Compression;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    /// <summary><c>hello zstd\n</c>, the content of <see cref="ZstdHelloFrame" />.</summary>
    private const string ZstdHello = "68656C6C6F207A7374640A";

    /// <summary>
    /// <c>hello zstd\n</c> as the 20-byte zstd frame sent to curl (BL-861 Notes): single
    /// segment, a 1-byte content size of 11, no checksum, one last raw block of 11 bytes.
    /// </summary>
    private const string ZstdHelloFrame = "28B52FFD200B590000" + ZstdHello;

    private static readonly int[] ChunkSizes = [1, 7, 65536];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    /// <summary>
    /// Measured: <c>00 01 02 …</c> as gzip gives <c>incorrect header check</c>, and
    /// <c>1F 8B 07</c> gives <c>unknown compression method</c>. A zlib header naming method 7
    /// (<c>77 09</c>) fails the same way, as zlib reports it. Corrupt data past a good header,
    /// and corrupt Brotli (measured), give curl's generic exit 61 text (ADR-0031); raw deflate here is a dynamic block, since a reserved or broken stored first block gets zlib's own text (BL-1810).
    /// </summary>
    [TestMethod]
    [DataRow("gzip", "000102030405060708090A0B", "Error while processing content unencoding: incorrect header check", DisplayName = "gzip: not a gzip or zlib header")]
    [DataRow("gzip", "1F8B07000000", "Error while processing content unencoding: unknown compression method", DisplayName = "gzip: method 7")]
    [DataRow("gzip", "7709000000", "Error while processing content unencoding: unknown compression method", DisplayName = "gzip: zlib header with method 7")]
    [DataRow("deflate", "7709000000", "Error while processing content unencoding: unknown compression method", DisplayName = "deflate: zlib header with method 7")]
    [DataRow("gzip", "1F8B080000000000000AFFFFFFFF", "Unrecognized or bad HTTP Content or Transfer-Encoding", DisplayName = "gzip: corrupt data")]
    [DataRow("deflate", "FDFFFFFF", "Unrecognized or bad HTTP Content or Transfer-Encoding", DisplayName = "deflate: corrupt raw data")]
    [DataRow("br", "FFFFFFFF", "Unrecognized or bad HTTP Content or Transfer-Encoding", DisplayName = "br: corrupt data")]
    public void Decode_CorruptBody_ThrowsExit61WithTheMeasuredMessage(string coding, string encoded, string message)
    {
        Diagnostics.Arrange("coding", coding);
        Diagnostics.Arrange("encoded", encoded);
        foreach (int chunkSize in ChunkSizes)
        {
            HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
                () => Decode(coding, HttpContentDecoderTests.Bytes(encoded), chunkSize));

            Diagnostics.Act($"exit at chunk size {chunkSize}", $"{thrown.ExitCode}: {thrown.Message}");
            Diagnostics.Assert($"message at chunk size {chunkSize}", message, thrown.Message);
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
        Diagnostics.Arrange("encoded", "the first 15 bytes of the gzip hello");
        foreach (int chunkSize in ChunkSizes)
        {
            byte[] decoded = Decode("gzip", HttpContentDecoderTests.Bytes(HttpContentDecoderTests.Gzip)[..15], chunkSize);

            Diagnostics.Act($"decoded at chunk size {chunkSize}", Encoding.ASCII.GetString(decoded));
            Diagnostics.Assert($"decoded at chunk size {chunkSize}", "hell", Encoding.ASCII.GetString(decoded));
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
        Diagnostics.Arrange("coding", coding);
        Diagnostics.Arrange("encoded", encoded);
        foreach (int chunkSize in ChunkSizes)
        {
            List<byte> decoded = [];

            HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
                () => DecodeInto(decoded, coding, HttpContentDecoderTests.Bytes(encoded), chunkSize));

            Diagnostics.Act($"decoded at chunk size {chunkSize}", Encoding.ASCII.GetString([.. decoded]));
            Diagnostics.Assert($"exit at chunk size {chunkSize}", CurlExitCode.WriteError, thrown.ExitCode);
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
        Diagnostics.Arrange("encoded", HttpContentDecoderTests.RawDeflate + "4142");
        foreach (int chunkSize in ChunkSizes)
        {
            byte[] decoded = Decode("deflate", HttpContentDecoderTests.Bytes(HttpContentDecoderTests.RawDeflate + "4142"), chunkSize);

            Diagnostics.Act($"decoded at chunk size {chunkSize}", Encoding.ASCII.GetString(decoded));
            Diagnostics.Assert($"decoded at chunk size {chunkSize}", "hello", Encoding.ASCII.GetString(decoded));
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
        Diagnostics.Arrange("coding", coding);
        Diagnostics.Arrange("body length", body.Length);
        foreach (int chunkSize in ChunkSizes)
        {
            List<byte> decoded = [];

            HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
                () => DecodeInto(decoded, coding, [.. Encode(coding, body), 0x41], chunkSize));

            Diagnostics.Act($"decoded length at chunk size {chunkSize}", decoded.Count);
            Diagnostics.Diff($"decoded at chunk size {chunkSize}", body, [.. decoded]);
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
        Diagnostics.Arrange("coding", coding);
        Diagnostics.Arrange("body length", body.Length);
        foreach (int chunkSize in ChunkSizes)
        {
            byte[] decoded = Decode(coding, Encode(coding, body), chunkSize);

            Diagnostics.Act($"decoded length at chunk size {chunkSize}", decoded.Length);
            Diagnostics.Diff($"decoded at chunk size {chunkSize}", body, decoded);
            CollectionAssert.AreEqual(body, decoded, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("gzip", "1F", DisplayName = "gzip: one byte")]
    [DataRow("gzip", "1F8B", DisplayName = "gzip: two bytes of gzip header")]
    [DataRow("deflate", "78", DisplayName = "deflate: one byte")]
    public void Decode_TooFewBytesToTellTheFormat_DecodesNothing(string coding, string encoded)
    {
        Diagnostics.Arrange("coding", coding);
        Diagnostics.Arrange("encoded", encoded);

        byte[] decoded = Decode(coding, HttpContentDecoderTests.Bytes(encoded), 1);

        Diagnostics.Act("decoded length", decoded.Length);
        Diagnostics.Assert("decoded length", 0, decoded.Length);
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
        Diagnostics.Arrange("coding", coding);
        Diagnostics.Arrange("body length", body.Length);

        int[] sizes = [.. decoder.Decode(Encode(coding, body)).Select(piece => piece.Length)];

        Diagnostics.Act("piece sizes", string.Join(", ", sizes));
        Diagnostics.Assert("piece sizes", "16384, 16384, 7232", string.Join(", ", sizes));
        CollectionAssert.AreEqual(new[] { 16384, 16384, 7232 }, sizes);
        CollectionAssert.AreEqual(body, Decode(coding, Encode(coding, body), 65536));
    }

    /// <summary>
    /// Measured (BL-861 Notes): a 20-byte zstd frame of <c>hello zstd\n</c> writes it, exit 0.
    /// </summary>
    [TestMethod]
    public void Decode_ZstdFrame_WritesItsContent()
    {
        Diagnostics.Arrange("encoded", ZstdHelloFrame);
        foreach (int chunkSize in ChunkSizes)
        {
            byte[] decoded = Decode("zstd", HttpContentDecoderTests.Bytes(ZstdHelloFrame), chunkSize);

            Diagnostics.Bytes($"decoded at chunk size {chunkSize}", decoded);
            Diagnostics.Act($"decoded length at chunk size {chunkSize}", decoded.Length);
            Diagnostics.Diff($"decoded at chunk size {chunkSize}", "hello zstd\n"u8, decoded);
            Assert.AreEqual("hello zstd\n", Encoding.ASCII.GetString(decoded), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured (BL-861 Notes): two concatenated frames write both contents, exit 0.
    /// </summary>
    [TestMethod]
    public void Decode_TwoConcatenatedZstdFrames_WritesBothContents()
    {
        Diagnostics.Arrange("encoded", ZstdHelloFrame + ZstdHelloFrame);
        foreach (int chunkSize in ChunkSizes)
        {
            byte[] decoded = Decode("zstd", HttpContentDecoderTests.Bytes(ZstdHelloFrame + ZstdHelloFrame), chunkSize);

            Diagnostics.Act($"decoded length at chunk size {chunkSize}", decoded.Length);
            Diagnostics.Diff($"decoded at chunk size {chunkSize}", "hello zstd\nhello zstd\n"u8, decoded);
            Assert.AreEqual("hello zstd\nhello zstd\n", Encoding.ASCII.GetString(decoded), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured (BL-861 Notes): the frame's first 15 bytes write <c>hello </c>, what its raw
    /// block gave, and exit 0; a body that ends mid-frame is not an error.
    /// </summary>
    [TestMethod]
    public void Decode_TruncatedZstdFrame_WritesWhatArrivedWithoutFailing()
    {
        Diagnostics.Arrange("encoded", "the first 15 bytes of the zstd hello frame");
        foreach (int chunkSize in ChunkSizes)
        {
            byte[] decoded = Decode("zstd", HttpContentDecoderTests.Bytes(ZstdHelloFrame)[..15], chunkSize);

            Diagnostics.Act($"decoded at chunk size {chunkSize}", Encoding.ASCII.GetString(decoded));
            Diagnostics.Assert($"decoded at chunk size {chunkSize}", "hello ", Encoding.ASCII.GetString(decoded));
            Assert.AreEqual("hello ", Encoding.ASCII.GetString(decoded), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured (BL-861 Notes): a bad magic number and a reserved block type each write
    /// nothing and exit 61 with <c>Unrecognized or bad HTTP Content or Transfer-Encoding</c>.
    /// </summary>
    [TestMethod]
    [DataRow("29B52FFD200B590000" + ZstdHello, DisplayName = "zstd: bad magic number")]
    [DataRow("28B52FFD200B5F0000" + ZstdHello, DisplayName = "zstd: reserved block type")]
    public void Decode_CorruptZstdBody_ThrowsExit61WithoutWriting(string encoded)
    {
        Diagnostics.Arrange("encoded", encoded);
        foreach (int chunkSize in ChunkSizes)
        {
            List<byte> decoded = [];

            HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
                () => DecodeInto(decoded, "zstd", HttpContentDecoderTests.Bytes(encoded), chunkSize));

            Diagnostics.Act($"exit at chunk size {chunkSize}", $"{thrown.ExitCode}: {thrown.Message}");
            Diagnostics.Assert($"decoded length at chunk size {chunkSize}", 0, decoded.Count);
            Assert.IsEmpty(decoded, $"Chunk size {chunkSize}");
            Assert.AreEqual(CurlExitCode.BadContentEncoding, thrown.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Unrecognized or bad HTTP Content or Transfer-Encoding", thrown.Message, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured (BL-861 Notes): <c>junk</c> or four zero bytes after the last frame write the
    /// frame's content and then exit 61 - not gzip and Brotli's exit 23 - because libzstd
    /// reads them as the next frame's magic number and rejects it.
    /// </summary>
    [TestMethod]
    [DataRow(ZstdHelloFrame + "6A756E6B", DisplayName = "zstd, then junk")]
    [DataRow(ZstdHelloFrame + "00000000", DisplayName = "zstd, then four zero bytes")]
    public void Decode_BytesAfterTheLastZstdFrame_WritesTheFrameThenThrowsExit61(string encoded)
    {
        Diagnostics.Arrange("encoded", encoded);
        foreach (int chunkSize in ChunkSizes)
        {
            List<byte> decoded = [];

            HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
                () => DecodeInto(decoded, "zstd", HttpContentDecoderTests.Bytes(encoded), chunkSize));

            Diagnostics.Act($"exit at chunk size {chunkSize}", $"{thrown.ExitCode}: {thrown.Message}");
            Diagnostics.Diff($"decoded at chunk size {chunkSize}", "hello zstd\n"u8, [.. decoded]);
            Assert.AreEqual("hello zstd\n", Encoding.ASCII.GetString([.. decoded]), $"Chunk size {chunkSize}");
            Assert.AreEqual(CurlExitCode.BadContentEncoding, thrown.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Unrecognized or bad HTTP Content or Transfer-Encoding", thrown.Message, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// A 40000-byte RLE block comes out in pieces of at most the output size, however its
    /// frame arrives.
    /// </summary>
    [TestMethod]
    public void Decode_LargeZstdBlock_ReturnsPiecesOfAtMostTheOutputSize()
    {
        // Window descriptor 0x30 (64 KiB), then a last RLE block of 40000 bytes of 'A'.
        byte[] encoded = HttpContentDecoderTests.Bytes("28B52FFD0030" + "03E204" + "41");
        using HttpContentCodingDecoder decoder = new(HttpContentCoding.Zstandard);
        Diagnostics.Bytes("encoded", encoded);
        Diagnostics.Arrange("frame", "a last RLE block of 40000 bytes of 'A'");

        int[] sizes = [.. decoder.Decode(encoded).Select(piece => piece.Length)];

        Diagnostics.Act("piece sizes", string.Join(", ", sizes));
        Diagnostics.Assert("piece sizes", "16384, 16384, 7232", string.Join(", ", sizes));
        CollectionAssert.AreEqual(new[] { 16384, 16384, 7232 }, sizes);
        foreach (int chunkSize in ChunkSizes)
        {
            CollectionAssert.AreEqual(Enumerable.Repeat((byte)'A', 40000).ToArray(), Decode("zstd", encoded, chunkSize), $"Chunk size {chunkSize}");
        }
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
        "zstd" => HttpContentCoding.Zstandard,
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
