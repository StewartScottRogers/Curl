using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.McpClient;
using static Curl.Conformance.Extended.UnitTests.McpClient.McpClientRun;

namespace Curl.Conformance.Extended.UnitTests.McpClient;

/// <summary>MCP 2025-06-18 "Base Protocol &gt; Authorization" cases (BL-4906, ADR-1243).</summary>
[TestClass]
public sealed class McpAuthorizationCases
{
    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Authorization-1")]
    [SpecRequirement("McpClient:MCP20250618-Authorization-2")]
    [DataRow("--oauth2-bearer", "tok1")]
    [DataRow("-H", "Authorization: Bearer tok1")]
    public async Task BearerToken_EveryRequestCarriesItInTheAuthorizationHeader(string option, string value)
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([Echo])) { RequiredAuthorization = "Bearer tok1" };

        CurlRunResult result = await RunAsync(server, option, value, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("echo\tEchoes its text\n", Text(result.StandardOutput));
        Assert.IsGreaterThanOrEqualTo(3, server.Requests.Count);
        Assert.IsTrue(server.Requests.All(request => request.Header("Authorization") == "Bearer tok1" && request.Target == "/mcp"));
    }

    [TestMethod]
    public async Task UserAgentAndCustomHeader_EveryRequestCarriesThem()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([Echo]));

        CurlRunResult result = await RunAsync(server, "-H", "X-Api-Key: k1", "-A", "agent", Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.IsTrue(server.Requests.All(request => request.Header("X-Api-Key") == "k1" && request.Header("User-Agent") == "agent"));
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Authorization-3")]
    public async Task WithoutAToken_The401IsExit67()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([Echo])) { RequiredAuthorization = "Bearer tok1" };

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(67, result.ExitCode);
        Assert.AreEqual("curl: (67) The server answered with status 401.\n", Text(result.StandardError).Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
