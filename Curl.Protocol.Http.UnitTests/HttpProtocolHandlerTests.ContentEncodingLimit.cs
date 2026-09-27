using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// The limit of five Content-Encoding codings under <c>--compressed</c>. Every case here was
/// measured on curl 8.21.0 with <c>-sS --compressed</c> against a loopback server with
/// <c>Record-CurlExchange.ps1</c>, which sent each response and closed (BL-364 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string SixContentCodings = "Content-Encoding: identity, identity, identity, identity, identity, identity\r\n";

    private const string TooManyContentCodings = "Reject response exceeding limit of 5 content encodings";

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n" + SixContentCodings + "\r\nhello", "", DisplayName = "Six codings")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Encoding: identity, identity, identity\r\nContent-Encoding: identity, identity, identity\r\n\r\nhello", "", DisplayName = "Six codings in two headers")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n" + SixContentCodings + "\r\n", "", DisplayName = "Empty body")]
    [DataRow("HTTP/1.1 200 OK\r\n" + SixContentCodings + "Connection: close\r\n\r\n", "", DisplayName = "Body to the close")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Encoding: foo, foo, foo, foo, foo, foo\r\n\r\nhello", "", DisplayName = "Unrecognized codings")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\ncontent-encoding: gzip,br,deflate,x-gzip,IDENTITY,foo\r\n\r\nhello", "", DisplayName = "Every coding counted")]
    [DataRow("HTTP/1.1 200 OK\r\n" + SixContentCodings + "Content-Length: x\r\n\r\nhello", "", DisplayName = "Invalid Content-Length after it")]
    [DataRow("HTTP/1.1 200 OK\r\n" + SixContentCodings + "Transfer-Encoding: foo\r\n\r\nhello", "", DisplayName = "Refused Transfer-Encoding after it")]
    [DataRow("HTTP/1.1 404 Not Found\r\nContent-Length: 5\r\n" + SixContentCodings + "\r\nhello", "-f", DisplayName = "Before -f")]
    [DataRow("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 0\r\n" + SixContentCodings + "\r\n", "-L", DisplayName = "Before a redirect is followed")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n" + SixContentCodings + "\r\n", "-I", DisplayName = "With -I")]
    public async Task ExecuteAsync_CompressedAndMoreThanFiveContentCodings_FailsWithExit61(string response, string option)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(CompressedContext(output, option));

            Assert.AreEqual(CurlExitCode.BadContentEncoding, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(TooManyContentCodings, result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Encoding: identity, identity, identity, identity, identity\r\n\r\nhello", "", "hello", DisplayName = "Five codings")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Encoding: identity,,identity, ,identity,identity,identity,\r\n\r\nhello", "", "hello", DisplayName = "Empty items not counted")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n" + SixContentCodings + "\r\nhello", "--raw", "hello", DisplayName = "--raw")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n" + SixContentCodings + "\r\nhello", "no --compressed", "hello", DisplayName = "Without --compressed")]
    [DataRow("HTTP/1.1 204 No Content\r\n" + SixContentCodings + "\r\n", "", "", DisplayName = "204")]
    [DataRow("HTTP/1.1 304 Not Modified\r\n" + SixContentCodings + "\r\n", "", "", DisplayName = "304")]
    public async Task ExecuteAsync_ContentCodingsWithinTheLimitOrNotDecoded_WritesTheBody(string response, string option, string body)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(CompressedContext(output, option));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            Assert.AreEqual(body, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: x\r\n" + SixContentCodings + "\r\nhello", CurlExitCode.WeirdServerReply, "Invalid Content-Length: value", "", DisplayName = "Invalid Content-Length")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: x\r\n" + SixContentCodings + "\r\n", CurlExitCode.WeirdServerReply, "Invalid Content-Length: value", "-I", DisplayName = "Invalid Content-Length with -I")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: foo\r\n" + SixContentCodings + "\r\nhello", CurlExitCode.BadContentEncoding, "Unsolicited Transfer-Encoding (foo) found", "", DisplayName = "Refused Transfer-Encoding")]
    public async Task ExecuteAsync_RefusedHeaderBeforeTheContentCodingsPastTheLimit_ReportsThatHeader(
        string response,
        CurlExitCode exitCode,
        string message,
        string option)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(CompressedContext(new MemoryStream(), option));

            Assert.AreEqual(exitCode, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, result.ErrorMessage, $"Chunk size {chunkSize}");
        }
    }

    private static TransferContext CompressedContext(Stream output, string option) =>
        new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = output,
            NoBody = option == "-I",
            Http = new HttpRequestOptions
            {
                Compressed = option != "no --compressed",
                Raw = option == "--raw",
                Fail = option == "-f" ? HttpFailMode.Fail : HttpFailMode.None,
                FollowRedirects = option == "-L",
            },
        };
}
