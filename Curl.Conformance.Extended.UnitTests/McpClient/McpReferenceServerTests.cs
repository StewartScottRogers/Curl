using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.McpClient;

namespace Curl.Conformance.Extended.UnitTests.McpClient;

/// <summary>The reference MCP server's own answers to what curl never sends.</summary>
[TestClass]
public sealed class McpReferenceServerTests
{
    private static string Post(string body, string? sessionId = null) =>
        $"POST /mcp HTTP/1.1\r\nHost: x\r\n{(sessionId is null ? string.Empty : $"Mcp-Session-Id: {sessionId}\r\n")}Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}";

    private static async Task<string> ServeAsync(McpStreamableHttpReferenceServer server, string requests)
    {
        using OneByteStream stream = new(Encoding.UTF8.GetBytes(requests));
        await server.ServeAsync(new ReferenceConnection(stream, new WireExchange(0, ReferenceTransport.Tcp)), CancellationToken.None);
        return Encoding.UTF8.GetString(stream.Written.ToArray());
    }

    [TestMethod]
    public void Answer_UnknownMethod_IsMethodNotFound()
    {
        string? answer = new McpReferenceToolServer([]).Answer("""{"jsonrpc":"2.0","id":"a","method":"prompts/list"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":"a","error":{"code":-32601,"message":"Method not found: prompts/list"}}""", answer);
    }

    [TestMethod]
    [DataRow("""{"cursor":"bogus"}""")]
    [DataRow("""{"cursor":"opaque:x"}""")]
    [DataRow("""{"cursor":"opaque:9"}""")]
    [DataRow("""{"cursor":null}""")]
    public void Answer_InvalidCursor_IsInvalidParams(string parameters)
    {
        string? answer = new McpReferenceToolServer([]).Answer($$"""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{{parameters}}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"error":{"code":-32602,"message":"Invalid cursor"}}""", answer);
    }

    [TestMethod]
    public void Answer_ListWithParamsButNoCursor_ListsFromTheStart()
    {
        string? answer = new McpReferenceToolServer([new("t", "d", "{}")]).Answer("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        StringAssert.Contains(answer, "\"name\":\"t\"");
    }

    [TestMethod]
    public async Task ServeAsync_RequestWithoutSessionId_Is400_AndAfterExpiryTheNewSessionIsKept()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([])) { ExpiresSessionAtMethod = "tools/list" };
        const string Initialize = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""";
        const string List = """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""";

        string replies = await ServeAsync(server, Post(List) + Post(Initialize) + Post(List, "session-1") + Post(Initialize) + Post(List, "session-2") + Post(List, "session-2") + Post(List, "session-1"));

        string[] statuses = [.. replies.Split("HTTP/1.1 ").Skip(1).Select(reply => reply[..3])];
        CollectionAssert.AreEqual(new[] { "400", "200", "404", "200", "200", "200", "404" }, statuses);
        StringAssert.Contains(replies, "Mcp-Session-Id: session-2");
        Assert.HasCount(7, server.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionClosesInsideABody_EndsWithoutARequest()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([]));

        string replies = await ServeAsync(server, "POST /mcp HTTP/1.1\r\nContent-Length: 10\r\n\r\n{}");

        Assert.AreEqual(string.Empty, replies);
        Assert.HasCount(0, server.Requests);
    }

    // Hands out one byte per read, so a request arrives across many reads.
    private sealed class OneByteStream(byte[] input) : Stream
    {
        private int position;

        public MemoryStream Written { get; } = new();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (position == input.Length)
            {
                return 0;
            }

            buffer[offset] = input[position++];
            return 1;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);
    }
}
