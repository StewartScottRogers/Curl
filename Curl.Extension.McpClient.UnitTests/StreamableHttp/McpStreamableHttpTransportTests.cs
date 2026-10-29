using System.Text;
using System.Text.Json;
using Curl.Extension.McpClient.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.McpClient.StreamableHttp;

/// <summary>
/// Each test replays a recorded server reply against one rule of the Model Context Protocol 2025-06-18,
/// "Base Protocol &gt; Transports &gt; Streamable HTTP", and names the section that sets it.
/// </summary>
[TestClass]
public sealed class McpStreamableHttpTransportTests
{
    private const string Request = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";
    private const string Notification = """{"jsonrpc":"2.0","method":"notifications/initialized"}""";
    private const string Result = """{"jsonrpc":"2.0","id":1,"result":{"tools":[]}}""";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SendRequestAsync_JsonReply_PostsWithTheNamedHeadersAndReturnsTheResponse()
    {
        // "Sending Messages to the Server": every message is a POST accepting application/json and text/event-stream.
        var connection = new RecordedConnection(JsonReply(Result));
        await using var transport = Transport(connection);

        var response = await transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken);

        Assert.AreEqual(
            "POST /mcp HTTP/1.1\r\nHost: example.com:8080\r\nAccept: application/json, text/event-stream\r\n"
            + $"Content-Type: application/json\r\nContent-Length: {Request.Length}\r\n\r\n{Request}",
            connection.Written);
        Assert.AreEqual(1L, response.Id);
        Assert.AreEqual("""{"tools":[]}""", response.Result!.Value.GetRawText());
        Assert.IsFalse(connection.IsDisposed);
    }

    [TestMethod]
    public async Task SendRequestAsync_AfterASessionIdAndProtocolVersion_SendsBothOnTheSameConnection()
    {
        // "Session Management" and "Protocol Version Header": Mcp-Session-Id and MCP-Protocol-Version go on every later request.
        var connection = new RecordedConnection(JsonReply(Result, "Mcp-Session-Id: abc-123\r\n") + JsonReply(Result));
        await using var transport = Transport(connection);

        await transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken);
        transport.ProtocolVersion = "2025-06-18";
        await transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken);

        Assert.AreEqual("abc-123", transport.SessionId);
        Assert.AreEqual("2025-06-18", transport.ProtocolVersion);
        StringAssert.Contains(connection.Written, $"Content-Length: {Request.Length}\r\nMcp-Session-Id: abc-123\r\nMCP-Protocol-Version: 2025-06-18\r\n\r\n");
    }

    [TestMethod]
    public async Task SendRequestAsync_ChunkedEventStreamSplitAcrossReads_ReturnsTheAwaitedResponse()
    {
        // "Sending Messages to the Server": a text/event-stream reply may carry server notifications before the response.
        var events = ": comment\r\nevent: message\r\ndata: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\"}\r\n\r\n"
            + "id: 7\rdata: {\"jsonrpc\":\"2.0\",\"id\":1,\r\ndata:\"result\":{\"tools\":[]}}\n\n";
        var body = $"{events.Length - 40:x}\r\n{events[..^40]}\r\n{40:x};ext=1\r\n{events[^40..]}\r\n0\r\nTrailer: x\r\n\r\n";
        var connection = new RecordedConnection(
            $"HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nTransfer-Encoding: chunked\r\n\r\n{body}", readSize: 7);
        await using var transport = Transport(connection);

        var response = await transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken);

        Assert.AreEqual("""{"tools":[]}""", response.Result!.Value.GetRawText());
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task SendRequestAsync_EventStreamEndsWithoutTheResponse_FailsWithNoResponse()
    {
        // "Sending Messages to the Server": the stream is to carry the response to the request.
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream; charset=utf-8\r\n\r\ndata: {\"jsonrpc\":\"2.0\",\"id\":9,\"result\":{}}\n\n");
        await using var transport = Transport(connection);

        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(() => transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken));

        Assert.AreEqual(McpHttpFailure.NoResponse, exception.Failure);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task SendRequestAsync_JsonReplyForAnotherId_FailsWithNoResponse()
    {
        // "Sending Messages to the Server": an application/json reply is the one response to the request.
        await using var transport = Transport(new RecordedConnection(JsonReply("""{"jsonrpc":"2.0","id":2,"result":{}}""")));

        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(() => transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken));

        Assert.AreEqual(McpHttpFailure.NoResponse, exception.Failure);
    }

    [TestMethod]
    [DataRow("Content-Type: text/html\r\n")]
    [DataRow("")]
    public async Task SendRequestAsync_ReplyOfAnotherType_FailsWithUnexpectedContentType(string contentType)
    {
        // "Sending Messages to the Server": the reply is application/json or text/event-stream.
        await using var transport = Transport(new RecordedConnection($"HTTP/1.1 200 OK\r\n{contentType}Content-Length: 0\r\n\r\n"));

        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(() => transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken));

        Assert.AreEqual(McpHttpFailure.UnexpectedContentType, exception.Failure);
    }

    [TestMethod]
    public async Task SendRequestAsync_NotFound_FailsWithTheStatus()
    {
        // "Session Management": a server answers a request for an ended session with 404 Not Found.
        var connection = new RecordedConnection("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n");
        await using var transport = Transport(connection);

        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(() => transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken));

        Assert.AreEqual(McpHttpFailure.UnexpectedStatus, exception.Failure);
        Assert.AreEqual(404, exception.StatusCode);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    [DataRow("Mcp-Session-Id: a b")]
    [DataRow("Mcp-Session-Id:")]
    public async Task SendRequestAsync_SessionIdNotVisibleAscii_FailsWithMalformedHeaderField(string field)
    {
        // "Session Management": the session ID must only contain visible ASCII characters (0x21 to 0x7E).
        await using var transport = Transport(new RecordedConnection(JsonReply(Result, field + "\r\n")));

        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(() => transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken));

        Assert.AreEqual(McpHttpFailure.MalformedHeaderField, exception.Failure);
    }

    [TestMethod]
    public async Task SendRequestAsync_ConnectionClose_OpensANewConnectionForTheNextExchange()
    {
        // RFC 9112 §9.3: a response with Connection: close ends the connection.
        var first = new RecordedConnection(JsonReply(Result, "Connection: close\r\n"));
        var second = new RecordedConnection(JsonReply(Result));
        var connections = new Queue<IConnection>([first, second]);
        await using var transport = new McpStreamableHttpTransport(_ => ValueTask.FromResult(connections.Dequeue()), "example.com:8080", "/mcp");

        await transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken);
        await transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken);

        Assert.IsTrue(first.IsDisposed);
        StringAssert.StartsWith(second.Written, "POST /mcp HTTP/1.1\r\n");
    }

    [TestMethod]
    public async Task SendNotificationAsync_Accepted_CompletesAndKeepsTheConnection()
    {
        // "Sending Messages to the Server": a notification is answered by 202 Accepted with no body.
        var connection = new RecordedConnection("HTTP/1.1 202 Accepted\r\nContent-Length: 0\r\n\r\n");
        await using var transport = Transport(connection);

        await transport.SendNotificationAsync(Encoding.UTF8.GetBytes(Notification), TestContext.CancellationToken);

        Assert.AreEqual(
            "POST /mcp HTTP/1.1\r\nHost: example.com:8080\r\nAccept: application/json, text/event-stream\r\n"
            + $"Content-Type: application/json\r\nContent-Length: {Notification.Length}\r\n\r\n{Notification}",
            connection.Written);
        Assert.IsFalse(connection.IsDisposed);
    }

    [TestMethod]
    public async Task SendNotificationAsync_AnsweredByOk_FailsWithTheStatus()
    {
        // "Sending Messages to the Server": a server that accepts a notification returns 202 Accepted.
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
        await using var transport = Transport(connection);

        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(
            () => transport.SendNotificationAsync(Encoding.UTF8.GetBytes(Notification), TestContext.CancellationToken));

        Assert.AreEqual(McpHttpFailure.UnexpectedStatus, exception.Failure);
        Assert.AreEqual(200, exception.StatusCode);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task EndSessionAsync_WithoutASession_OpensNoConnection()
    {
        // "Session Management": only a session the server assigned is ended with DELETE.
        await using var transport = new McpStreamableHttpTransport(_ => throw new AssertFailedException("No connection is opened."), "example.com", "/mcp");

        await transport.EndSessionAsync(TestContext.CancellationToken);

        Assert.IsNull(transport.SessionId);
    }

    [TestMethod]
    [DataRow("200 OK")]
    [DataRow("204 No Content")]
    [DataRow("405 Method Not Allowed")]
    public async Task EndSessionAsync_WithASession_SendsDeleteAndForgetsTheSession(string status)
    {
        // "Session Management": DELETE with Mcp-Session-Id ends the session; 405 means the server does not let the client end it.
        var connection = new RecordedConnection(JsonReply(Result, "Mcp-Session-Id: s1\r\n") + $"HTTP/1.1 {status}\r\nContent-Length: 0\r\n\r\n");
        await using var transport = Transport(connection);
        await transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken);

        await transport.EndSessionAsync(TestContext.CancellationToken);

        StringAssert.EndsWith(connection.Written, "DELETE /mcp HTTP/1.1\r\nHost: example.com:8080\r\nMcp-Session-Id: s1\r\n\r\n");
        Assert.IsNull(transport.SessionId);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    [DataRow("199 Odd")]
    [DataRow("500 Internal Server Error")]
    public async Task EndSessionAsync_OtherStatus_FailsWithTheStatusAndKeepsTheSession(string status)
    {
        // "Session Management": only a success or 405 ends the client's side of the session.
        var connection = new RecordedConnection(JsonReply(Result, "Mcp-Session-Id: s1\r\n") + $"HTTP/1.1 {status}\r\nContent-Length: 0\r\n\r\nHTTP/1.1 500 x\r\n\r\n");
        await using var transport = Transport(connection);
        await transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken);

        var exception = await Assert.ThrowsExactlyAsync<McpHttpException>(() => transport.EndSessionAsync(TestContext.CancellationToken));

        Assert.AreEqual(500, exception.StatusCode);
        Assert.AreEqual("s1", transport.SessionId);
    }

    [TestMethod]
    public async Task DisposeAsync_WithAnOpenConnection_DisposesIt()
    {
        var connection = new RecordedConnection(JsonReply(Result));
        var transport = Transport(connection);
        await transport.SendRequestAsync(OutgoingRequest(), TestContext.CancellationToken);

        await transport.DisposeAsync();

        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public void ProtocolVersion_SetToNull_SendsNone()
    {
        var transport = Transport(new RecordedConnection(string.Empty));

        transport.ProtocolVersion = null;

        Assert.IsNull(transport.ProtocolVersion);
    }

    [TestMethod]
    public void ProtocolVersion_HoldsALineBreak_Throws()
    {
        var transport = Transport(new RecordedConnection(string.Empty));

        Assert.ThrowsExactly<ArgumentException>(() => transport.ProtocolVersion = "2025\r\nX: y");
    }

    [TestMethod]
    [DataRow("example.com\r\n", "/mcp")]
    [DataRow("example.com", "")]
    public void Constructor_AuthorityOrTargetNotVisibleAscii_Throws(string authority, string target) =>
        Assert.ThrowsExactly<ArgumentException>(() => new McpStreamableHttpTransport(_ => throw new InvalidOperationException(), authority, target));

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpStreamableHttpTransport(null!, "example.com", "/mcp"));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpStreamableHttpTransport(_ => throw new InvalidOperationException(), null!, "/mcp"));
    }

    [TestMethod]
    public async Task SendAsync_NullMessages_Throw()
    {
        await using var transport = Transport(new RecordedConnection(string.Empty));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => transport.SendRequestAsync(null!, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => transport.SendNotificationAsync(null!, TestContext.CancellationToken));
    }

    private static string JsonReply(string json, string extraFields = "") =>
        $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n{extraFields}Content-Length: {json.Length}\r\n\r\n{json}";

    private static McpOutgoingRequest OutgoingRequest() => new(1, Encoding.UTF8.GetBytes(Request));

    private static McpStreamableHttpTransport Transport(RecordedConnection connection) =>
        new(_ => ValueTask.FromResult<IConnection>(connection), "example.com:8080", "/mcp");
}
