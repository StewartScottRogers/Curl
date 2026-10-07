using System.Text;

using Curl.Networking.Fakes;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DohResponseReader" /> to how curl 8.21.0 took a DoH server's response
/// (measured with <c>Record-CurlExchange.ps1 -Tls</c> on 2026-09-28, BL-641): status and
/// <c>Content-Type</c> ignored, <c>Content-Length</c> and chunked bodies read, and a body
/// delimited by the close, cut short or too large read as a failure.
/// </summary>
[TestClass]
public sealed class DohResponseReaderTests
{
    private static readonly byte[] Body = [0x00, 0x00, 0x81, 0x80, 0x0D, 0x0A, 0x41];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReadBodyAsync_WithContentLength_ReturnsThatManyBytes()
    {
        var body = await ReadAsync(Head("HTTP/1.1 200 OK", "Content-Type: application/dns-message", "Content-Length: 7"), Body, "trailing"u8.ToArray());

        Diagnostics.Diff("body", Body, body);
        CollectionAssert.AreEqual(Body, body);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 500 Internal Server Error", "Content-Type: application/dns-message")]
    [DataRow("HTTP/1.1 200 OK", "Content-Type: text/plain")]
    [DataRow("HTTP/1.1 404 Not Found", "X-Other: y")]
    public async Task ReadBodyAsync_WhateverTheStatusOrContentType_ReturnsTheBody(string statusLine, string header)
    {
        // curl decoded the body of a 500, of a text/plain answer and of one with no
        // Content-Type alike, and resolved the name from it (measured).
        var body = await ReadAsync(Head(statusLine, header, "content-length:  7 "), Body);

        Diagnostics.Diff("body", Body, body);
        CollectionAssert.AreEqual(Body, body);
    }

    [TestMethod]
    public async Task ReadBodyAsync_WithAnEmptyBody_ReturnsNoBytes()
    {
        var body = await ReadAsync(Head("HTTP/1.1 500 Internal Server Error", "Content-Length: 0"));

        Diagnostics.Assert("body length", 0, body?.Length);
        Assert.IsNotNull(body);
        Assert.IsEmpty(body);
    }

    [TestMethod]
    public async Task ReadBodyAsync_WithBareLineFeeds_ReadsTheHead()
    {
        var body = await ReadAsync(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\nContent-Length: 7\n\n"), Body);

        Diagnostics.Diff("body", Body, body);
        CollectionAssert.AreEqual(Body, body);
    }

    [TestMethod]
    public async Task ReadBodyAsync_WithAChunkedBody_JoinsItsChunks()
    {
        var chunked = Encoding.Latin1.GetBytes("4;name=value\r\n").Concat(Body[..4]).Concat("\r\n3\r\n"u8.ToArray())
            .Concat(Body[4..]).Concat("\r\n0\r\nX-Trailer: 1\r\n\r\n"u8.ToArray()).ToArray();

        var body = await ReadAsync(Head("HTTP/1.1 200 OK", "Transfer-Encoding: chunked", "Content-Length: 99"), chunked);

        Diagnostics.Diff("body", Body, body);
        CollectionAssert.AreEqual(Body, body);
    }

    [TestMethod]
    public async Task ReadBodyAsync_WithTransferEncodingNotChunkedAndAContentLength_ReadsTheContentLength()
    {
        var body = await ReadAsync(Head("HTTP/1.1 200 OK", "Content-Length: 7", "Transfer-Encoding: identity"), Body);

        Diagnostics.Diff("body", Body, body);
        CollectionAssert.AreEqual(Body, body);
    }

    [TestMethod]
    public async Task ReadBodyAsync_WithABodyOfTheMaximumLength_ReturnsIt()
    {
        var large = new byte[DohResponseReader.MaximumBodyLength];

        var body = await ReadAsync(Head("HTTP/1.1 200 OK", "Content-Length: 3000"), large);

        Diagnostics.Assert("body length", DohResponseReader.MaximumBodyLength, body?.Length);
        Assert.HasCount(DohResponseReader.MaximumBodyLength, body!);
    }

    [TestMethod]
    public async Task ReadBodyAsync_WithAChunkedBodyOfTheMaximumLength_ReturnsIt()
    {
        var chunked = "BB8\r\n"u8.ToArray().Concat(new byte[3000]).Concat("\r\n0\r\n\r\n"u8.ToArray()).ToArray();

        var body = await ReadAsync(Head("HTTP/1.1 200 OK", "Transfer-Encoding: chunked"), chunked);

        Diagnostics.Assert("body length", DohResponseReader.MaximumBodyLength, body?.Length);
        Assert.HasCount(DohResponseReader.MaximumBodyLength, body!);
    }

    [TestMethod]
    [DataRow("close-delimited", "HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\n\r\nABCDEFG")]
    [DataRow("cut short", "HTTP/1.1 200 OK\r\nContent-Length: 8\r\n\r\nABCDEFG")]
    [DataRow("over curl's buffer", "HTTP/1.1 200 OK\r\nContent-Length: 3001\r\n\r\nABCDEFG")]
    [DataRow("Content-Length not a number", "HTTP/1.1 200 OK\r\nContent-Length: seven\r\n\r\nABCDEFG")]
    [DataRow("Content-Length negative", "HTTP/1.1 200 OK\r\nContent-Length: -7\r\n\r\nABCDEFG")]
    [DataRow("no status line", "ABCDEFG\r\nContent-Length: 7\r\n\r\nABCDEFG")]
    [DataRow("nothing at all", "")]
    [DataRow("closed in the status line", "HTTP/1.1 200")]
    [DataRow("closed in the head", "HTTP/1.1 200 OK\r\nContent-Length: 7\r\n")]
    [DataRow("a header line without a colon", "HTTP/1.1 200 OK\r\nContent-Length 7\r\n\r\nABCDEFG")]
    [DataRow("a header line starting with a colon", "HTTP/1.1 200 OK\r\n: 7\r\n\r\nABCDEFG")]
    [DataRow("chunk size not hexadecimal", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\nABCDEFG\r\n0\r\n\r\n")]
    [DataRow("chunk size negative", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nFFFFFFFF\r\nABCDEFG\r\n0\r\n\r\n")]
    [DataRow("chunk size missing", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n")]
    [DataRow("chunk not followed by CRLF", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nABCD\r\n0\r\n\r\n")]
    [DataRow("closed after a chunk's data", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nABC")]
    [DataRow("chunk cut short", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n9\r\nABC")]
    [DataRow("chunks over curl's buffer", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nBB9\r\nABC")]
    [DataRow("closed in the trailer", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n1\r\nA\r\n0\r\nX-Trailer: 1\r\n")]
    public async Task ReadBodyAsync_WithAResponseCurlFailsToReceive_ReturnsNull(string reason, string response)
    {
        // A close-delimited body was "DoH request Failure when receiving data from the peer" (measured).
        Diagnostics.Arrange("reason", reason);
        var body = await ReadAsync(Encoding.Latin1.GetBytes(response));

        Diagnostics.Assert("body", "null", body is null ? "null" : $"{body.Length} bytes");
        Assert.IsNull(body, reason);
    }

    [TestMethod]
    public async Task ReadBodyAsync_WithALineOverTheLimit_ReturnsNull()
    {
        Diagnostics.Arrange("line limit", DohResponseReader.MaximumLineLength);
        var longLine = "HTTP/1.1 200 OK\r\nX-Long: " + new string('a', DohResponseReader.MaximumLineLength) + "\r\nContent-Length: 0\r\n\r\n";

        var body = await ReadAsync(Encoding.Latin1.GetBytes(longLine));

        Diagnostics.Assert("body", "null", body is null ? "null" : $"{body.Length} bytes");
        Assert.IsNull(body);
    }

    private static byte[] Head(params string[] lines) =>
        Encoding.Latin1.GetBytes(string.Join("\r\n", lines) + "\r\n\r\n");

    private async Task<byte[]?> ReadAsync(params byte[][] parts)
    {
        byte[] response = [.. parts.SelectMany(part => part)];
        Diagnostics.Arrange("response length", response.Length);
        Diagnostics.Bytes("response", response);
        var connection = new ScriptedConnection(response);

        var body = await DohResponseReader.ReadBodyAsync(connection, CancellationToken.None);

        Diagnostics.Act("body", body is null ? "null" : $"{body.Length} bytes");
        if (body is not null)
        {
            Diagnostics.Bytes("body", body);
        }

        return body;
    }
}
