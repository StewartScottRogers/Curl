using System.Text.Json;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.McpClient;
using static Curl.Conformance.Extended.UnitTests.McpClient.McpClientRun;

namespace Curl.Conformance.Extended.UnitTests.McpClient;

/// <summary>MCP 2025-06-18 "Lifecycle" and "Base Protocol &gt; Overview" cases over Streamable HTTP.</summary>
[TestClass]
public sealed class McpLifecycleCases
{
    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Base-1")]
    [SpecRequirement("McpClient:MCP20250618-Base-2")]
    [SpecRequirement("McpClient:MCP20250618-Base-3")]
    [SpecRequirement("McpClient:MCP20250618-Lifecycle-1")]
    [SpecRequirement("McpClient:MCP20250618-Lifecycle-2")]
    [SpecRequirement("McpClient:MCP20250618-Lifecycle-3")]
    [SpecRequirement("McpClient:MCP20250618-Lifecycle-4")]
    [SpecRequirement("McpClient:MCP20250618-Lifecycle-5")]
    public async Task Initialize_ThenInitializedNotification_BeforeAnyOtherRequest()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([Echo]));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        CollectionAssert.AreEqual(new[] { "initialize", "notifications/initialized", "tools/list" }, PostedMethods(server));
        IReadOnlyList<McpHttpRequestRecord> posts = [.. server.Requests.Where(request => request.Method == "POST")];
        using JsonDocument initialize = JsonDocument.Parse(posts[0].Body);
        JsonElement parameters = initialize.RootElement.GetProperty("params");
        Assert.AreEqual("2025-06-18", parameters.GetProperty("protocolVersion").GetString());
        Assert.AreEqual(JsonValueKind.Object, parameters.GetProperty("capabilities").ValueKind);
        Assert.IsFalse(string.IsNullOrEmpty(parameters.GetProperty("clientInfo").GetProperty("name").GetString()));
        Assert.IsFalse(string.IsNullOrEmpty(parameters.GetProperty("clientInfo").GetProperty("version").GetString()));
        Assert.AreEqual("2.0", initialize.RootElement.GetProperty("jsonrpc").GetString());
        using JsonDocument initialized = JsonDocument.Parse(posts[1].Body);
        Assert.IsFalse(initialized.RootElement.TryGetProperty("id", out _), "A notification carries no id.");
        using JsonDocument list = JsonDocument.Parse(posts[2].Body);
        JsonElement firstId = initialize.RootElement.GetProperty("id");
        JsonElement listId = list.RootElement.GetProperty("id");
        Assert.IsTrue(firstId.ValueKind is JsonValueKind.Number or JsonValueKind.String);
        Assert.IsTrue(listId.ValueKind is JsonValueKind.Number or JsonValueKind.String);
        Assert.AreNotEqual(firstId.GetRawText(), listId.GetRawText(), "A request id is never reused within a session.");
        Assert.AreEqual("echo\tEchoes its text\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [DataRow("2025-03-26", DisplayName = "An older version")]
    [DataRow("1999-01-01", DisplayName = "A version no revision has")]
    [SpecRequirement("McpClient:MCP20250618-Lifecycle-6")]
    public async Task ServerAnswersAVersionCurlDoesNotSupport_CurlDisconnectsWithExit8(string version)
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([Echo]) { ProtocolVersion = version });

        await McpClientRun.KnownFailures.RunCaseAsync(["McpClient:MCP20250618-Lifecycle-6"], async () =>
        {
            CurlRunResult result = await RunAsync(server, Url);

            Assert.AreEqual(8, result.ExitCode);
            CollectionAssert.AreEqual(new[] { "initialize" }, PostedMethods(server));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        });
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Lifecycle-7")]
    public async Task ServerDeclaresNoToolsCapability_CurlSendsNoToolsRequestAndExits8()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([Echo]) { DeclaresTools = false });

        await McpClientRun.KnownFailures.RunCaseAsync(["McpClient:MCP20250618-Lifecycle-7"], async () =>
        {
            CurlRunResult result = await RunAsync(server, Url);

            Assert.AreEqual(8, result.ExitCode);
            Assert.IsFalse(PostedMethods(server).Any(method => method.StartsWith("tools/", StringComparison.Ordinal)));
        });
    }
}
