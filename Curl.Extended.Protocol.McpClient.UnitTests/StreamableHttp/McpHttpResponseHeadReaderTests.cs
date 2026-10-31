namespace Curl.Extended.Protocol.McpClient.StreamableHttp;

/// <summary>Each test reads a recorded response head against one rule of RFC 9112 the Streamable HTTP transport rests on.</summary>
[TestClass]
public sealed class McpHttpResponseHeadReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("HTTP/1.1 200")]
    [DataRow("HTTP/1.1 200 OK")]
    public void ParseStatusLine_Http11_ReturnsTheStatus(string line) =>
        Assert.AreEqual((200, true), McpHttpResponseHeadReader.ParseStatusLine(line));

    [TestMethod]
    public void ParseStatusLine_Http10_IsNotHttp11() =>
        Assert.AreEqual((200, false), McpHttpResponseHeadReader.ParseStatusLine("HTTP/1.0 200 OK"));

    [TestMethod]
    [DataRow("HTTP/1.1 20")]
    [DataRow("HTTP/2.0 200 OK")]
    [DataRow("HTTP/1.x 200 OK")]
    [DataRow("HTTP/1.1_200 OK")]
    [DataRow("HTTP/1.1 2x0 OK")]
    [DataRow("HTTP/1.1 2000")]
    public void ParseStatusLine_Malformed_Throws(string line)
    {
        // RFC 9112 §4: status-line = HTTP-version SP status-code SP [ reason-phrase ].
        var exception = Assert.ThrowsExactly<McpHttpException>(() => McpHttpResponseHeadReader.ParseStatusLine(line));

        Assert.AreEqual(McpHttpFailure.MalformedStatusLine, exception.Failure);
    }

    [TestMethod]
    [DataRow("no colon")]
    [DataRow(": value")]
    [DataRow("Name : value")]
    [DataRow("Na\tme: value")]
    public void ParseField_Malformed_Throws(string line)
    {
        // RFC 9112 §5.1: no whitespace is allowed between the field name and the colon.
        var exception = Assert.ThrowsExactly<McpHttpException>(() => McpHttpResponseHeadReader.ParseField(line));

        Assert.AreEqual(McpHttpFailure.MalformedHeaderField, exception.Failure);
    }

    [TestMethod]
    [DataRow("Transfer-Encoding: gzip, chunked\r\n", null)]
    [DataRow("Transfer-Encoding: gzip\r\nContent-Length: 4\r\n", -1L)]
    [DataRow("Content-Length: 4\r\n", 4L)]
    [DataRow("", -1L)]
    public async Task ReadAsync_Framing_FollowsRfc9112Section6_3(string fields, long? expectedLength)
    {
        var response = await Read($"HTTP/1.1 200 OK\r\n{fields}\r\n");

        Assert.AreEqual(expectedLength, McpHttpResponseHeadReader.FindBodyLength(response.Headers, false));
    }

    [TestMethod]
    [DataRow("204 No Content")]
    [DataRow("304 Not Modified")]
    public async Task ReadAsync_NoContentOrNotModified_HasAnEmptyBodyAndPersists(string status)
    {
        // RFC 9112 §6.3 rule 1: a 204 or 304 response ends at its head.
        var response = await Read($"HTTP/1.1 {status}\r\n\r\n");

        Assert.IsEmpty(await response.Body.ReadToEndAsync(TestContext.CancellationToken));
        Assert.IsTrue(response.ConnectionPersists);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: keep-alive, Close\r\nContent-Length: 0\r\n\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\n\r\n")]
    [DataRow("HTTP/1.0 200 OK\r\nContent-Length: 0\r\n\r\n")]
    public async Task ReadAsync_CloseOrCloseDelimitedOrHttp10_DoesNotPersist(string head) =>
        Assert.IsFalse((await Read(head)).ConnectionPersists);

    [TestMethod]
    public async Task ReadAsync_InvalidContentLength_Throws()
    {
        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(() => Read("HTTP/1.1 200 OK\r\nContent-Length: -1\r\n\r\n"));

        Assert.AreEqual(McpHttpFailure.InvalidContentLength, exception.Failure);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nA: b\r\n folded\r\n\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nA: b\r\n\tfolded\r\n\r\n")]
    public async Task ReadAsync_ObsFold_Throws(string head)
    {
        // RFC 9112 §5.2: a recipient may reject obs-fold.
        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(() => Read(head));

        Assert.AreEqual(McpHttpFailure.MalformedHeaderField, exception.Failure);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("HTTP/1.1 200 OK\r\nA: b\r\n")]
    public async Task ReadAsync_ClosedInsideTheHead_Throws(string head)
    {
        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(() => Read(head));

        Assert.AreEqual(McpHttpFailure.ClosedEarly, exception.Failure);
    }

    [TestMethod]
    public async Task ReadAsync_LineLongerThanTheLimit_Throws()
    {
        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(
            () => Read("HTTP/1.1 200 OK\r\nA: " + new string('a', McpHttpConnectionReader.MaxLineLength) + "\r\n\r\n"));

        Assert.AreEqual(McpHttpFailure.LineTooLong, exception.Failure);
    }

    private Task<McpHttpResponse> Read(string head) =>
        McpHttpResponseHeadReader.ReadAsync(new McpHttpConnectionReader(new RecordedConnection(head, readSize: 5)), TestContext.CancellationToken);
}
