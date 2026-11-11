using System.Net;
using System.Text;
using Curl.Extended.Protocol.McpClient.Client;
using Curl.Extended.Protocol.McpClient.Stdio;
using Curl.Extended.Protocol.McpClient.StreamableHttp;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Extended.Protocol.McpClient;

/// <summary>
/// Credentials, <c>-A</c> and <c>-H</c> on every MCP Streamable HTTP request as curl sends them on HTTP, and a persistent
/// 401 as exit 67 (Model Context Protocol 2025-06-18 "Authorization", ADR-1243, BL-4906).
/// </summary>
[TestClass]
public sealed class McpAuthorizationHeaderTests
{
    private const string InitializeResult = """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{"tools":{}},"serverInfo":{"name":"s","version":"1"}}}""";
    private const string ToolsResult = """{"jsonrpc":"2.0","id":2,"result":{"tools":[{"name":"a","description":"tool A"},{"name":"b","description":"tool B"}]}}""";
    private const string Accepted = "HTTP/1.1 202 Accepted\r\nContent-Length: 0\r\n\r\n";
    private const string Unauthorized = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Bearer resource_metadata=\"http://x/.well-known/oauth-protected-resource\"\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_OAuth2Bearer_EveryRequestCarriesTheTokenAndTheToolsAreListed()
    {
        var connection = new RecordedConnection(JsonReply(InitializeResult) + Accepted + JsonReply(ToolsResult));
        var output = new MemoryStream();

        var result = await Handler(connection).ExecuteAsync(Context(output, new HttpRequestOptions { BearerToken = "tok1" }));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("a\ttool A\nb\ttool B\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(3, CountOf(connection.Written, "POST /secure HTTP/1.1\r\nHost: example.com\r\nAuthorization: Bearer tok1\r\nUser-Agent: curl/8.22.0\r\n"));
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthorizationHeaderUserAgentAndCustomHeader_EveryRequestCarriesThem()
    {
        var connection = new RecordedConnection(JsonReply(InitializeResult) + Accepted + JsonReply(ToolsResult));
        var options = new HttpRequestOptions { UserAgent = "agent", Headers = ["Authorization: Bearer tok1", "X-Api-Key: k1"] };

        var result = await Handler(connection).ExecuteAsync(Context(new MemoryStream(), options));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(3, CountOf(connection.Written, "Host: example.com\r\nUser-Agent: agent\r\nAuthorization: Bearer tok1\r\nX-Api-Key: k1\r\n"));
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerAnswers401_ExitsLoginDenied67()
    {
        var connection = new RecordedConnection(Unauthorized);

        var result = await Handler(connection).ExecuteAsync(Context(new MemoryStream(), null));

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("The server answered with status 401.", result.ErrorMessage);
    }

    [TestMethod]
    public void ExitCodeOf_Status401_IsLoginDeniedAndOtherStatusesStayExit8()
    {
        Assert.AreEqual(CurlExitCode.LoginDenied, McpProtocolHandler.ExitCodeOf(new McpHttpException(McpHttpFailure.UnexpectedStatus, "x", 401)));
        Assert.AreEqual(CurlExitCode.WeirdServerReply, McpProtocolHandler.ExitCodeOf(new McpHttpException(McpHttpFailure.UnexpectedStatus, "x", 403)));
    }

    [TestMethod]
    public void Build_UserAndPassword_SendsBasicAuthorization() =>
        CollectionAssert.AreEqual(
            new[] { "Authorization: Basic dXNlcjpwdw==", "User-Agent: curl/8.22.0" },
            McpRequestHeaderFields.Build(null, new NetworkCredential("user", "pw"), null, []).ToArray());

    [TestMethod]
    public void Build_BearerAndUser_BearerWins() =>
        CollectionAssert.AreEqual(
            new[] { "Authorization: Bearer t" },
            McpRequestHeaderFields.Build("t", new NetworkCredential("user", "pw"), string.Empty, []).ToArray());

    [TestMethod]
    public void Build_EmptyBearerWithoutUserAndEmptyUserAgent_SendsNothing() =>
        Assert.IsEmpty(McpRequestHeaderFields.Build(string.Empty, null, string.Empty, []));

    [TestMethod]
    public void Build_CustomHeaderForms_ReplaceRemoveSendEmptyCutAtLineEndOrAreIgnored() =>
        CollectionAssert.AreEqual(
            new[] { "user-agent: mine", "X-Empty:", "X-Cut: a" },
            McpRequestHeaderFields.Build("t", null, null, ["authorization:", "user-agent:  mine ", "X-Empty;", ";", "NoSeparator", ":novalue", "X-Cut: a\r\nInjected: b"]).ToArray());

    private static McpProtocolHandler Handler(RecordedConnection connection) =>
        new(new ScriptedMcpConnector(ConnectResult.Connected(connection)), new ScriptedMcpServerProcessStarter(null));

    private static TransferContext Context(Stream output, HttpRequestOptions? http) => new()
    {
        Url = CurlUrl.Parse("mcp://example.com/secure"),
        Output = output,
        Http = http,
    };

    private static int CountOf(string text, string part) => text.Split(part).Length - 1;

    private static string JsonReply(string json) =>
        $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";
}
