using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpContentDecoder" /> and <see cref="HttpContentCodingDecoder" /> against
/// curl 8.21.0 (mingw, Schannel) run with <c>--compressed</c> against a loopback server that
/// sent each body below (BL-177 Notes). Every body is fed in 1-byte, 7-byte and whole pieces
/// and must decode the same.
/// </summary>
[TestClass]
public sealed class HttpContentDecoderTests
{
    /// <summary><c>hello</c> as gzip, as sent to curl.</summary>
    internal const string Gzip = "1F8B080000000000000ACB48CDC9C9070086A6103605000000";

    /// <summary><c>hello</c> as zlib-wrapped deflate, as sent to curl.</summary>
    internal const string ZLib = "789CCB48CDC9C90700062C0215";

    /// <summary><c>hello</c> as raw deflate, as sent to curl.</summary>
    internal const string RawDeflate = "CB48CDC9C90700";

    /// <summary><c>hello</c> as Brotli, as sent to curl.</summary>
    internal const string Brotli = "0B028068656C6C6F03";

    /// <summary><c>hello</c> gzipped and then Brotli-compressed, for <c>Content-Encoding: gzip, br</c>.</summary>
    internal const string GzipThenBrotli = "0B0C801F8B080000000000000ACB48CDC9C9070086A610360500000003";

    private static readonly int[] ChunkSizes = [1, 7, 65536];

    [TestMethod]
    [DataRow("gzip", Gzip, DisplayName = "gzip")]
    [DataRow("x-gzip", Gzip, DisplayName = "x-gzip")]
    [DataRow("GZIP", Gzip, DisplayName = "Coding compared without case")]
    [DataRow("gzip", ZLib, DisplayName = "gzip label on a zlib stream")]
    [DataRow("deflate", ZLib, DisplayName = "zlib-wrapped deflate")]
    [DataRow("deflate", RawDeflate, DisplayName = "raw deflate")]
    [DataRow("br", Brotli, DisplayName = "br")]
    [DataRow("gzip, br", GzipThenBrotli, DisplayName = "Stacked gzip, br")]
    [DataRow("identity, gzip,, \tbr", GzipThenBrotli, DisplayName = "identity and empty items skipped")]
    public async Task WriteAsync_EncodedBody_WritesTheDecodedBody(string contentEncoding, string encoded)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = await DecodeAsync([Header("Content-Encoding", contentEncoding)], Bytes(encoded), chunkSize);

            Assert.AreEqual("hello", Encoding.ASCII.GetString(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task WriteAsync_TwoContentEncodingHeaders_DecodeAsOneStackedList()
    {
        HttpResponseHeader[] headers = [Header("Content-Encoding", "gzip"), Header("Content-Type", "text/plain"), Header("content-encoding", "br")];

        MemoryStream output = await DecodeAsync(headers, Bytes(GzipThenBrotli), 65536);

        Assert.AreEqual("hello", Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public void For_NoCodingToDecode_ReturnsNull()
    {
        Assert.IsNull(HttpContentDecoder.For([]));
        Assert.IsNull(HttpContentDecoder.For([Header("Content-Type", "gzip")]));
        Assert.IsNull(HttpContentDecoder.For([Header("Content-Encoding", "identity")]));
        Assert.IsNull(HttpContentDecoder.For([Header("Content-Encoding", " , ")]));
    }

    /// <summary>
    /// Measured: <c>Content-Encoding: compress</c> gives
    /// <c>curl: (61) Unrecognized content encoding type</c>. <c>zstd</c> is refused the same
    /// way, because Curl does not advertise it (ADR-0020).
    /// </summary>
    [TestMethod]
    [DataRow("compress")]
    [DataRow("zstd")]
    [DataRow("gzip, compress")]
    public async Task WriteAsync_UnrecognizedCoding_ThrowsExit61WithoutWriting(string contentEncoding)
    {
        using HttpContentDecoder decoder = HttpContentDecoder.For([Header("Content-Encoding", contentEncoding)])!;
        MemoryStream output = new();

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await decoder.WriteAsync(output, Bytes(HttpContentDecoderTests.Gzip), CancellationToken.None));

        Assert.AreEqual(0L, output.Length);

        Assert.AreEqual(CurlExitCode.BadContentEncoding, thrown.ExitCode);
        Assert.AreEqual("Unrecognized content encoding type", thrown.Message);
    }

    /// <summary>
    /// Parses hexadecimal text, ignoring spaces, into bytes.
    /// </summary>
    internal static byte[] Bytes(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    private static async Task<MemoryStream> DecodeAsync(HttpResponseHeader[] headers, byte[] encoded, int chunkSize)
    {
        using HttpContentDecoder decoder = HttpContentDecoder.For(headers)!;
        MemoryStream output = new();
        for (int offset = 0; offset < encoded.Length; offset += chunkSize)
        {
            await decoder.WriteAsync(output, encoded.AsMemory(offset, Math.Min(chunkSize, encoded.Length - offset)), CancellationToken.None);
        }

        return output;
    }

    private static HttpResponseHeader Header(string name, string value) => new(name, value);
}
