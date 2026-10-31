using System.ComponentModel;
using System.Text;
using Curl.Extension.Protocol.McpClient.Stdio;
using Curl.Extension.Protocol.McpClient.StreamableHttp;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.McpClient.Client;

/// <summary>
/// Each test replays a recorded server against the Model Context Protocol 2025-06-18 and names the section that sets the
/// rule: "Lifecycle", "Server Features &gt; Tools", "Utilities &gt; Pagination" and the two "Base Protocol &gt; Transports".
/// </summary>
[TestClass]
public sealed class McpProtocolHandlerTests
{
    private const string InitializeResult = """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{"tools":{}},"serverInfo":{"name":"s","version":"1"}}}""";
    private const string Accepted = "HTTP/1.1 202 Accepted\r\nContent-Length: 0\r\n\r\n";
    private const string Deleted = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_McpUrlWithoutData_InitializesListsEveryPageAndEndsTheSession()
    {
        // "Lifecycle > Initialization", "Listing Tools" with "Pagination", and "Session Management": DELETE ends the session.
        var connection = new RecordedConnection(
            JsonReply(InitializeResult, "Mcp-Session-Id: s-1\r\n") + Accepted
            + JsonReply("""{"jsonrpc":"2.0","id":2,"result":{"tools":[{"name":"echo","description":"Echoes\nback","inputSchema":{"type":"object"}}],"nextCursor":"p2"}}""")
            + JsonReply("""{"jsonrpc":"2.0","id":3,"result":{"tools":[{"name":"add"}]}}""")
            + Deleted);
        var connector = new ScriptedMcpConnector(ConnectResult.Connected(connection));
        var output = new MemoryStream();

        var result = await Handler(connector).ExecuteAsync(Context("mcp://example.com/mcp?k=v", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("echo\tEchoes back\nadd\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(result.BytesTransferred, output.Length);
        StringAssert.StartsWith(connection.Written, "POST /mcp?k=v HTTP/1.1\r\nHost: example.com\r\n");
        StringAssert.Contains(connection.Written, """{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        StringAssert.Contains(connection.Written, "MCP-Protocol-Version: 2025-06-18\r\n");
        StringAssert.Contains(connection.Written, "\"params\":{\"cursor\":\"p2\"}");
        StringAssert.Contains(connection.Written, "DELETE /mcp?k=v HTTP/1.1\r\nHost: example.com\r\n");
        Assert.AreEqual(new ConnectTarget("example.com", 80, false).Port, connector.Target!.Port);
    }

    [TestMethod]
    [DataRow("mcp://example.com/mcp", 80, false)]
    [DataRow("mcps://example.com/mcp", 443, true)]
    [DataRow("MCPS://example.com:8443/mcp", 8443, true)]
    public void TargetOf_Scheme_GivesItsPortAndTls(string url, int port, bool useTls)
    {
        var target = McpHttpSession.TargetOf(Context(url, new MemoryStream()));

        Assert.AreEqual("example.com", target.Host);
        Assert.AreEqual(port, target.Port);
        Assert.AreEqual(useTls, target.UseTls);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExplicitPort_SendsItInTheHostHeader()
    {
        var connection = new RecordedConnection(string.Empty);

        await Handler(new ScriptedMcpConnector(ConnectResult.Connected(connection))).ExecuteAsync(Context("mcp://example.com:8080/", new MemoryStream()));

        StringAssert.StartsWith(connection.Written, "POST / HTTP/1.1\r\nHost: example.com:8080\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_EndsWithTheConnectorsExitCodeAndText()
    {
        var connector = new ScriptedMcpConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect"));

        var result = await Handler(connector).ExecuteAsync(Context("mcp://example.com/mcp", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerAnswersWithAJsonRpcError_EndsWithExit8AndTheError()
    {
        // "Base Protocol > Overview > Responses": an error object instead of a result.
        var connection = new RecordedConnection(JsonReply("""{"jsonrpc":"2.0","id":1,"error":{"code":-32602,"message":"Unsupported protocol version"}}"""));

        var result = await Handler(new ScriptedMcpConnector(ConnectResult.Connected(connection))).ExecuteAsync(Context("mcp://example.com/mcp", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("MCP server error -32602: Unsupported protocol version", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_StdioCallWithData_CallsTheToolAndWritesEachContentItem()
    {
        // "Transports > stdio" and "Calling Tools": a text item is written as its text, any other item as its JSON.
        var process = new InMemoryMcpServerProcess(
            InitializeResult + "\n"
            + """{"jsonrpc":"2.0","id":2,"result":{"content":[{"type":"text","text":"7"},{"type":"image","data":"AA==","mimeType":"image/png"}]}}""" + "\n");
        var starter = new ScriptedMcpServerProcessStarter(process);
        var output = new MemoryStream();

        var result = await Handler(starter).ExecuteAsync(Context("mcp+stdio://localhost/npx?-y&my%20server", output, """{"name":"add","arguments":{"a":3,"b":4}}"""));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("7\n{\"type\":\"image\",\"data\":\"AA==\",\"mimeType\":\"image/png\"}\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual("npx", starter.Command);
        CollectionAssert.AreEqual(new[] { "-y", "my server" }, starter.Arguments!.ToArray());
        StringAssert.Contains(process.WrittenInput, """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"add","arguments":{"a":3,"b":4}}}""" + "\n");
        Assert.IsTrue(process.InputClosed);
        Assert.IsTrue(process.Disposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ToolReportsAnError_WritesItsContentAndEndsWithExit22()
    {
        // "Server Features > Tools > Error Handling": isError marks a tool execution error.
        var process = new InMemoryMcpServerProcess(
            InitializeResult + "\n" + """{"jsonrpc":"2.0","id":2,"result":{"content":[{"type":"text","text":"no such city"}],"isError":true}}""" + "\n");
        var output = new MemoryStream();

        var result = await Handler(new ScriptedMcpServerProcessStarter(process)).ExecuteAsync(Context("mcp+stdio://localhost/weather", output, """{"name":"forecast"}"""));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(McpProtocolHandler.ToolReportedError, result.ErrorMessage);
        Assert.AreEqual("no such city\n", Encoding.UTF8.GetString(output.ToArray()));
        StringAssert.Contains(process.WrittenInput, "\"arguments\":{}");
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerExitsBeforeAnswering_EndsWithExit8()
    {
        var result = await Handler(new ScriptedMcpServerProcessStarter(new InMemoryMcpServerProcess(string.Empty))).ExecuteAsync(Context("mcp+stdio://localhost/server", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerCannotStart_EndsWithExit7()
    {
        var starter = new ScriptedMcpServerProcessStarter(null, new Win32Exception(2, "The system cannot find the file specified."));

        var result = await Handler(starter).ExecuteAsync(Context("mcp+stdio://localhost/missing", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to start the MCP server missing: The system cannot find the file specified.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_StdioUrlWithARemoteHost_EndsWithExit3()
    {
        var starter = new ScriptedMcpServerProcessStarter(null);

        var result = await Handler(starter).ExecuteAsync(Context("mcp+stdio://example.com/server", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(McpProtocolHandler.NoLocalCommand, result.ErrorMessage);
        Assert.IsNull(starter.Command);
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("""{"arguments":{}}""")]
    public async Task ExecuteAsync_DataIsNotAToolCall_EndsWithExit43BeforeConnecting(string data)
    {
        var connector = new ScriptedMcpConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "x"));

        var result = await Handler(connector).ExecuteAsync(Context("mcp://example.com/mcp", new MemoryStream(), data));

        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual(McpProtocolHandler.NotAToolCall, result.ErrorMessage);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    public void ExitCodeOf_TransportAndUnknownExceptions_GivesExit8OrNothing()
    {
        Assert.AreEqual(CurlExitCode.WeirdServerReply, McpProtocolHandler.ExitCodeOf(new McpHttpException(McpHttpFailure.NoResponse, "x")));
        Assert.IsNull(McpProtocolHandler.ExitCodeOf(new IOException()));
    }

    [TestMethod]
    public void UseProtocolVersion_OnStdio_DoesNothing()
    {
        var process = new InMemoryMcpServerProcess(string.Empty);
        var session = new McpStdioClientSession(new McpStdioSession(process, TimeProvider.System));

        session.UseProtocolVersion("2025-06-18");

        Assert.AreEqual(string.Empty, process.WrittenInput);
        Assert.IsFalse(process.InputClosed);
        Assert.IsFalse(process.Killed);
        Assert.IsFalse(process.Disposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancelled_LetsTheCancellationThroughAndStopsTheServer()
    {
        var process = new InMemoryMcpServerProcess(InitializeResult + "\n");
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("mcp+stdio://localhost/server"),
            Output = new MemoryStream(),
            CancellationToken = new CancellationToken(canceled: true),
        };

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await Handler(new ScriptedMcpServerProcessStarter(process)).ExecuteAsync(context));

        Assert.IsTrue(process.Disposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Handler(new ScriptedMcpServerProcessStarter(null)).ExecuteAsync(null!));

    private static McpProtocolHandler Handler(IConnector connector) => new(connector, new ScriptedMcpServerProcessStarter(null));

    private static McpProtocolHandler Handler(IMcpServerProcessStarter starter) =>
        new(new ScriptedMcpConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "x")), starter);

    private static TransferContext Context(string url, Stream output, string? data = null) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = output,
        PostData = data is null ? null : (ReadOnlyMemory<byte>?)Encoding.UTF8.GetBytes(data),
    };

    private static string JsonReply(string json, string extraFields = "") =>
        $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n{extraFields}Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";
}
