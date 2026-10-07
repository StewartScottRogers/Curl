using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// <c>-0</c>/<c>--http1.0</c>, <c>--raw</c> and <c>--ignore-content-length</c> over HTTP. Every
/// request, output and message here was measured on curl 8.21.0 against a loopback server on
/// port 18180 with <c>Record-CurlExchange.ps1</c>, which sent each response and closed
/// (BL-180 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string LoopbackUrl = "http://127.0.0.1:18180/a";

    private const string LoopbackHeaders = "Host: 127.0.0.1:18180\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n";

    private const string LoopbackGet = "GET /a HTTP/1.1\r\n" + LoopbackHeaders + "\r\n";

    [TestMethod]
    public async Task ExecuteAsync_Http10_SendsAnHttp10RequestLine()
    {
        const string expected = "GET /a HTTP/1.0\r\n" + LoopbackHeaders + "\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            ScriptedConnection connection = Connection("HTTP/1.0 200 OK\r\nContent-Length: 5\r\n\r\nhello", chunkSize, expected);
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("expected request", OneLine(expected));

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(EncodingContext(output, new HttpRequestOptions { Version = HttpVersionPreference.Http10 }));

            WriteResult(result);
            Diagnostics.Assert("body", "hello", Latin1(output.ToArray()));
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http10WithData_SendsAnHttp10PostWithItsLength()
    {
        const string expected = "POST /a HTTP/1.0\r\n" + LoopbackHeaders
            + "Content-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=1";
        HttpRequestOptions options = new()
        {
            Version = HttpVersionPreference.Http10,
            Body = new BytesBody(Encoding.Latin1.GetBytes("x=1"), "application/x-www-form-urlencoded"),
        };
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            ScriptedConnection connection = Connection("HTTP/1.0 200 OK\r\nContent-Length: 5\r\n\r\nhello", chunkSize, expected);
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("expected request", OneLine(expected));

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(EncodingContext(output, options));

            WriteResult(result);
            Diagnostics.Assert("body", "hello", Latin1(output.ToArray()));
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http10WithChunkedHeader_SendsTheBodyChunked()
    {
        const string expected = "POST /a HTTP/1.0\r\n" + LoopbackHeaders
            + "Transfer-Encoding: chunked\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n1\r\nx\r\n0\r\n\r\n";
        HttpRequestOptions options = new()
        {
            Version = HttpVersionPreference.Http10,
            Headers = ["Transfer-Encoding: chunked"],
            Body = new BytesBody(Encoding.Latin1.GetBytes("x"), "application/x-www-form-urlencoded"),
        };
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", chunkSize, expected);
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("expected request", OneLine(expected));

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(EncodingContext(new MemoryStream(), options));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http10WithBodyOfUnknownLength_FailsWithExit25AndSendsNothing()
    {
        HttpRequestOptions options = new()
        {
            Version = HttpVersionPreference.Http10,
            Body = new StreamBody(new MemoryStream([1, 2, 3]), null, "application/octet-stream"),
            CustomMethod = "PUT",
        };
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", 65536);
        MemoryStream output = new();
        Diagnostics.Arrange("request", "HTTP/1.0 PUT of a 3-byte stream of unknown length");

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(EncodingContext(output, options));

        WriteResult(result);
        Diagnostics.Assert("bytes written to the connection", 0, connection.Written.Length);
        Diagnostics.Assert("output length", 0L, output.Length);
        Assert.AreEqual(CurlExitCode.UploadFailed, result.ExitCode);
        Assert.AreEqual("Chunky upload is not supported by HTTP 1.0", result.ErrorMessage);
        Assert.IsEmpty(connection.Written);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    [DataRow(
        "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n",
        "5\r\nhello\r\n0\r\n\r\n",
        DisplayName = "--raw chunked")]
    [DataRow(
        "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n",
        "5\r\nhello\r\n0\r\nX-T: 1\r\n\r\nEXTRA",
        DisplayName = "--raw chunked with trailers and bytes past the end")]
    [DataRow(
        "HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n",
        "5\r\nhello\r\n0\r\n\r\n",
        DisplayName = "--raw refuses no transfer coding")]
    [DataRow(
        "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: 3\r\n\r\n",
        "5\r\nhello\r\n0\r\n\r\n",
        DisplayName = "--raw chunked ignores Content-Length")]
    public async Task ExecuteAsync_RawChunked_WritesTheBodyUndecodedUntilTheServerCloses(string head, string body)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headerOutput = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted head", OneLine(head));
            Diagnostics.Arrange("scripted body", OneLine(body));

            TransferResult result = await Handler(QueueConnector.For(Connection(head + body, chunkSize, LoopbackGet)))
                .ExecuteAsync(EncodingContext(output, new HttpRequestOptions { Raw = true }, headerOutput));

            WriteResult(result);
            Diagnostics.Assert("body", OneLine(body), OneLine(Latin1(output.ToArray())));
            Diagnostics.Assert("bytes transferred", (long)body.Length, result.BytesTransferred);
            Diagnostics.Assert("header output", OneLine(head), OneLine(Latin1(headerOutput.ToArray())));
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(body, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual((long)body.Length, result.BytesTransferred, $"Chunk size {chunkSize}");
            Assert.AreEqual(head, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_RawWithContentLength_StopsAtIt()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", "200, Content-Length: 3, body hello; --raw");

            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nhello", chunkSize)))
                .ExecuteAsync(EncodingContext(output, new HttpRequestOptions { Raw = true }));

            WriteResult(result);
            Diagnostics.Assert("body", "hel", Latin1(output.ToArray()));
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("hel", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 3\r\nConnection: close\r\n\r\nhello", "hello", DisplayName = "Longer than its Content-Length")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 9\r\n\r\nhello", "hello", DisplayName = "Shorter than its Content-Length")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: 3\r\n\r\n5\r\nhello\r\n0\r\n\r\n", "hello", DisplayName = "Chunked is still decoded")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: abc\r\n\r\nhello", "hello", DisplayName = "An invalid Content-Length is not read")]
    public async Task ExecuteAsync_IgnoreContentLength_ReadsUntilTheServerCloses(string response, string body)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", OneLine(response));

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize, LoopbackGet)))
                .ExecuteAsync(EncodingContext(output, new HttpRequestOptions { IgnoreContentLength = true }));

            WriteResult(result);
            Diagnostics.Assert("body", OneLine(body), OneLine(Latin1(output.ToArray())));
            Diagnostics.Assert("bytes transferred", (long)body.Length, result.BytesTransferred);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(body, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual((long)body.Length, result.BytesTransferred, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_RawWithCompressed_AsksForEncodingAndWritesTheBodyUndecoded()
    {
        const string expected = "GET /a HTTP/1.1\r\n" + LoopbackHeaders + "Accept-Encoding: deflate, gzip, br, zstd\r\n\r\n";
        const string response = "HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: 5\r\n\r\nhello";
        MemoryStream output = new();
        Diagnostics.Arrange("scripted response", OneLine(response));
        Diagnostics.Arrange("options", "--raw --compressed");

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536, expected)))
            .ExecuteAsync(EncodingContext(output, new HttpRequestOptions { Raw = true, Compressed = true }));

        WriteResult(result);
        Diagnostics.Assert("body", "hello", Latin1(output.ToArray()));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    private static TransferContext EncodingContext(Stream output, HttpRequestOptions options, Stream? headerOutput = null) =>
        new() { Url = CurlUrl.Parse(LoopbackUrl), Output = output, HeaderOutput = headerOutput, Http = options };
}
