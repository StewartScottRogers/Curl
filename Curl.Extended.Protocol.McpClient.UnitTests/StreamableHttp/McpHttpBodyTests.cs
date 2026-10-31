using System.Text;

namespace Curl.Extended.Protocol.McpClient.StreamableHttp;

/// <summary>Each test reads a recorded response body against one framing rule of RFC 9112 §6 and §7.</summary>
[TestClass]
public sealed class McpHttpBodyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ChunkedBody_ChunksAndTrailers_ReadsTheData()
    {
        // RFC 9112 §7.1: chunks until a zero-size chunk, then trailer fields.
        var body = new McpChunkedBody(Reader("3;name=value\r\nabc\r\n2\r\nde\r\n0\r\nTrailer: x\r\n\r\n"));

        Assert.AreEqual("abcde", Encoding.ASCII.GetString(await body.ReadToEndAsync(TestContext.CancellationToken)));
        Assert.AreEqual(0, await body.ReadAsync(new byte[1], TestContext.CancellationToken));
    }

    [TestMethod]
    [DataRow("x\r\nabc\r\n0\r\n\r\n", McpHttpFailure.InvalidChunk)]
    [DataRow("3\r\nabcX\r\n0\r\n\r\n", McpHttpFailure.InvalidChunk)]
    [DataRow("3\r\nab", McpHttpFailure.ClosedEarly)]
    [DataRow("", McpHttpFailure.ClosedEarly)]
    public async Task ChunkedBody_Malformed_Throws(string recorded, McpHttpFailure failure)
    {
        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(() => new McpChunkedBody(Reader(recorded)).ReadToEndAsync(TestContext.CancellationToken));

        Assert.AreEqual(failure, exception.Failure);
    }

    [TestMethod]
    public async Task ContentLengthBody_ReadsExactlyTheLength()
    {
        // RFC 9112 §6.3 rule 6: the body is Content-Length bytes.
        var body = new McpContentLengthBody(Reader("abcdef"), 4);

        Assert.AreEqual("abcd", Encoding.ASCII.GetString(await body.ReadToEndAsync(TestContext.CancellationToken)));
    }

    [TestMethod]
    public async Task ContentLengthBody_ClosedEarly_Throws()
    {
        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(() => new McpContentLengthBody(Reader("ab"), 4).ReadToEndAsync(TestContext.CancellationToken));

        Assert.AreEqual(McpHttpFailure.ClosedEarly, exception.Failure);
    }

    [TestMethod]
    public async Task CloseDelimitedBody_ReadsUntilTheClose()
    {
        // RFC 9112 §6.3 rule 8: the body ends when the server closes the connection.
        var body = new McpCloseDelimitedBody(Reader("abc"));

        Assert.AreEqual("abc", Encoding.ASCII.GetString(await body.ReadToEndAsync(TestContext.CancellationToken)));
    }

    private static McpHttpConnectionReader Reader(string recorded) => new(new RecordedConnection(recorded, readSize: 2));
}
