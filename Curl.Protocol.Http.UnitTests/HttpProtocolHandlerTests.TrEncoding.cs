using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// <c>--tr-encoding</c> over HTTP. Every request, output and message here was measured on curl
/// 8.21.0 against a loopback server on port 18180 with <c>Record-CurlExchange.ps1</c>, which
/// sent each response and closed (BL-315 Notes). In each response <c>{gz}</c> stands for the
/// 25-byte gzip stream of <c>hello</c> the measurement sent.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string TrEncodingGet = "GET /a HTTP/1.1\r\n" + LoopbackHeaders + "TE: gzip\r\nConnection: TE\r\n\r\n";

    private const string GzipHello = "1F8B080000000000020ACB48CDC9C9070086A6103605000000";

    private const string ZlibHello = "789CCB48CDC9C90700062C0215";

    private const string BrotliHello = "0B028068656C6C6F03";

    private const string GzipOfGzipHello =
        "1F8B0800000000000400" + "93EFE66000011686D31E674F9E6467685B2660C60A1400005D6C48CB19000000";

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 25\r\nTransfer-Encoding: gzip\r\n\r\n{gz}", "hello", DisplayName = "gzip")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n19\r\n{gz}\r\n0\r\n\r\n", "hello", DisplayName = "gzip, chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 13\r\nTransfer-Encoding: deflate\r\n\r\n{zlib}", "hello", DisplayName = "deflate")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 25\r\nTransfer-Encoding: x-gzip\r\n\r\n{gz}", "hello", DisplayName = "x-gzip")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 10\r\nTransfer-Encoding: br\r\n\r\n{br}", "hello", DisplayName = "br")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 25\r\nTransfer-Encoding: , GZip ,\r\n\r\n{gz}", "hello", DisplayName = "Blanks, case and empty items")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nTransfer-Encoding: identity\r\n\r\nhello", "hello", DisplayName = "identity")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip\r\n\r\n{gz}", "hello", DisplayName = "gzip to the close")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 12\r\nTransfer-Encoding: gzip\r\n\r\n{gz}", "hello", DisplayName = "Content-Length not trusted")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip\r\nTransfer-Encoding: chunked\r\n\r\n19\r\n{gz}\r\n0\r\n\r\n", "hello", DisplayName = "chunked in a later header")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 25\r\nContent-Encoding: gzip\r\nTransfer-Encoding: gzip\r\n\r\n{gz}", "hello", DisplayName = "Content-Encoding not decoded")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nTransfer-Encoding: identity, identity, identity, identity, identity\r\n\r\nhello", "hello", DisplayName = "Five codings")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: identity, identity, identity, identity, chunked\r\n\r\n5\r\nhello\r\n0\r\n\r\n", "hello", DisplayName = "Five codings with chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nTransfer-Encoding: gzip\r\n\r\n", "", DisplayName = "Empty gzip body")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nTransfer-Encoding: foo\r\n\r\n", "", DisplayName = "Empty body with an unknown coding")]
    public async Task ExecuteAsync_TransferEncoding_SendsTeAndDecodesTheBody(string response, string body)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(Encoded(response), chunkSize, TrEncodingGet)))
                .ExecuteAsync(EncodingContext(output, new HttpRequestOptions { TransferEncoding = true }));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            Assert.AreEqual(body, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nTransfer-Encoding: gzip\r\n\r\nhello", "Error while processing content unencoding: incorrect header check", DisplayName = "Corrupt gzip")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 25\r\nTransfer-Encoding: gzip, gzip\r\n\r\n{gz}", "Error while processing content unencoding: incorrect header check", DisplayName = "gzip twice")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nTransfer-Encoding: foo\r\n\r\nhello", "Unrecognized content encoding type", DisplayName = "Unknown coding")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: foo, chunked\r\n\r\n5\r\nhello\r\n0\r\n\r\n", "Unrecognized content encoding type", DisplayName = "Unknown coding, chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked, gzip\r\n\r\n19\r\n{gz}\r\n0\r\n\r\n", "Reject response due to 'chunked' not being the last Transfer-Encoding", DisplayName = "gzip after chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked, identity\r\n\r\n5\r\nhello\r\n0\r\n\r\n", "Reject response due to 'chunked' not being the last Transfer-Encoding", DisplayName = "identity after chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nTransfer-Encoding: gzip\r\n\r\n19\r\n{gz}\r\n0\r\n\r\n", "Reject response due to 'chunked' not being the last Transfer-Encoding", DisplayName = "gzip in a header after chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nTransfer-Encoding: identity, identity, identity, identity, identity, identity\r\n\r\nhello", "Reject response exceeding limit of 5 transfer encodings", DisplayName = "Six codings")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: identity, identity, identity, identity, identity, chunked\r\n\r\n5\r\nhello\r\n0\r\n\r\n", "Reject response exceeding limit of 5 transfer encodings", DisplayName = "Six codings with chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nTransfer-Encoding: identity, identity, identity\r\nTransfer-Encoding: identity, identity, identity\r\n\r\nhello", "Reject response exceeding limit of 5 transfer encodings", DisplayName = "Six codings in two headers")]
    public async Task ExecuteAsync_TransferEncodingRefused_FailsWithExit61(string response, string message)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(Encoded(response), chunkSize, TrEncodingGet)))
                .ExecuteAsync(EncodingContext(output, new HttpRequestOptions { TransferEncoding = true }));

            Assert.AreEqual(CurlExitCode.BadContentEncoding, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 25\r\nTransfer-Encoding: gzip\r\n\r\n{gz}", "hello", DisplayName = "--raw still decodes gzip")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n0\r\n\r\n", "5\r\nhello\r\n0\r\n\r\n", DisplayName = "--raw passes chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 25\r\nContent-Encoding: gzip\r\n\r\n{gz}", "{gz}", DisplayName = "--raw passes Content-Encoding")]
    public async Task ExecuteAsync_RawTransferEncoding_DecodesAllButChunked(string response, string body)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            HttpRequestOptions options = new() { TransferEncoding = true, Raw = true, Compressed = response.Contains("Content-Encoding", StringComparison.Ordinal) };
            string expected = options.Compressed
                ? "GET /a HTTP/1.1\r\n" + LoopbackHeaders + "TE: gzip\r\nAccept-Encoding: deflate, gzip, br\r\nConnection: TE\r\n\r\n"
                : TrEncodingGet;

            TransferResult result = await Handler(QueueConnector.For(Connection(Encoded(response), chunkSize, expected)))
                .ExecuteAsync(EncodingContext(output, options));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(Encoded(body), Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n19\r\n{gz}\r\n0\r\n\r\n", "Error while processing content unencoding: incorrect header check", DisplayName = "--raw gzip over raw chunks")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nTransfer-Encoding: foo\r\n\r\nhello", "Unrecognized content encoding type", DisplayName = "--raw unknown coding")]
    public async Task ExecuteAsync_RawTransferEncodingRefused_FailsWithExit61(string response, string message)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TransferResult result = await Handler(QueueConnector.For(Connection(Encoded(response), chunkSize, TrEncodingGet)))
                .ExecuteAsync(EncodingContext(new MemoryStream(), new HttpRequestOptions { TransferEncoding = true, Raw = true }));

            Assert.AreEqual(CurlExitCode.BadContentEncoding, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, result.ErrorMessage, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferEncodingAndCompressed_DecodesTheTransferCodingThenTheContentCoding()
    {
        const string expected = "GET /a HTTP/1.1\r\n" + LoopbackHeaders + "TE: gzip\r\nAccept-Encoding: deflate, gzip, br\r\nConnection: TE\r\n\r\n";
        string response = "HTTP/1.1 200 OK\r\nContent-Length: 42\r\nContent-Encoding: gzip\r\nTransfer-Encoding: gzip\r\n\r\n"
            + Latin1(HttpContentDecoderTests.Bytes(GzipOfGzipHello));
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize, expected)))
                .ExecuteAsync(EncodingContext(output, new HttpRequestOptions { TransferEncoding = true, Compressed = true }));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(42L, result.BytesTransferred, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with curl 8.21.0 <c>-L --max-redirs 0 --tr-encoding</c> against a 302 with
    /// <c>Transfer-Encoding: foo</c> and body <c>abc</c>: curl ends with exit 47, not 61, so the
    /// discarded body is not decoded; without <c>-L</c> the same response is exit 61.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_TransferEncodingWhileFollowing_DoesNotDecodeTheDiscardedBody()
    {
        MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(LoopbackUrl),
            Output = output,
            Http = new HttpRequestOptions { FollowRedirects = true, TransferEncoding = true },
        };

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 3\r\nTransfer-Encoding: foo\r\n\r\nabc", 65536)))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("http://127.0.0.1:18180/b", result.Report!.RedirectUrl);
        Assert.AreEqual(0L, output.Length);
    }

    /// <summary>
    /// Replaces <c>{gz}</c>, <c>{zlib}</c> and <c>{br}</c> with the encoded <c>hello</c> the
    /// measurement sent, one character per byte.
    /// </summary>
    private static string Encoded(string text) =>
        text.Replace("{gz}", Latin1(HttpContentDecoderTests.Bytes(GzipHello)), StringComparison.Ordinal)
            .Replace("{zlib}", Latin1(HttpContentDecoderTests.Bytes(ZlibHello)), StringComparison.Ordinal)
            .Replace("{br}", Latin1(HttpContentDecoderTests.Bytes(BrotliHello)), StringComparison.Ordinal);
}
