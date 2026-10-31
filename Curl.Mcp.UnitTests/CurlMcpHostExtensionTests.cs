using System.Text.Json;
using Curl.Mcp.Protocol.Model;
using Curl.Protocol.Abstractions;

namespace Curl.Mcp;

// The registry's real engine with a test extension (BL-4098): the extension's handler answers in process, so nothing is sent.
[TestClass]
public sealed class CurlMcpHostExtensionTests
{
    private static readonly McpResponseCap Cap = new(McpResponseCap.DefaultMaxBytes);

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

    private static async Task<McpToolCallResult> CallTransferAsync(McpServerRegistry registry)
    {
        using JsonDocument arguments = JsonDocument.Parse("""{"url":"example-extension://host/"}""");
        return await registry.FindTool("transfer")!.CallAsync(arguments.RootElement, CancellationToken.None);
    }

    private sealed class ReachedExtensionHandler : IProtocolHandler
    {
        public int Transfers { get; private set; }

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["example-extension"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Transfers++;
            return ValueTask.FromResult(TransferResult.Success(0));
        }
    }
}
