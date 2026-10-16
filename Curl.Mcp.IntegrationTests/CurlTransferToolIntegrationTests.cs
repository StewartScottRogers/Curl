using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;

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
        Task<string> served = ServeOneResponseAsync(listener, "HTTP/1.1 201 Created\r\nContent-Type: text/plain\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");
        using IHost host = CurlMcpHost.Build([], _ => null);
        CurlTransferTool tool = new(host.Services.GetRequiredService<McpTransferTool>());

        CallToolResult result = await tool.TransferAsync($"http://127.0.0.1:{port}/item", headers: [new McpTransferHeader("X-Test", "yes")]);

        string request = await served;
        Assert.IsFalse(result.IsError, ((TextContentBlock)result.Content.Single()).Text);
        StringAssert.StartsWith(request, "GET /item HTTP/1.1\r\n");
        StringAssert.Contains(request, "\r\nX-Test: yes\r\n");
        using JsonDocument json = JsonDocument.Parse(((TextContentBlock)result.Content.Single()).Text);
        Assert.AreEqual(201, json.RootElement.GetProperty("statusCode").GetInt64());
        Assert.AreEqual("hello", json.RootElement.GetProperty("body").GetProperty("content").GetString());
        Assert.AreEqual("text/plain", json.RootElement.GetProperty("info").GetProperty("contentType").GetString());
    }

    private static async Task<string> ServeOneResponseAsync(TcpListener listener, string response)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync();
        NetworkStream stream = client.GetStream();
        StringBuilder request = new();
        byte[] buffer = new byte[4096];
        while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            int read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            request.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
        await stream.FlushAsync();
        return request.ToString();
    }
}
