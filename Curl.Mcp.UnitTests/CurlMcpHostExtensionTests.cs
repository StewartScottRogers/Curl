using System.Text.Json;
using Curl.Extension.Composition;
using Curl.Mcp.Protocol.Model;
using Curl.Protocol.Abstractions;

namespace Curl.Mcp;

// The registry's real engine with a test extension (BL-4098): the extension's handler answers in process, so nothing is sent.
[TestClass]
public sealed class CurlMcpHostExtensionTests
{
    private static readonly McpResponseCap Cap = new(McpResponseCap.DefaultMaxBytes);

    private const string MarkdownOffLine = "Extensions are off: `CURL_EXTENSIONS` is `0`.";

    [TestMethod]
    public async Task CreateRegistry_WithExtensionsOn_TransferToolReachesTheExtensionHandler()
    {
        ReachedExtensionHandler handler = new();
        McpServerRegistry registry = CurlMcpHost.CreateRegistry(McpConfinementRoot.NotConfigured, Cap, _ => null, _ => [handler]);

        McpToolCallResult result = await CallTransferAsync(registry);

        Assert.IsFalse(result.IsError, result.Content[0].Text);
        Assert.AreEqual(1, handler.Transfers);
    }

    [TestMethod]
    public async Task CreateRegistry_WithCurlExtensionsZero_TransferToolRefusesTheSchemeAsUnsupported()
    {
        ReachedExtensionHandler handler = new();
        McpServerRegistry registry = CurlMcpHost.CreateRegistry(
            McpConfinementRoot.NotConfigured,
            Cap,
            name => name == "CURL_EXTENSIONS" ? "0" : null,
            _ => [handler]);

        McpToolCallResult result = await CallTransferAsync(registry);

        Assert.IsTrue(result.IsError, result.Content[0].Text);
        StringAssert.Contains(result.Content[0].Text, "not supported");
        Assert.AreEqual(0, handler.Transfers);
    }

    [TestMethod]
    public async Task CreateRegistry_WithExtensionsOn_ExtensionsResourceIsTheExtensionListing()
    {
        McpServerRegistry registry = CurlMcpHost.CreateRegistry(McpConfinementRoot.NotConfigured, Cap, _ => null, _ => []);

        string markdown = await ReadExtensionsResourceAsync(registry);

        Assert.AreEqual(ExtensionListing.Markdown(_ => null), markdown);
        Assert.IsFalse(markdown.Contains(MarkdownOffLine, StringComparison.Ordinal), markdown);
    }

    [TestMethod]
    public async Task CreateRegistry_WithCurlExtensionsZero_ExtensionsResourceSaysTheyAreOff()
    {
        Func<string, string?> off = name => name == "CURL_EXTENSIONS" ? "0" : null;
        McpServerRegistry registry = CurlMcpHost.CreateRegistry(McpConfinementRoot.NotConfigured, Cap, off, _ => []);

        string markdown = await ReadExtensionsResourceAsync(registry);

        Assert.AreEqual(ExtensionListing.Markdown(off), markdown);
        StringAssert.Contains(markdown, MarkdownOffLine);
    }

    [TestMethod]
    public async Task CreateRegistry_WithExtensionsOn_TransferToolReturnsTheExtensionHandlersBody()
    {
        ReachedExtensionHandler handler = new() { Body = "from the extension" };
        McpServerRegistry registry = CurlMcpHost.CreateRegistry(McpConfinementRoot.NotConfigured, Cap, _ => null, _ => [handler]);

        McpToolCallResult result = await CallTransferAsync(registry);

        Assert.IsFalse(result.IsError, result.Content[0].Text);
        using JsonDocument json = JsonDocument.Parse(result.Content[0].Text);
        Assert.AreEqual("from the extension", json.RootElement.GetProperty("body").GetProperty("content").GetString());
    }

    [TestMethod]
    [DataRow(null, false)]
    [DataRow("0", true)]
    public async Task CreateRegistry_ParseAndBuildUrl_AcceptAnExtendedSchemeUnlessCurlExtensionsIsZero(string? extensions, bool refused)
    {
        McpServerRegistry registry = CurlMcpHost.CreateRegistry(McpConfinementRoot.NotConfigured, Cap, name => name == "CURL_EXTENSIONS" ? extensions : null, _ => []);

        McpToolCallResult parsed = await CallAsync(registry, "parse_url", """{"url":"coap://example.com/a"}""");
        McpToolCallResult built = await CallAsync(registry, "build_url", """{"scheme":"coap","host":"example.com","path":"/a"}""");
        McpToolCallResult original = await CallAsync(registry, "parse_url", """{"url":"https://example.com/a"}""");

        Assert.AreEqual(refused, parsed.IsError, parsed.Content[0].Text);
        Assert.AreEqual(refused, built.IsError, built.Content[0].Text);
        Assert.AreEqual(refused, parsed.Content[0].Text.Contains("UnsupportedScheme", StringComparison.Ordinal), parsed.Content[0].Text);
        Assert.IsFalse(original.IsError, original.Content[0].Text);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("0")]
    public async Task CreateRegistry_VersionTool_ListsTheExtendedSchemesApartFromCurlsProtocols(string? extensions)
    {
        Func<string, string?> environment = name => name == "CURL_EXTENSIONS" ? extensions : null;
        McpServerRegistry registry = CurlMcpHost.CreateRegistry(McpConfinementRoot.NotConfigured, Cap, environment, _ => []);

        McpToolCallResult result = await CallAsync(registry, "version", "{}");

        using JsonDocument json = JsonDocument.Parse(result.Content[0].Text);
        JsonElement extended = json.RootElement.GetProperty("extendedProtocols");
        CollectionAssert.AreEqual(ExtensionListing.Schemes(environment).ToArray(), extended.EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.AreEqual(extensions is null, extended.GetArrayLength() > 0);
        Assert.IsFalse(json.RootElement.GetProperty("protocols").EnumerateArray().Any(item => item.GetString() == "coap"));
    }

    private static async Task<McpToolCallResult> CallAsync(McpServerRegistry registry, string tool, string arguments)
    {
        using JsonDocument document = JsonDocument.Parse(arguments);
        return await registry.FindTool(tool)!.CallAsync(document.RootElement, CancellationToken.None);
    }

    private static async Task<string> ReadExtensionsResourceAsync(McpServerRegistry registry)
    {
        McpResourceContents? contents = await registry.FindResourceProvider("curl://ai-help/extensions")!
            .ReadAsync("curl://ai-help/extensions", CancellationToken.None);
        return contents!.Text!;
    }

    private static async Task<McpToolCallResult> CallTransferAsync(McpServerRegistry registry)
    {
        using JsonDocument arguments = JsonDocument.Parse("""{"url":"example-extension://host/"}""");
        return await registry.FindTool("transfer")!.CallAsync(arguments.RootElement, CancellationToken.None);
    }

    private sealed class ReachedExtensionHandler : IProtocolHandler
    {
        public int Transfers { get; private set; }

        public string Body { get; init; } = "";

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["example-extension"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Transfers++;
            byte[] body = System.Text.Encoding.UTF8.GetBytes(Body);
            await context.Output.WriteAsync(body);
            return TransferResult.Success(body.Length);
        }
    }
}
