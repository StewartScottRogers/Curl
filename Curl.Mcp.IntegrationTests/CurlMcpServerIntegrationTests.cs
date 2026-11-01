using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Curl.Mcp;

/// <summary>
/// The real Curl.Mcp process over standard input and output, driven by <see cref="McpStdioClient" />:
/// the handshake, the tool and resource lists, the <c>version</c> tool, and the <c>curl</c> tool against
/// a loopback listener and against the <c>CURL_MCP_ROOT</c> confinement. It starts a child process and
/// sends bytes over a socket, so it is an Integration test (ADR-0421).
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class CurlMcpServerIntegrationTests
{
    private string root = string.Empty;

    [TestInitialize]
    public void CreateRoot()
    {
        root = Path.Combine(Path.GetTempPath(), $"curl-mcp-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public async Task Initialize_ReportsTheToolsAndResourcesCapabilities()
    {
        await using McpStdioClient client = McpStdioClient.Start(root);

        JsonElement result = await client.InitializeAsync();

        JsonElement capabilities = result.GetProperty("capabilities");
        Assert.IsTrue(capabilities.TryGetProperty("tools", out _), result.GetRawText());
        Assert.IsTrue(capabilities.TryGetProperty("resources", out _), result.GetRawText());
    }

    [TestMethod]
    public async Task ToolsList_NamesTheFiveTools()
    {
        await using McpStdioClient client = McpStdioClient.Start(root);
        await client.InitializeAsync();

        JsonElement result = await client.RequestAsync("tools/list");

        string[] names = [.. result.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()!).Order(StringComparer.Ordinal)];
        CollectionAssert.AreEqual(new[] { "build_url", "curl", "parse_url", "transfer", "version" }, names);
    }

    [TestMethod]
    public async Task ResourcesList_NamesTheAiHelpIndexAndFullReference()
    {
        await using McpStdioClient client = McpStdioClient.Start(root);
        await client.InitializeAsync();

        JsonElement result = await client.RequestAsync("resources/list");

        string[] uris = [.. result.GetProperty("resources").EnumerateArray().Select(resource => resource.GetProperty("uri").GetString()!)];
        CollectionAssert.Contains(uris, McpAiHelpResourceCatalogue.IndexUri);
        CollectionAssert.Contains(uris, $"{McpAiHelpResourceCatalogue.IndexUri}/all");
        CollectionAssert.Contains(uris, McpAiHelpResourceCatalogue.ExtensionsUri);
        Assert.HasCount(McpAiHelpResourceCatalogue.Resources.Count + 1, uris);
    }

    [TestMethod]
    public async Task Version_ReportsTheEngineVersionAndHttp()
    {
        await using McpStdioClient client = McpStdioClient.Start(root);
        await client.InitializeAsync();

        JsonElement result = await client.CallToolAsync("version", []);

        Assert.IsFalse(IsError(result), ToolText(result));
        using JsonDocument version = JsonDocument.Parse(ToolText(result));
        Assert.IsFalse(string.IsNullOrEmpty(version.RootElement.GetProperty("version").GetString()));
        StringAssert.StartsWith(version.RootElement.GetProperty("versionLine").GetString(), "curl ");
        CollectionAssert.Contains(
            version.RootElement.GetProperty("protocols").EnumerateArray().Select(protocol => protocol.GetString()).ToArray(),
            "http");
    }

    [TestMethod]
    public async Task Curl_LoopbackServer_ReturnsItsBodyAndExitZero()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<string> served = LoopbackHttpResponder.ServeOneResponseAsync(listener, HelloResponse);
        await using McpStdioClient client = McpStdioClient.Start(root);
        await client.InitializeAsync();

        JsonElement result = await client.CallToolAsync("curl", CurlArguments("-sS", $"http://127.0.0.1:{port}/"));

        string request = await served;
        Assert.IsFalse(IsError(result), ToolText(result));
        using JsonDocument run = JsonDocument.Parse(ToolText(result));
        Assert.AreEqual(0, run.RootElement.GetProperty("exitCode").GetInt32());
        Assert.AreEqual("hello", run.RootElement.GetProperty("standardOutput").GetProperty("content").GetString());
        StringAssert.StartsWith(request, "GET / HTTP/1.1\r\n");
        StringAssert.Contains(request, $"\r\nHost: 127.0.0.1:{port}\r\n");
    }

    [TestMethod]
    public async Task Curl_OutputOutsideTheRoot_IsRefusedNamingTheOptionAndWritesNothing()
    {
        string outsideFolder = Path.Combine(Path.GetTempPath(), $"curl-mcp-outside-{Guid.NewGuid():N}");
        string outsideFile = Path.Combine(outsideFolder, "out.txt");
        await using McpStdioClient client = McpStdioClient.Start(root);
        await client.InitializeAsync();

        JsonElement result = await client.CallToolAsync("curl", CurlArguments("-sS", "-o", outsideFile, "http://127.0.0.1:1/"));

        Assert.IsTrue(IsError(result), ToolText(result));
        using JsonDocument run = JsonDocument.Parse(ToolText(result));
        StringAssert.StartsWith(run.RootElement.GetProperty("failure").GetProperty("text").GetString(), "Refused -o:");
        Assert.AreEqual(JsonValueKind.Null, run.RootElement.GetProperty("exitCode").ValueKind);
        Assert.IsFalse(File.Exists(outsideFile));
        Assert.IsFalse(Directory.Exists(outsideFolder));
    }

    [TestMethod]
    public async Task Curl_OutputInsideTheRoot_WritesTheBodyThere()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<string> served = LoopbackHttpResponder.ServeOneResponseAsync(listener, HelloResponse);
        await using McpStdioClient client = McpStdioClient.Start(root);
        await client.InitializeAsync();

        JsonElement result = await client.CallToolAsync("curl", CurlArguments("-sS", "-o", "inside.txt", $"http://127.0.0.1:{port}/"));

        await served;
        Assert.IsFalse(IsError(result), ToolText(result));
        Assert.AreEqual("hello", await File.ReadAllTextAsync(Path.Combine(root, "inside.txt")));
    }

    private const string HelloResponse = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello";

    private static JsonObject CurlArguments(params string[] args) =>
        new() { ["args"] = new JsonArray([.. args.Select(argument => (JsonNode?)JsonValue.Create(argument))]) };

    private static bool IsError(JsonElement result) =>
        result.TryGetProperty("isError", out JsonElement isError) && isError.GetBoolean();

    private static string ToolText(JsonElement result) =>
        result.GetProperty("content")[0].GetProperty("text").GetString()!;
}
