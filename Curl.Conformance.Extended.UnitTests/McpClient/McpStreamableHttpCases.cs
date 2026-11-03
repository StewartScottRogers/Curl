using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.McpClient;
using static Curl.Conformance.Extended.UnitTests.McpClient.McpClientRun;

namespace Curl.Conformance.Extended.UnitTests.McpClient;

/// <summary>MCP 2025-06-18 "Base Protocol &gt; Transports &gt; Streamable HTTP" cases.</summary>
[TestClass]
public sealed class McpStreamableHttpCases
{
    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-StreamableHttp-1")]
    [SpecRequirement("McpClient:MCP20250618-StreamableHttp-2")]
    [SpecRequirement("McpClient:MCP20250618-StreamableHttp-3")]
    [SpecRequirement("McpClient:MCP20250618-StreamableHttp-4")]
    public async Task JsonReply_EveryMessageIsOnePostAcceptingJsonAndEventStream()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([Echo]));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("echo\tEchoes its text\n", Text(result.StandardOutput));
        foreach (McpHttpRequestRecord post in server.Requests.Where(request => request.Method == "POST"))
        {
            Assert.AreEqual("/mcp", post.Target);
            string[] accepted = [.. post.Header("Accept")!.Split(',').Select(type => type.Trim())];
            CollectionAssert.IsSubsetOf(new[] { "application/json", "text/event-stream" }, accepted);
            Assert.AreEqual("application/json", post.Header("Content-Type"));
            Assert.IsNotNull(post.JsonRpcMethod, "The body is one JSON-RPC message.");
        }
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-StreamableHttp-4")]
    public async Task EventStreamReply_SkipsUnrelatedMessagesBeforeTheResponse()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([Echo])) { RepliesWithEventStream = true };

        CurlRunResult result = await RunAsync(server, "-d", """{"name":"echo","arguments":{}}""", Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("hello\n", Text(result.StandardOutput));
        Assert.AreEqual(string.Empty, Text(result.StandardError));
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-StreamableHttp-5")]
    [SpecRequirement("McpClient:MCP20250618-StreamableHttp-6")]
    [SpecRequirement("McpClient:MCP20250618-StreamableHttp-7")]
    [SpecRequirement("McpClient:MCP20250618-StreamableHttp-9")]
    public async Task LaterRequests_CarrySessionIdAndNegotiatedVersion_AndTheSessionEndsWithDelete()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([Echo]));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        IReadOnlyList<McpHttpRequestRecord> requests = server.Requests;
        Assert.IsNull(requests[0].Header("Mcp-Session-Id"));
        foreach (McpHttpRequestRecord later in requests.Skip(1))
        {
            Assert.AreEqual("session-1", later.Header("Mcp-Session-Id"), later.Method + " " + later.JsonRpcMethod);
            Assert.AreEqual("2025-06-18", later.Header("MCP-Protocol-Version"), later.Method + " " + later.JsonRpcMethod);
        }

        Assert.AreEqual("DELETE", requests[^1].Method);
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-StreamableHttp-8")]
    public async Task ExpiredSession404_CurlStartsANewSessionWithoutTheOldId()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([Echo])) { ExpiresSessionAtMethod = "tools/list" };

        await McpClientRun.KnownFailures.RunCaseAsync(["McpClient:MCP20250618-StreamableHttp-8"], async () =>
        {
            CurlRunResult result = await RunAsync(server, Url);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual("echo\tEchoes its text\n", Text(result.StandardOutput));
            McpHttpRequestRecord reinitialize = server.Requests.Where(request => request.JsonRpcMethod == "initialize").ElementAt(1);
            Assert.IsNull(reinitialize.Header("Mcp-Session-Id"));
        });
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-StreamableHttp-10")]
    public async Task Mcps_RunsTheSameExchangeOverTls()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([Echo]));
        using var certificate = ReferenceCertificate.CreateSelfSigned("localhost");

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
            Curl.Console.InProcessCurl.RunAsync,
            [ReferenceRoute.Tls(8443, server, certificate)],
            ["-sS", "-k", "-m", "30", "mcps://localhost:8443/mcp"]);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("echo\tEchoes its text\n", Text(result.StandardOutput));
        Assert.AreEqual(ReferenceTransport.Tls, result.Exchanges[0].Transport);
        CollectionAssert.AreEqual(new[] { "initialize", "notifications/initialized", "tools/list" }, PostedMethods(server));
    }
}
