using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp;

/// <summary>
/// The <c>transfer</c> tool through the host's real composition - the production
/// <c>ManagedCurlEngine</c> - against a loopback HTTP listener the test opens and closes itself.
/// It sends bytes over a socket, so it is an Integration test (ADR-0421).
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class CurlTransferToolIntegrationTests
{
    [TestMethod]
    public async Task TransferAsync_LoopbackServer_ReturnsItsStatusAndBody()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<string> served = LoopbackHttpResponder.ServeOneResponseAsync(listener, "HTTP/1.1 201 Created\r\nContent-Type: text/plain\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");
        IMcpTool tool = CurlMcpHost.CreateRegistry(McpConfinementRoot.NotConfigured, new McpResponseCap(McpResponseCap.DefaultMaxBytes), _ => null, _ => []).FindTool("transfer")!;

        using JsonDocument arguments = JsonDocument.Parse($$"""{"url":"http://127.0.0.1:{{port}}/item","headers":[{"name":"X-Test","value":"yes"}]}""");
        McpToolCallResult result = await tool.CallAsync(arguments.RootElement, CancellationToken.None);

        string request = await served;
        Assert.IsFalse(result.IsError, result.Content.Single().Text);
        StringAssert.StartsWith(request, "GET /item HTTP/1.1\r\n");
        StringAssert.Contains(request, "\r\nX-Test: yes\r\n");
        using JsonDocument json = JsonDocument.Parse(result.Content.Single().Text);
        Assert.AreEqual(201, json.RootElement.GetProperty("statusCode").GetInt64());
        Assert.AreEqual("hello", json.RootElement.GetProperty("body").GetProperty("content").GetString());
        Assert.AreEqual("text/plain", json.RootElement.GetProperty("info").GetProperty("contentType").GetString());
    }
}
